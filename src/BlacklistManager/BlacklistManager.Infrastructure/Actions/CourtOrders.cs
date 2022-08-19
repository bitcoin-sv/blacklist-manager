// Copyright (c) 2020 Bitcoin Association
using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.ExternalServiceViewModel;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.Bitcoin;
using Common.SmartEnums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Actions
{
  public class CourtOrders : ICourtOrders
  {
    readonly ITrustListRepository _trustListRepository;
    readonly ICourtOrderRepository _courtOrderRepository;
    readonly INodeRepository _nodeRepository;
    readonly ILegalEntityFactory _legalEntityServiceFactory;
    readonly ILogger<CourtOrders> _logger;
    readonly IConsensusActivationValidatorFactory _consensusActivationValidatorFactory;
    readonly ILegalEndpoints _legalEndpointsService;
    readonly IBitcoinFactory _bitcoindFactory;
    readonly IDelegatedKeys _delegatedKeys;
    readonly IConfigurationParams _configurationParams;
    readonly IMetrics _metrics;
    readonly AppSettings _appSettings;

    public CourtOrders(
      ITrustListRepository trustListRepository,
      ICourtOrderRepository courtOrderRepository,
      INodeRepository nodeRepository,
      ILegalEntityFactory legalEntityFactory,
      ILogger<CourtOrders> logger,
      IConsensusActivationValidatorFactory consensusActivationValidatorFactory,
      ILegalEndpoints legalEndpointsService,
      IBitcoinFactory bitcoindFactory,
      IDelegatedKeys delegatedKeys,
      IConfigurationParams configurationParams,
      IMetrics metrics,
      IOptions<AppSettings> options)
    {
      _trustListRepository = trustListRepository ?? throw new ArgumentNullException(nameof(trustListRepository));
      _courtOrderRepository = courtOrderRepository ?? throw new ArgumentNullException(nameof(courtOrderRepository));
      _nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      _legalEntityServiceFactory = legalEntityFactory ?? throw new ArgumentNullException(nameof(legalEntityFactory));
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _consensusActivationValidatorFactory = consensusActivationValidatorFactory ?? throw new ArgumentNullException(nameof(consensusActivationValidatorFactory));
      _legalEndpointsService = legalEndpointsService ?? throw new ArgumentNullException(nameof(legalEndpointsService));
      _bitcoindFactory = bitcoindFactory ?? throw new ArgumentNullException(nameof(bitcoindFactory));
      _delegatedKeys = delegatedKeys ?? throw new ArgumentNullException(nameof(delegatedKeys));
      _configurationParams = configurationParams ?? throw new ArgumentNullException(nameof(configurationParams));
      _metrics = metrics;
      _appSettings = options.Value;
    }

    public async Task<ProcessCourtOrderResult> ProcessCourtOrderAsync(JsonEnvelope signedCourtOrder, CourtOrder courtOrder, int? legalEntityEndpointId)
    {
      bool isSuccess = true;
      List<string> errorList = new();
      var (succes, errors) = await CheckReferencedCourtOrderAsync(courtOrder, signedCourtOrder);
      if (!succes)
      {
        errorList.Add(errors);
        isSuccess &= false;
      }
      if (!courtOrder.IsValid)
      {
        errorList.AddRange(courtOrder.ValidationMessages);
        isSuccess &= false;
      }
      if (!courtOrder.DoNetworksMatch(_appSettings.BitcoinNetwork, out var error))
      {
        errorList.Add(error);
        return new ProcessCourtOrderResult(courtOrder.CourtOrderHash, errorList.ToArray());
      }

      if (!isSuccess)
      {
        return new ProcessCourtOrderResult(courtOrder.CourtOrderHash, errorList.ToArray());
      }

      long? internalCourtOrderId;
      try
      {
        internalCourtOrderId = await ImportCourtOrderAsync(courtOrder, signedCourtOrder, legalEntityEndpointId);
      }
      catch (InvalidOrderException ex)
      {
        string errorMsg = $"Error while trying to store the court order to database. {ex.GetBaseException().Message}";
        return new ProcessCourtOrderResult(courtOrder.CourtOrderHash, errorMsg);
      }

      return new ProcessCourtOrderResult(courtOrder.CourtOrderHash, internalCourtOrderId);
    }

    private async Task<ProcessCourtOrderResult> ProcessConfiscationEnvelopeAsync(JsonEnvelope signedCourtOrder, ConfiscationEnvelope confiscationEnvelope, int? legalEntityEndpointId)
    {
      bool isSuccess = true;
      List<string> errorList = new();
      string coHash = SignatureTools.GetSigDoubleHash(confiscationEnvelope.ConfiscationCourtOrder, Encoding.UTF8.BodyName);

      CourtOrder confiscationCO;
      try
      {
        var confiscationVM = JsonSerializer.Deserialize<CourtOrderViewModel>(confiscationEnvelope.ConfiscationCourtOrder, SerializerOptions.SerializeOptionsNoPrettyPrint);
        confiscationCO = confiscationVM.ToDomainObject(coHash, Network.GetNetwork(_appSettings.BitcoinNetwork));
      }
      catch (Exception ex)
      {
        _logger.LogError(ex.ToString());
        errorList.Add(ex.Message);
        return new ProcessCourtOrderResult(coHash, errorList.ToArray());
      }

      if (!confiscationCO.IsValid)
      {
        errorList.AddRange(confiscationCO.ValidationMessages);
        isSuccess = false;
      }

      if (!confiscationCO.DoNetworksMatch(_appSettings.BitcoinNetwork, out var error))
      {
        errorList.Add(error);
        return new ProcessCourtOrderResult(coHash, errorList.ToArray());
      }

      var (success, Error) = await CheckReferencedCourtOrderAsync(confiscationCO, signedCourtOrder);
      if (!success)
      {
        errorList.Add(Error);
        isSuccess = false;
      }

      if (!isSuccess)
      {
        return new ProcessCourtOrderResult(coHash, errorList.ToArray());
      }


      var network = Network.GetNetwork(_appSettings.BitcoinNetwork);
      var confiscationTxOrder = confiscationEnvelope.ConfiscationTxDocument;
      var (success1, Errors) = confiscationTxOrder.Validate(confiscationCO, network);
      if (!success1)
      {
        return new ProcessCourtOrderResult(confiscationCO.CourtOrderHash, Errors);
      }

      long? courtOrderId;
      try
      {
        courtOrderId = await ImportCourtOrderAsync(confiscationCO, signedCourtOrder, legalEntityEndpointId);
        if (courtOrderId.HasValue)
        {
          await _courtOrderRepository.InsertConfiscationTransactionsAsync(courtOrderId.Value, confiscationTxOrder.ConfiscationTxData);
        }
      }
      catch (InvalidOrderException ex)
      {
        string errorMsg = $"Error while trying to store the court order to database. {ex.GetBaseException().Message}";
        return new ProcessCourtOrderResult(coHash, errorMsg);
      }

      return new ProcessCourtOrderResult(coHash, courtOrderId);
    }

    private async Task<(bool, string)> VerifyJsonEnvelopeAsync(JsonEnvelope signedCourtOrder)
    {
      if (signedCourtOrder == null)
      {
        return (false, $"Confiscation court order is incomplete.");
      }
      if (!(await _trustListRepository.IsPublicKeyTrustedAsync(signedCourtOrder.PublicKey)))
      {
        return (false, $"Public key '{signedCourtOrder.PublicKey}' used to sign the court order is not trusted.");
      }
      if (!SignatureTools.VerifyCourtOrderJsonEnvelope(signedCourtOrder))
      {
        return (false, "Digital signature applied to court order is invalid");
      }
      return (true, null);
    }

    public async Task<ProcessCourtOrderResult> ProcessSignedCourtOrderAsync<T>(JsonEnvelope signedCourtOrder, T order, int? legalEntityEndpointId = null)
    {
      object orderObj = order;
      ConfiscationEnvelope confiscationEnvelope = null;
      CourtOrder courtOrder = null;
      string coHash;
      if (typeof(T) == typeof(ConfiscationEnvelope))
      {
        confiscationEnvelope = (ConfiscationEnvelope)orderObj;
        coHash = SignatureTools.GetSigDoubleHash(confiscationEnvelope.ConfiscationCourtOrder, Encoding.UTF8.BodyName);
      }
      else if (typeof(T) == typeof(CourtOrder))
      {
        courtOrder = (CourtOrder)orderObj;
        coHash = courtOrder.CourtOrderHash;
      }
      else
      {
        throw new BadRequestException($"Unknown court order type '{typeof(T)}'");
      }

      var existingCo = (await _courtOrderRepository.GetCourtOrdersAsync(coHash, false)).ToList();
      if (existingCo != null && existingCo.Count > 0)
      {
        _logger.LogInformation($"Court order with id {coHash} already imported. Skipping.");
        return new ProcessCourtOrderResult(coHash, new string[] { })
        {
          AlreadyImported = true
        };
      }

      var (isSuccess, error) = await VerifyJsonEnvelopeAsync(signedCourtOrder);
      if (!isSuccess)
      {
        return new ProcessCourtOrderResult(coHash, new string[] { error });
      }
      if (confiscationEnvelope != null)
      {
        return await ProcessConfiscationEnvelopeAsync(signedCourtOrder, confiscationEnvelope, legalEntityEndpointId);
      }
      else
      {
        return await ProcessCourtOrderAsync(signedCourtOrder, courtOrder, legalEntityEndpointId);
      }
    }

    /// <summary>
    /// Imports court order into database. 
    /// If requested adds reference to legal entity endpoint.
    /// If requested start background jobs.
    /// </summary>
    /// <returns>false if order already imported</returns>
    private async Task<long?> ImportCourtOrderAsync(CourtOrder courtOrder, JsonEnvelope signedCourtOrder, int? legalEntityEndpointId)
    {
      string signedCOString = JsonSerializer.Serialize(signedCourtOrder, SerializerOptions.SerializeOptionsNoPrettyPrint);
      var courtOrderId = await _courtOrderRepository.InsertCourtOrderAsync(courtOrder, signedCOString, legalEntityEndpointId, signedCourtOrder.PublicKey);
      if (courtOrderId.HasValue)
      {
        _logger.LogInformation(LogEvents.CourtOrderStatusChanged, $"Court order '{courtOrder.CourtOrderHash}' imported");
      }
      else
      {
        _logger.LogInformation(LogEvents.CourtOrderStatusChanged, $"Court order '{courtOrder.CourtOrderHash}' already imported");
      }
      return courtOrderId;
    }

    public async Task<CourtOrderActivationResult> ActivateCourtOrdersAsync(CancellationToken cancellationToken)
    {
      try
      {
        var result = new CourtOrderActivationResult();
        var courtOrdersToActivate = await _courtOrderRepository.GetCourtOrdersToActivateAsync();
        if (courtOrdersToActivate.Any())
        {
          _logger.LogDebug($"Starting activation for {courtOrdersToActivate.Count()} court orders");
          foreach (var courtOrderHash in courtOrdersToActivate)
          {
            if (cancellationToken.IsCancellationRequested)
            {
              _logger.LogDebug("Activation of court orders canceled");
              break;
            }
            var co = (await _courtOrderRepository.GetCourtOrdersAsync(courtOrderHash, false)).SingleOrDefault();

            if (co is not null)
            {
              try
              {
                await SetCourtOrderStatusAsync(courtOrderHash, co.GetActiveStatus(), null);
                result.AddActivated(courtOrderHash);
              }
              catch (Exception ex)
              {
                _logger.LogError(LogEvents.CourtOrderActivation, ex, $"Activation of court order '{courtOrderHash}' aborted with exception");
              }
            }
          }
          _logger.LogInformation($"Activation of court orders ended. {result.ActivatedCourtOrders.Count()}/{courtOrdersToActivate.Count()} court orders activated successfully");
        }
        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(LogEvents.CourtOrderActivation, ex, $"Activation of court orders aborted with exception");
        return new CourtOrderActivationResult(internalError: true);
      }
    }

    public async Task<ProcessConsensusActivationResult> ActivateConsensusActivationsAsync(CancellationToken cancellationToken)
    {
      try
      {
        var pendingActivations = await _courtOrderRepository.GetUnprocessedConsensusActivationsAsync();
        var result = new ProcessConsensusActivationResult();

        foreach (var activation in pendingActivations)
        {
          var co = (await _courtOrderRepository.GetCourtOrdersAsync(activation.ActivationHash, false)).Single();
          var status = CourtOrderStatus.FreezeConsensus;
          if (co.Type == CourtOrderType.Unfreeze)
          {
            status = CourtOrderStatus.UnfreezeConsensus;
          }
          else if (co.Type == CourtOrderType.Confiscation)
          {
            status = CourtOrderStatus.ConfiscationConsensus;
          }
          await SetCourtOrderStatusAsync(activation.ActivationHash, status, activation.EnforceAtHeight);
          result.AddActivated(activation.ActivationHash);
        }
        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(LogEvents.ConsensusActivation, ex, $"Processing of consensus activations aborted with exception");
        return new ProcessConsensusActivationResult(internalError: true);
      }
    }

    public async Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus status, int? enforceAtHeight)
    {
      var courtOrder = (await _courtOrderRepository.GetCourtOrdersAsync(courtOrderHash, false)).SingleOrDefault();
      if (courtOrder is null)
      {
        throw new BadRequestException($"Court order '{courtOrderHash}' not found in database");
      }
      else if (courtOrder.Status != status)
      {
        if (!courtOrder.IsStatusChangeValid(status))
        {
          throw new BadRequestException($"Can not change court order '{courtOrder.CourtOrderHash}' status from '{courtOrder.Status}' to  '{status}'");
        }
        await _courtOrderRepository.SetCourtOrderStatusAsync(courtOrder.CourtOrderHash, status, enforceAtHeight);
        _logger.LogInformation(LogEvents.CourtOrderStatusChanged, $"Court order '{courtOrder.CourtOrderHash}' status changed to '{status}'");
      }
    }

    /// <summary>
    /// Process/fetch consensus activations and report if any pending consensus activations remain
    /// </summary>
    public async Task<ProcessConsensusActivationResult> GetConsensusActivationsAsync(CancellationToken cancellationToken)
    {
      try
      {
        var result = new ProcessConsensusActivationResult();

        var pendingConsensusActivations = await _courtOrderRepository.GetPendingConsensusActivationsAsync(_appSettings.MaxRetryCount, _appSettings.ConsensusWaitDays);

        if (pendingConsensusActivations.Any())
        {
          _logger.LogDebug($"Starting processing of {pendingConsensusActivations.Count()} consensus activations");

          foreach (var pda in pendingConsensusActivations)
          {
            try
            {
              // download
              var legalEntityService = _legalEntityServiceFactory.Create(pda.LegalEntityEndpointUrl, null, Common.Consts.ApiKeyHeaderName, pda.LegalEntityEndpointApiKey, pda.LegalEntityEndpointId);
              var consensusActivation = await legalEntityService.GetConsensusActivationAsync(pda.CourtOrderHash, cancellationToken);

              if (consensusActivation == null)
              {
                result.SetConsensusActivationsPending(false);
                _logger.LogDebug($"No consensus activation yet for '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}'");
                continue;
              }

              // validate
              var caValidator = _consensusActivationValidatorFactory.Create(consensusActivation, pda.CourtOrderHash);
              bool isCaValid = await caValidator.IsValidAsync();

              // insert into database
              await _courtOrderRepository.InsertConsensusActivationAsync(consensusActivation, pda.InternalCourtOrderId, pda.LegalEntityEndpointId, isCaValid, Network.GetNetwork(_appSettings.BitcoinNetwork), pda.RetryCount++);

              // edge case remark: only first consensus activation is processed in case of many consensusActivations for same courtOrder (signed) from different legal entities endpoints connected to same blacklist manager

              if (isCaValid)
              {
                result.Processed++;
                _logger.LogInformation(
                  $"Processed consensus activation '{consensusActivation.Hash}' for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}'");
              }
              else
              {
                result.SetConsensusActivationsPending();
                _logger.LogWarning(
                  $"Consensus activation '{consensusActivation.Hash}' for court order '{pda.CourtOrderHash}'" +
                  $"and '{pda.LegalEntityEndpointUrl}' is invalid: {string.Join(",", caValidator.Errors)}");
              }
            }
            catch (HttpRequestException ex)
            {
              result.SetConsensusActivationsPending(false);
              await _courtOrderRepository.UpdateLegalEntityEndpointErrorAsync(pda.LegalEntityEndpointId, "Error requesting consensus activation");
              _logger.LogError(LogEvents.ConsensusActivation, ex,
                $"Requesting of consensus activation for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}' aborted with exception");
            }
            catch (ValidationException ex)
            {
              result.SetConsensusActivationsPending();
              await _courtOrderRepository.UpdateLegalEntityEndpointErrorAsync(pda.LegalEntityEndpointId, "Received invalid consensus activation");
              _logger.LogError(LogEvents.ConsensusActivation, ex,
                $"Consensus activation for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}' is invalid");
            }
            catch (Exception ex)
            {
              result.SetConsensusActivationsPending();
              result.SetInternalError();
              await _courtOrderRepository.UpdateLegalEntityEndpointErrorAsync(pda.LegalEntityEndpointId, "Error processing consensus activation");
              _logger.LogError(LogEvents.ConsensusActivation, ex,
                $"Processing of consensus activation for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}' aborted with exception");
            }

            if (cancellationToken.IsCancellationRequested)
            {
              result.SetInternalError();
              break;
            }
          }

          _logger.LogInformation($"Processing of consensus activations ended. {result.Processed}/{pendingConsensusActivations.Count()} processed successfully");
        }
        _metrics.ProcessedConsensusActivations.Add(result.Processed);
        _metrics.FailedConsensusActivations.Add(result.Failed);
        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(LogEvents.ConsensusActivation, ex, $"Processing of consensus activations aborted with exception");
        return new ProcessConsensusActivationResult(internalError: true);
      }
    }

    private async Task<(bool IsSuccess, string Error)> CheckReferencedCourtOrderAsync(CourtOrder courtOrder, JsonEnvelope signedCourtOrder)
    {
      string error;
      if (!string.IsNullOrEmpty(courtOrder.FreezeCourtOrderHash))
      {
        var freezeCourtOrders = await _courtOrderRepository.GetCourtOrdersAsync(courtOrder.FreezeCourtOrderHash, true);
        if (freezeCourtOrders.Any())
        {
          if (freezeCourtOrders.Count() != 1)
          {
            error = $"Court order '{courtOrder.CourtOrderHash}' references {freezeCourtOrders.Count()} freeze court orders '{courtOrder.FreezeCourtOrderHash}'.";
            return (false, error);
          }

          var freezeCourtOrder = freezeCourtOrders.Single();

          if (freezeCourtOrder.SignedByKey != signedCourtOrder.PublicKey)
          {
            var trustListChain = await _trustListRepository.GetTrustListChainAsync(signedCourtOrder.PublicKey);
            if (!trustListChain.Any(x => x.PublicKey == freezeCourtOrder.SignedByKey))
            {
              error = "Key that was used to sign court order does not belong to the trust chain which was used to sign referenced order.";
              return (false, error);
            }
          }

          if (freezeCourtOrder.SignedDate > courtOrder.SignedDate)
          {
            return (false, "Referenced freeze order must have signature date lower than the order referencing it.");
          }

          if (courtOrder.DocumentType == DocumentType.ConfiscationOrder)
          {
            var confiscateFunds = courtOrder.Funds.Intersect(freezeCourtOrder.Funds, new FundEqualityComparerByTxOut()).ToArray();
            if (!confiscateFunds.Any() || confiscateFunds.Length != courtOrder.Funds.Count)
            {
              error = "Referenced funds marked for confiscation are missing.";
              return (false, error);
            }
          }
          else
          {
            var unreferenceFunds = courtOrder.Funds.Except(freezeCourtOrder.Funds, new FundEqualityComparerByTxOut()).ToList();
            if (unreferenceFunds.Any())
            {
              error = $"Not all court order funds exist on referenced court order. First unreferenced fund is '{unreferenceFunds.First().TxOut}'.";
              return (false, error);
            }
          }
        }
        else
        {
          error = "Referenced court order does not exist.";
          return (false, error);
        }
      }
      return (true, null);
    }

    public async Task CheckAndResendProcessingErrorsToLEsAsync(CancellationToken cancellationToken)
    {
      foreach (var error in (await _courtOrderRepository.GetValidationErrorsAsync(_appSettings.MaxRetryCount)).ToArray())
      {
        var legalEntity = await _legalEndpointsService.GetAsync(error.LegalEntityEndpointId);
        var legalEntityClient = _legalEntityServiceFactory.Create(legalEntity.BaseUrl, null, Common.Consts.ApiKeyHeaderName, legalEntity.APIKey, legalEntity.LegalEntityEndpointId);

        var coResult = JsonSerializer.Deserialize<ProcessCourtOrderResult>(error.ErrorData, SerializerOptions.SerializeOptions);
        await SendProcessingErrorsToLegalEntityAsync(legalEntityClient, coResult, cancellationToken);
      }
    }

    private async Task SendProcessingErrorsToLegalEntityAsync(ILegalEntity legalEntityClient, ProcessCourtOrderResult processCourtOrderResult, CancellationToken cancellationToken)
    {
      try
      {
        var activeKey = await _delegatedKeys.GetActiveKeyForSigningAsync();

        if (activeKey == null)
        {
          throw new InvalidOperationException("No active key for signing present. Will not send errors to NT.");
        }

        if (processCourtOrderResult.CourtOrderHash == null)
        {
          _logger.LogInformation("Court order hash is not known. Skipping sending of error to legal entity client.");
          return;
        }

        var nodes = (await _nodeRepository.GetNodesAsync()).ToArray();
        if (!nodes.Any())
        {
          return;
        }
        HelperTools.ShuffleArray(nodes);
        var node = nodes.First();
        var bitcoind = _bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
        var currentBestBlockHash = await bitcoind.GetBestBlockHashAsync();

        var coAcceptance = new Domain.ExternalServiceViewModel.CourtOrderAcceptanceViewModel
        {
          CreatedAt = DateTime.UtcNow,
          CourtOrderHash = processCourtOrderResult.CourtOrderHash,
          CurrentBlockHash = currentBestBlockHash,
          DocumentType = DocumentType.CourtOrderAcceptance,
          Rejection = processCourtOrderResult.Errors.ToArray()
        };
        if (activeKey.DelegationRequired)
        {
          coAcceptance.DelegatedKeys = activeKey.SignedDelegatedKeyJSON;
        }

        var jsonCoAcceptance = JsonSerializer.Serialize(coAcceptance, SerializerOptions.SerializeOptionsNoPrettyPrint);
        var wifKey = EncryptionTools.Decrypt(activeKey.DelegatedPrivateKey, _appSettings.EncryptionKey);
        var coAcceptanceJsonEnvelope = SignatureTools.CreateJSonSignature(jsonCoAcceptance, wifKey);

        await legalEntityClient.PostCourtOrderAcceptanceAsync(coAcceptance.CourtOrderHash, coAcceptanceJsonEnvelope, cancellationToken);

        await _courtOrderRepository.InsertValidationErrorsAsync(legalEntityClient.LegalEntityClientId.Value,
                                                                processCourtOrderResult.CourtOrderHash,
                                                                JsonSerializer.Serialize(processCourtOrderResult, SerializerOptions.SerializeOptions),
                                                                null);
        await _courtOrderRepository.MarkValidationErrorSuccessfulAsync(legalEntityClient.LegalEntityClientId.Value, processCourtOrderResult.CourtOrderHash);

        return;
      }
      catch (Exception ex)
      {
        _logger.LogError($"Unable to send error report for court order `{processCourtOrderResult.CourtOrderHash}` to legal entity client at `{legalEntityClient.BaseUrl}`. Will try again.");
        _logger.LogError(ex.GetBaseException().Message);

        await _courtOrderRepository.InsertValidationErrorsAsync(legalEntityClient.LegalEntityClientId.Value,
                                                                processCourtOrderResult.CourtOrderHash,
                                                                JsonSerializer.Serialize(processCourtOrderResult, SerializerOptions.SerializeOptions),
                                                                ex.ToString());
      }

      return;
    }

    public async Task ProcessGetCourtOrdersAsync(IEnumerable<LegalEntityEndpoint> ntEndpoints, CancellationToken cancellationToken)
    {
      int failures = 0;
      int successfull = 0;
      foreach (var notaryTool in ntEndpoints)
      {
        try
        {
          ILegalEntity legalEntityClient = null;
          bool useDeltaLink = false;

          if (string.IsNullOrEmpty(notaryTool.CourtOrderDeltaLink))
          {
            legalEntityClient = _legalEntityServiceFactory.Create(notaryTool.BaseUrl, null, Common.Consts.ApiKeyHeaderName, notaryTool.APIKey, notaryTool.LegalEntityEndpointId);
          }
          else
          {
            legalEntityClient = _legalEntityServiceFactory.Create(notaryTool.BaseUrl,
                                                                 notaryTool.CourtOrderDeltaLink,
                                                                 Consts.ApiKeyHeaderName,
                                                                 notaryTool.APIKey,
                                                                 notaryTool.LegalEntityEndpointId);
            useDeltaLink = true;
          }

          do
          {
            var coViewModel = await legalEntityClient.GetCourtOrdersAsync(useDeltaLink, cancellationToken);

            if (coViewModel == null)
            {
              break;
            }

            if (!string.IsNullOrEmpty(coViewModel.NextLink))
            {
              try
              {
                Uri urlBase = new Uri(legalEntityClient.BaseUrl);
                Uri urlNext = new Uri(coViewModel.NextLink);
                if (urlBase.Host != urlNext.Host || urlBase.Scheme != urlNext.Scheme)
                {
                  string errorMessage = $"NextLink host differs from BaseUrl host. BaseUrl '{legalEntityClient.BaseUrl}', NextLink '{coViewModel.NextLink}'.";
                  _logger.LogError(LogEvents.BackgroundJobs, $"Error while processing Legal Entity Endpoint {notaryTool.BaseUrl}: '{errorMessage}'.");
                  await _legalEndpointsService.UpdateLastErrorAsync(notaryTool.LegalEntityEndpointId, errorMessage, true);
                  break;
                }
              }
              catch (UriFormatException)
              {
                string errorMessage = $"NextLink host differs from BaseUrl host. BaseUrl '{legalEntityClient.BaseUrl}', NextLink '{coViewModel.NextLink}'.";
                _logger.LogError(LogEvents.BackgroundJobs, $"Error while processing Legal Entity Endpoint {notaryTool.BaseUrl}: '{errorMessage}'.");
                await _legalEndpointsService.UpdateLastErrorAsync(notaryTool.LegalEntityEndpointId, errorMessage, true);
                break;
              }

              legalEntityClient.DeltaLink = coViewModel.NextLink;
              useDeltaLink = true;
            }
            else
            {
              legalEntityClient.DeltaLink = coViewModel.DeltaLink;
              legalEntityClient.IsFinished = true;
            }

            foreach (var signedPayload in coViewModel.CourtOrders)
            {
              ProcessCourtOrderResult processResult = null;
              bool newCOInserted = false;
              string coHash = null;
              try
              {
                (processResult, newCOInserted, coHash) = await ProcessSignedPayloadAsync(signedPayload, notaryTool.LegalEntityEndpointId);
              }
              catch (Exception ex)
              {
                _logger.LogError(ex.ToString());
                processResult = new ProcessCourtOrderResult(coHash, new string[] { ex.GetBaseException().Message });
                await _legalEndpointsService.UpdateLastErrorAsync(notaryTool.LegalEntityEndpointId, ex.GetBaseException().Message, true);
              }

              if (processResult.Errors?.Count > 0)
              {
                failures++;
                _logger.LogError(LogEvents.BackgroundJobs, $"Error while processing court order with hash {processResult.CourtOrderHash}.");
                _logger.LogError(LogEvents.BackgroundJobs, $"Logging first error of '{processResult.Errors.Count}' errors. ");
                _logger.LogError(LogEvents.BackgroundJobs, processResult.Errors.First());

                await SendProcessingErrorsToLegalEntityAsync(legalEntityClient, processResult, cancellationToken);
              }
              else
              {
                successfull++;
              }
            }

            if (cancellationToken.IsCancellationRequested)
            {
              break;
            }
          } while (!legalEntityClient.IsFinished);

          await _legalEndpointsService.UpdateDeltaLinkAsync(notaryTool.LegalEntityEndpointId, legalEntityClient.DeltaLink, successfull);
        }
        catch(HttpRequestException ex)
        {
          _logger.LogError(LogEvents.BackgroundJobs, ex, $"Error while reading orders from {notaryTool.BaseUrl}");
          await _legalEndpointsService.UpdateLastErrorAsync(notaryTool.LegalEntityEndpointId, ex.GetBaseException().Message, true);
        }
        catch (Exception ex)
        {
          failures++;
          _logger.LogError(LogEvents.BackgroundJobs, ex, $"Error while processing orders from {notaryTool.BaseUrl}");
          await _legalEndpointsService.UpdateLastErrorAsync(notaryTool.LegalEntityEndpointId, ex.GetBaseException().Message, true);
        }
      }
      // metrics are added here, because we also want to report 0 to prometheus so that the graph doesn't have gaps
      _metrics.FailedCourtOrders.Add(failures);
      _metrics.ProcessedCourtOrders.Add(successfull);
    }

    private async Task<(ProcessCourtOrderResult, bool, string)> ProcessSignedPayloadAsync(SignedPayloadViewModel signedPayload, int legalEntityEndpointId)
    {
      ProcessCourtOrderResult processResult = null;
      bool newCOInserted = false;
      string coHash = null;

      var courtOrderVM = JsonSerializer.Deserialize<CourtOrderViewModel>(signedPayload.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationVM = JsonSerializer.Deserialize<ConfiscationEnvelopeViewModel>(signedPayload.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);
      var cancellationVM = JsonSerializer.Deserialize<CancelConfiscationOrderViewModel>(signedPayload.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);

      try
      {
        if (confiscationVM is not null && confiscationVM.DocumentType == DocumentType.ConfiscationEnvelope)
        {
          var domainConfiscationOrder = confiscationVM.ToDomainObject();
          coHash = SignatureTools.GetSigDoubleHash(domainConfiscationOrder.ConfiscationCourtOrder, Encoding.UTF8.BodyName);

          processResult = await ProcessSignedCourtOrderAsync(signedPayload.ToJsonEnvelope(), domainConfiscationOrder, legalEntityEndpointId);
          if (!processResult.AlreadyImported && !processResult.Errors.Any())
          {
            newCOInserted = true;
          }
        }
        else if (cancellationVM is not null && cancellationVM.DocumentType == DocumentType.CancelConfiscationOrder)
        {
          coHash = cancellationVM.ConfiscationOrderHash;
          processResult = await CancelConfiscationOrderAsync(signedPayload.ToJsonEnvelope(), cancellationVM.ConfiscationOrderHash);
        }
        else if (courtOrderVM is not null)
        {
          coHash = SignatureTools.GetSigDoubleHash(signedPayload.Payload, signedPayload.Encoding);
          CourtOrder domainOrder = courtOrderVM.ToDomainObject(coHash, NBitcoin.Network.GetNetwork(_appSettings.BitcoinNetwork));

          processResult = await ProcessSignedCourtOrderAsync(signedPayload.ToJsonEnvelope(), domainOrder, legalEntityEndpointId);
          if (!processResult.AlreadyImported && !processResult.Errors.Any())
          {
            newCOInserted = true;
          }
        }
      }
      catch(Exception ex)
      {
        return (new ProcessCourtOrderResult(coHash, ex.GetBaseException().Message), false, coHash);
      }

      if (courtOrderVM == null && confiscationVM == null && cancellationVM == null)
      {
        throw new InvalidOperationException($"Unknown document type");
      }

      return (processResult, newCOInserted, coHash);
    }

    public async Task<bool> ProcessFailedCourtOrdersAsync(CancellationToken token)
    {
      int successCount = 0;
      var failedOrderList = (await _courtOrderRepository.GetFailedCourtOrdersAsync()).ToArray();
      _logger.LogInformation($"Found {failedOrderList.Length} failed orders that will be retried.");

      foreach (var failedOrder in failedOrderList)
      {
        token.ThrowIfCancellationRequested();

        var legalEntity = await _legalEndpointsService.GetAsync(failedOrder.LegalEntityEndpointId);
        var legalEntityClient = _legalEntityServiceFactory.Create(legalEntity.BaseUrl, null, Common.Consts.ApiKeyHeaderName, legalEntity.APIKey, legalEntity.LegalEntityEndpointId);
        try
        {
          var signedPayload = await legalEntityClient.GetCourtOrderByHashAsync(failedOrder.CourtOrderHash, token);
          await ProcessSignedPayloadAsync(signedPayload, failedOrder.LegalEntityEndpointId);
          await _courtOrderRepository.MarkCourtOrderSuccessfullyProccesedAsync(failedOrder.LegalEntityEndpointId, failedOrder.CourtOrderHash);
          successCount++;
          _metrics.ProcessedCourtOrders.Add(1);
        }
        catch (Exception ex)
        {
          _logger.LogError(LogEvents.BackgroundJobs, ex, $"Error while reading and processing orders from {legalEntity.BaseUrl}");
          await Task.WhenAll(_legalEndpointsService.UpdateLastErrorAsync(failedOrder.LegalEntityEndpointId, ex.GetBaseException().Message, false),
                             _courtOrderRepository.InsertValidationErrorsAsync(failedOrder.LegalEntityEndpointId, failedOrder.CourtOrderHash, null, ex.GetBaseException().Message));
        }
      }

      _logger.LogInformation("{successCount}/{failedOrderCount} failed orders successfully processed.", successCount, failedOrderList.Length);
      return successCount > 0;
    }

    public async Task<(bool Successfull, int NoOfProcessed)> ProcessCourtOrderAcceptancesAsync(Node node, CancellationToken cancellationToken)
    {
      bool processingSuccessful = true;
      var bitcoind = _bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);

      var currentBestBlockHash = await bitcoind.GetBestBlockHashAsync();
      var courtOrders = await _courtOrderRepository.GetCourtOrdersToSendAcceptancesAsync(_appSettings.MaxRetryCount);
      var ntEndpoints = await _legalEndpointsService.GetAsync();
      var activeKey = await _delegatedKeys.GetActiveKeyForSigningAsync();
      int processedCourtOrders = 0;

      if (activeKey == null)
      {
        _logger.LogWarning("No active key available...will skip court order acceptance processing.");
        return (false, 0);
      }
      if (!courtOrders.Any())
      {
        return (true, 0);
      }
      _logger.LogInformation($"Will process court order acceptances with data from node {node}");
      foreach (var courtOrder in courtOrders)
      {
        foreach (var notaryTool in ntEndpoints.Where(x => courtOrder.CourtOrderAcceptances.Any(y => y.LegalEntityEndpointId == x.LegalEntityEndpointId)))
        {
          var courtOrderAcceptance = courtOrder.CourtOrderAcceptances.Single(x => x.LegalEntityEndpointId == notaryTool.LegalEntityEndpointId);

          try
          {
            var legalEntityClient = _legalEntityServiceFactory.Create(notaryTool.BaseUrl, null, Common.Consts.ApiKeyHeaderName, notaryTool.APIKey, notaryTool.LegalEntityEndpointId);

            var coAcceptance = new CourtOrderAcceptanceViewModel
            {
              CreatedAt = DateTime.UtcNow,
              CourtOrderHash = courtOrder.CourtOrderHash,
              CurrentBlockHash = currentBestBlockHash,
              DesiredHashrateAcceptancePercent = await _configurationParams.GetDesiredHashrateAcceptancePercentAsync(),
              DocumentType = DocumentType.CourtOrderAcceptance
            };
            if (activeKey.DelegationRequired)
            {
              coAcceptance.DelegatedKeys = activeKey.SignedDelegatedKeyJSON;
            }

            var jsonCoAcceptance = JsonSerializer.Serialize(coAcceptance, SerializerOptions.SerializeOptionsNoPrettyPrint);
            var wifKey = EncryptionTools.Decrypt(activeKey.DelegatedPrivateKey, _appSettings.EncryptionKey);
            var coAcceptanceJsonEnvelope = SignatureTools.CreateJSonSignature(jsonCoAcceptance, wifKey);

            await legalEntityClient.PostCourtOrderAcceptanceAsync(coAcceptance.CourtOrderHash, coAcceptanceJsonEnvelope, cancellationToken);

            await _courtOrderRepository.SetCourtOrderAcceptanceStatusAsync(courtOrderAcceptance.CourtOrderAcceptanceId, coAcceptanceJsonEnvelope, DateTime.UtcNow, null, null);
            _logger.LogInformation($"Court order acceptance for '{courtOrder.CourtOrderHash}' and '{notaryTool.BaseUrl}' processed successfully");
            processedCourtOrders++;
          }
          catch (Exception ex)
          {
            var retryCount = courtOrder.RetryCount.HasValue ? courtOrder.RetryCount++ : 1;
            processingSuccessful = false;
            _logger.LogError(LogEvents.BackgroundJobs, ex,
              $"Court order acceptance for '{courtOrder.CourtOrderHash}' and '{notaryTool.BaseUrl}' aborted with exception");
            await _courtOrderRepository.SetCourtOrderAcceptanceStatusAsync(courtOrderAcceptance.CourtOrderAcceptanceId, null, null, ex.GetBaseException().ToString(), retryCount);
            await _legalEndpointsService.UpdateLastErrorAsync(notaryTool.LegalEntityEndpointId, ex.GetBaseException().Message, true);
          }
        }
        if (cancellationToken.IsCancellationRequested)
        {
          processingSuccessful = false;
          break;
        }
      }

      return (processingSuccessful, processedCourtOrders);
    }

    public async Task<ProcessCourtOrderResult> CancelConfiscationOrderAsync(JsonEnvelope jsonEnvelope, string courtOrderHash)
    {
      var (isSuccess, error) = await VerifyJsonEnvelopeAsync(jsonEnvelope);
      if (!isSuccess)
      {
        return new ProcessCourtOrderResult(courtOrderHash, new string[] { error });
      }

      var confiscationOrders = await _courtOrderRepository.GetCourtOrdersAsync(courtOrderHash, true);
      if (confiscationOrders?.Any() != true)
      {
        return new ProcessCourtOrderResult(courtOrderHash, new string[] { $"Confiscation order {courtOrderHash} does not exist." });
      }

      var confiscationOrder = confiscationOrders.Single();
      if (confiscationOrder.Type != CourtOrderType.Confiscation)
      {
        return new ProcessCourtOrderResult(courtOrderHash, new string[] { $"Order with hash {courtOrderHash} is not a confiscation order." });
      }

      if (confiscationOrder.Status != CourtOrderStatus.ConfiscationPolicy)
      {
        return new ProcessCourtOrderResult(courtOrderHash, new string[] { $"Confiscation order with hash {courtOrderHash} must be policy enforced for cancellation." });
      }

      if (confiscationOrder.SignedByKey != jsonEnvelope.PublicKey)
      {
        var trustListChain = await _trustListRepository.GetTrustListChainAsync(jsonEnvelope.PublicKey);
        if (!trustListChain.Any(x => x.PublicKey == confiscationOrder.SignedByKey))
        {
          return new ProcessCourtOrderResult(courtOrderHash, new string[] { "Key that was used to sign cancellation order does not belong to the trust chain which was used to sign referenced order." });
        }
      }

      await SetCourtOrderStatusAsync(courtOrderHash, CourtOrderStatus.ConfiscationCancelled, null);

      return new ProcessCourtOrderResult();
    }
  }
}
