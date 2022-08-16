// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Authentication;
using BlacklistManager.Infrastructure.Repositories;
using BlacklistManager.Test.Functional.MockServices;
using BlacklistManager.Test.Functional.Server;
using Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using Newtonsoft.Json;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional
{
  /// <summary>
  /// Base class for test classes
  /// </summary>
  public class TestBase
  {
    public IConfigurationRoot Configuration { get; private set; }
    public ICourtOrderRepository CourtOrderRepository { get; private set; }
    public INodeRepository NodeRepository { get; private set; }
    public ITrustListRepository TrustlistRepository { get; private set; }
    public ILegalEntityRepository LegalEntityRepository { get; private set; }

    private static bool providerSet = false;
    private readonly string dbConnectionString;
    protected Microsoft.AspNetCore.TestHost.TestServer server;
    protected System.Net.Http.HttpClient client;
    protected IDomainAction domainLogic;
    protected BitcoindFactoryMock bitcoindFactory;
    protected LegalEntityFactoryMock legalEntityFactory;
    protected BackgroundJobsMock backgroundJobs;
    protected ILogger loggerTest;
    protected PropagationEventsMock propagationEvents;

    public const string LOG_CATEGORY = "BlacklistManager.Test.Functional";

    public static AutoResetEvent SyncTest = new AutoResetEvent(true);

    public TestBase()
    {
      if (!providerSet)
      {
        // uncomment if needed
        //NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Debug);
        //NpgsqlLogManager.IsParameterLoggingEnabled = true;
        providerSet = true;
      }

      string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      Configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(appPath, "appsettings.json"))
        .AddEnvironmentVariables()
        .Build();

      dbConnectionString = Configuration["BlacklistManagerConnectionStrings:DBConnectionString"];      
    }

    public async Task InitializeAsync(bool addPK = true, bool mockedServices = false, bool addValidDelegatedKey = false)
    {
      SyncTest.WaitOne(); // tests must not run in parallel since each test first deletes database


      //setup server
      server = await BlacklistManagerServer.CreateServerAsync(mockedServices);
      client = server.CreateClient();
      client.DefaultRequestHeaders.Add(ApiKeyAuthenticationHandler.ApiKeyHeaderName, Configuration["AppSettings:REST_APIKey"]);

      loggerTest = server.Services.GetRequiredService<ILoggerFactory>().CreateLogger(LOG_CATEGORY);
      loggerTest.LogInformation($"DbConnectionString: {dbConnectionString}");
      // setup repositories
      CourtOrderRepository = server.Services.GetRequiredService<ICourtOrderRepository>();
      NodeRepository = server.Services.GetRequiredService<INodeRepository>();
      TrustlistRepository = server.Services.GetRequiredService<ITrustListRepository>();
      LegalEntityRepository = server.Services.GetRequiredService<ILegalEntityRepository>();

      // delete database before each test
      CourtOrderRepositoryPostgres.EmptyRepository(dbConnectionString); //db owner must be used because sql statement "TRUNCATE .. RESTART IDENTITY CASCADE" needs it      

      if (addPK)
      {
        // add PK to trusted list
        TrustlistRepository.CreatePublicKey(Utils.PublicKey, true, null);
      }


      // setup common services
      domainLogic = server.Services.GetRequiredService<IDomainAction>();
      backgroundJobs = server.Services.GetRequiredService<IBackgroundJobs>() as BackgroundJobsMock;
      bitcoindFactory = server.Services.GetRequiredService<IBitcoindFactory>() as BitcoindFactoryMock;
      legalEntityFactory = server.Services.GetRequiredService<ILegalEntityFactory>() as LegalEntityFactoryMock;
      propagationEvents = server.Services.GetService<IPropagationEvents>() as PropagationEventsMock;

      if (bitcoindFactory != null)
      {
        bitcoindFactory.ClearCalls();
      }

      if (addValidDelegatedKey)
      {
        await InsertValidDelegatingKeyAsync();
      }

      // wait for background jobs that start on app start
      if (backgroundJobs != null)
      {
        await backgroundJobs.WaitAllAsync();
      }
    }

    public void Cleanup()
    {
      server?.Dispose();
      SyncTest.Set();
    }

    private async Task InsertValidDelegatingKeyAsync()
    {
      var BitcoinNetwork = Network.GetNetwork(Configuration["AppSettings:BitcoinNetwork"]);
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new NBitcoin.Key().GetBitcoinSecret(BitcoinNetwork).ToWif(),
        DelegationRequired = true
      };
      // Import signer key (privateKey/delegatedKey)
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();

      var minerPrivateKey = new NBitcoin.Key();
      var minerKeyCreate = new MinerKeyViewModelCreate
      {
        PublicKeyAddress = minerPrivateKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, BitcoinNetwork).ToString()
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);

      // Sign the payload
      var envelopeString = SignatureTools.CreateJSONWithBitcoinSignature(minerKey.DataToSign, minerPrivateKey.GetBitcoinSecret(BitcoinNetwork).ToWif(), BitcoinNetwork);
      var bitcoinSignatureEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(envelopeString, SerializerOptions.SerializeOptions);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = HelperTools.ConvertFromHexToBase64(bitcoinSignatureEnvelope.Signature),
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
    }
  }
}
