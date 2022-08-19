// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Repositories;
using BlacklistManager.Test.Functional.MockServices;
using BlacklistManager.Test.Functional.Server;
using Common;
using Common.Bitcoin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using NBitcoin.DataEncoders;
using System.IO;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
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
    public INodes Nodes { get; private set; }

    private static bool _providerSet = false;
    private readonly string _dbConnectionString;
    protected Microsoft.AspNetCore.TestHost.TestServer Server { get; private set; }
    protected System.Net.Http.HttpClient Client { get; private set; }
    protected ICourtOrders CourtOrders { get; private set; }
    protected ILegalEndpoints LegalEndpoints { get; private set; }
    protected BitcoinFactoryMock BitcoindFactory { get; private set; }
    protected LegalEntityFactoryMock LegalEntityFactory { get; private set; }
    protected BackgroundJobsMock BackgroundJobs { get; private set; }
    protected ILogger LoggerTest { get; private set; }
    protected ILoggerFactory LoggerFactory { get; private set; }
    protected PropagationEventsMock PropagationEvents { get; private set; }

    protected const string TEST_PRIVATE_KEY_WIF = "cNpxQaWe36eHdfU3fo2jHVkWXVt5CakPDrZSYguoZiRHSz9rq8nF";
    protected const string TEST_PUBLIC_KEY = "027ae06a5b3fe1de495fa9d4e738e48810b8b06fa6c959a5305426f78f42b48f8c";
    protected const string TEST_ADDRESS = "msRNSw5hHA1W1jXXadxMDMQCErX1X8whTk";

    protected const string TEST_PRIVATE_KEY_WIF_ALT1 = "cTtRXJv1c6aTDB2VGaTFPvbi6PJqrhVdarZnh2Ar7yXHYprdp8EG";
    protected const string TEST_PUBLIC_KEY_ALT1 = "025ee396c4025bc2bc85d8f5005e5d01ef7cf65eec4b8ffe6e473ff108c562e572";
    protected const string TEST_PRIVATE_KEY_WIF_ALT2 = "cVs34NMV54AAhQb1Rx3G1L9MH8ZkBTsQ6hw3QMFj3Dw9W6wEYBwq";
    protected const string TEST_PUBLIC_KEY_ALT2 = "02a0ef716d065bdca562b17059f019722ab652e5b2215bcd71e513f1f428dae049";

    public const string LOG_CATEGORY = "BlacklistManager.Test.Functional";

    public TestBase()
    {
      if (!_providerSet)
      {
        // uncomment if needed
        //NpgsqlLogManager.Provider = new ConsoleLoggingProvider(NpgsqlLogLevel.Debug);
        //NpgsqlLogManager.IsParameterLoggingEnabled = true;
        _providerSet = true;
      }

      string appPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
      Configuration = new ConfigurationBuilder()
        .AddJsonFile(Path.Combine(appPath, "appsettings.json"))
        .AddJsonFile(Path.Combine(appPath, "appsettings.development.json"), optional: true)
        .AddEnvironmentVariables()
        .Build();

      _dbConnectionString = Configuration["BlacklistManagerConnectionStrings:DBConnectionString"];
    }

    public async Task InitializeAsync(bool addPK = true, bool mockedServices = false, bool addValidDelegatedKey = false)
    {
      //setup server
      Server = await BlacklistManagerServer.CreateServerAsync(mockedServices);
      Client = Server.CreateClient();
      Client.DefaultRequestHeaders.Add(Consts.ApiKeyHeaderName, Configuration["AppSettings:REST_APIKey"]);

      LoggerFactory = Server.Services.GetRequiredService<ILoggerFactory>();
      LoggerTest = LoggerFactory.CreateLogger(LOG_CATEGORY);
      LoggerTest.LogInformation($"DbConnectionString: {_dbConnectionString}");
      // setup repositories
      CourtOrderRepository = Server.Services.GetRequiredService<ICourtOrderRepository>();
      NodeRepository = Server.Services.GetRequiredService<INodeRepository>();
      TrustlistRepository = Server.Services.GetRequiredService<ITrustListRepository>();
      LegalEntityRepository = Server.Services.GetRequiredService<ILegalEntityRepository>();
      Nodes = Server.Services.GetRequiredService<INodes>();


      if (addPK)
      {
        // add PK to trusted list
        await TrustlistRepository.CreatePublicKeyAsync(Utils.PublicKey, true, null);
        await TrustlistRepository.CreatePublicKeyAsync(TEST_PUBLIC_KEY, true, null);
      }


      // setup common services
      CourtOrders = Server.Services.GetRequiredService<ICourtOrders>();
      Nodes = Server.Services.GetRequiredService<INodes>();
      LegalEndpoints = Server.Services.GetRequiredService<ILegalEndpoints>();
      if (mockedServices)
      {
        BackgroundJobs = Server.Services.GetRequiredService<IBackgroundJobs>() as BackgroundJobsMock;
        BitcoindFactory = Server.Services.GetRequiredService<IBitcoinFactory>() as BitcoinFactoryMock;
        LegalEntityFactory = Server.Services.GetRequiredService<ILegalEntityFactory>() as LegalEntityFactoryMock;
        PropagationEvents = Server.Services.GetService<IPropagationEvents>() as PropagationEventsMock;
      }

      if (BitcoindFactory != null)
      {
        BitcoindFactory.ClearCalls();
      }

      // Let's stop all background jobs so they don't interfere with our tests
      if (BackgroundJobs != null)
      {
        await BackgroundJobs.StopAllAsync();
        await BackgroundJobs.SetOfflineModeAsync(true);
      }

      if (addValidDelegatedKey)
      {
        await InsertValidDelegatingKeyAsync();
      }
    }

    public async Task CleanupAsync()
    {
      // Let's stop all background jobs
      if (BackgroundJobs != null)
      {
        await BackgroundJobs.StopAllAsync();
      }

      Server?.Dispose();
      // delete database after each test
      await CourtOrderRepositoryPostgres.EmptyRepositoryAsync(_dbConnectionString);
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
      var response = await Client.PostAsync(uri, new StringContent(JsonSerializer.Serialize(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var signerKeyResponse = JsonSerializer.Deserialize<SignerKeyViewModelGet>(await response.Content.ReadAsStringAsync());

      var minerPrivateKey = new NBitcoin.Key();
      var minerKeyCreate = new MinerKeyViewModelCreate
      {
        PublicKeyAddress = minerPrivateKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, BitcoinNetwork).ToString()
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{signerKeyResponse.SignerId}/minerKey";
      response = await Client.PostAsync(postUri, new StringContent(JsonSerializer.Serialize(minerKeyCreate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonSerializer.Deserialize<MinerKeyViewModelGet>(minerKeyResponse);

      // Sign the payload
      var envelopeString = SignatureTools.CreateJSONWithBitcoinSignature(minerKey.DataToSign, minerPrivateKey.GetBitcoinSecret(BitcoinNetwork).ToWif(), BitcoinNetwork);
      var bitcoinSignatureEnvelope = JsonEnvelope.ToObject(envelopeString);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = HelperTools.ConvertFromHexToBase64(bitcoinSignatureEnvelope.Signature),
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{signerKeyResponse.SignerId}/minerKey";
      response = await Client.PutAsync(putUri, new StringContent(JsonSerializer.Serialize(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
    }

    public string SignWithTestKey(object obj2Sign)
    {
      var payload = JsonSerializer.Serialize(obj2Sign, SerializerOptions.SerializeOptionsNoPrettyPrint);
      return SignWithTestKey(payload);
    }
    public string SignWithTestKey(string payload)
    {
      return SignatureTools.CreateJSONWithBitcoinSignature(payload, TEST_PRIVATE_KEY_WIF, Network.GetNetwork(Configuration["AppSettings:BitcoinNetwork"]), true);
    }


    public string CreateConfiscationTx(CourtOrderViewModelCreate.Fund fund, string refCourtOrderhash, string destinationAddress, string destinationFeeAddress, long amount2Confiscate)
    {
      var address = BitcoinAddress.Create(destinationAddress, Network.RegTest);
      var feeAddress = BitcoinAddress.Create(destinationFeeAddress, Network.RegTest);
      long feeValue = fund.Value - amount2Confiscate;

      var tx = Transaction.Create(Network.RegTest);
      tx.Inputs.Add(new OutPoint { Hash = new uint256(fund.TxOut.TxId), N = (uint)fund.TxOut.Vout });
      tx.Outputs.Add(new NBitcoin.TxOut(Money.Satoshis(amount2Confiscate), address));
      tx.Outputs.Add(new NBitcoin.TxOut(Money.Satoshis(feeValue), feeAddress));

      var script = new Script(OpcodeType.OP_FALSE);
      script += OpcodeType.OP_RETURN;
      // Add protocol id
      script += Op.GetPushOp(Encoders.Hex.DecodeData(Common.Consts.ConfiscationProtocolId));
      script += Op.GetPushOp(Encoders.Hex.DecodeData($"01{ refCourtOrderhash}"));
      tx.Outputs.Add(new NBitcoin.TxOut(0L, script));

      return tx.ToHex();
    }

    public async Task WaitBackgrounJobUntilCompletedAsync(string groupName, int cancellationTime = 5000)
    {
      Assert.IsTrue(await BackgroundJobs.BackgroundTasks.WaitUntilCompletedAsync(groupName, cancellationTime));
    }
  }
}
