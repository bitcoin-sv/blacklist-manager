// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Microsoft.Extensions.Options;

namespace BlacklistManager.Domain.Actions
{
  public class DomainAction : IDomainAction
  {
    private readonly ITrustListRepository trustListRepository;
    private readonly INodes nodes;
    private readonly ICourtOrders courtOrders;
    private readonly ILegalEndpoints legalEntityEndpoints;
    private readonly ILogger<BlackListManagerLogger> logger;
    private readonly IDelegatedKeys delegatedKeys;
    private readonly ILegalEntityFactory legalEntityFactory;
    readonly AppSettings appSettings;

    public DomainAction(
      ITrustListRepository trustListRepository,
      IDelegatedKeys delegatedKeys,
      ICourtOrders courtOrdes,
      INodes nodes,
      ILegalEndpoints legalEntityEndpoints,
      ILegalEntityFactory legalEntityFactory,
      IOptions<AppSettings> appSettings,
      ILogger<BlackListManagerLogger> logger)
    {
      this.trustListRepository = trustListRepository ?? throw new ArgumentNullException(nameof(trustListRepository));
      this.delegatedKeys = delegatedKeys ?? throw new ArgumentNullException(nameof(delegatedKeys));
      this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
      this.courtOrders = courtOrdes ?? throw new ArgumentNullException(nameof(courtOrdes));
      this.legalEntityEndpoints = legalEntityEndpoints ?? throw new ArgumentNullException(nameof(legalEntityEndpoints));
      this.legalEntityFactory = legalEntityFactory ?? throw new ArgumentNullException(nameof(legalEntityFactory));
      this.appSettings = appSettings.Value ?? throw new ArgumentNullException(nameof(appSettings));
      this.logger = logger;
    }

    public async Task<ProcessCourtOrderResult> ProcessCourtOrderAsync(string signedCourtOrder, CourtOrder courtOrder, int? legalEntityEndpointId, bool onSuccessStartBackgroundJobs = true)
    {
      var checkResult = await courtOrders.CheckReferencedCourtOrderAsync(courtOrder);
      checkResult.IfNotPassedThrowBadRequestException();
      var imported = await courtOrders.ImportCourtOrderAsync(courtOrder, signedCourtOrder, legalEntityEndpointId, onSuccessStartBackgroundJobs);
      return new ProcessCourtOrderResult(courtOrder.CourtOrderHash, !imported);
    }

    public async Task<ProcessCourtOrderResult> ProcessSignedCourtOrderAsync(JsonEnvelope signedCourtOrder, CourtOrder courtOrder, int? legalEntityEndpointId = null, bool onSuccessStartBackgroundJobs = true)
    {
      if (!SignatureTools.VerifyCourtOrderJsonEnvelope(signedCourtOrder))
      {
        throw new BadRequestException("Digital signature applied to court order is invalid");
      }

      if (!trustListRepository.IsPublicKeyTrusted(signedCourtOrder.PublicKey))
      {
        throw new BadRequestException($"Public key '{signedCourtOrder.PublicKey}' used to sign the court order is not trusted.");
      }

      string signedCOString = HelperTools.JSONSerializeNewtonsoft(signedCourtOrder, true);
      return await ProcessCourtOrderAsync(signedCOString, courtOrder, legalEntityEndpointId, onSuccessStartBackgroundJobs);
    }

    public async Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus status, int? enforceAtHeight)
    {
      await courtOrders.SetCourtOrderStatusAsync(courtOrderHash, status, enforceAtHeight);
    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
      return await nodes.CreateNodeAsync(node);
    }

    public async Task<bool> UpdateNodeAsync(Node node)
    {
      return await nodes.UpdateNodeAsync(node);
    }

    public Node GetNode(string id)
    {
      return nodes.GetNode(id);
    }

    public int DeleteNode(string id)
    {
      return nodes.DeleteNode(id);
    }

    public IEnumerable<Node> GetNodes()
    {
      return nodes.GetNodes();
    }

    public async Task<LegalEntityEndpoint> CreateLegalEntityEndpointAsync(string baseUrl, string apiKey)
    {
      var legalEntityEndpoint = legalEntityFactory.Create(baseUrl, apiKey);
      var publicKey = await legalEntityEndpoint.GetPublicKeyAsync();

      if (!trustListRepository.IsPublicKeyTrusted(publicKey))
      {
        throw new BadRequestException($"Public key '{publicKey}' used by '{baseUrl}' to sign documents is not trusted.");
      }

      return await legalEntityEndpoints.CreateAsync(baseUrl, apiKey);
    }

    public async Task<IEnumerable<LegalEntityEndpoint>> GetLegalEntityEndpointAsync()
    {
      return await legalEntityEndpoints.GetAsync();
    }

    public async Task<LegalEntityEndpoint> GetLegalEntityEndpointAsync(int id)
    {
      return await legalEntityEndpoints.GetAsync(id);
    }

    public async Task<bool> UpdateLegalEntityEndpointAsync(int id, string baseUrl, string apiKey)
    {
      return await legalEntityEndpoints.UpdateAsync(id, baseUrl, apiKey);
    }

    public async Task<bool> DisableLegalEntityEndpointAsync(int id)
    {
      return await legalEntityEndpoints.DisableAsync(id);
    }

    public async Task<bool> EnableLegalEntityEndpointAsync(int id)
    {
      return await legalEntityEndpoints.EnableAsync(id);
    }

    public async Task<bool> ResetLegalEntityEndpointAsync(int id)
    {
      return await legalEntityEndpoints.ResetAsync(id);
    }

    public async Task CreateInitialSignerKeyAsync(Network network)
    {
      if ((await delegatedKeys.GetDelegatedKeysAsync(null)).Any())
      {
        // First key is already present in database
        return;
      }

      logger.LogInformation("Delegatedkey table does not contain any keys for signing documents. Will insert first key, that needs to be activated.");
      var firstKey = new Key();
      var encrypted = Encryption.Encrypt(firstKey.ToString(network), appSettings.EncryptionKey);
      await delegatedKeys.InsertDelegatedKeyAsync(encrypted, firstKey.PubKey.ToHex(), true, false); 
      logger.LogInformation("Key inserted successfully");
    }

  }
}
