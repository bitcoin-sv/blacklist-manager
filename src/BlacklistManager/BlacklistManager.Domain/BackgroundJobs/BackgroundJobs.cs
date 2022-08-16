// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using Common;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using BlacklistManager.Domain.ExternalServiceViewModel;
using BlacklistManager.Domain.Actions;
using System.Threading.Tasks;
using Common.SmartEnums;
using Microsoft.Extensions.Options;

namespace BlacklistManager.Domain.BackgroundJobs
{
  public class BackgroundJobs : IBackgroundJobs
  {
    protected readonly BackgroundTasks backgroundTasks;
    private readonly ILogger logger;
    private readonly AppSettings appSettings;

    public BackgroundJobs(
      IServiceProvider serviceProvider,
      ILogger<BackgroundTask> bgtLogger,
      ILoggerFactory logger,
      IOptions<AppSettings> options)
    {
      backgroundTasks = new BackgroundTasks(serviceProvider, bgtLogger);
      this.logger = logger.CreateLogger(LogCategories.DomainBackgrundJobs);
      this.appSettings = options.Value;
    }

    private const int ON_ERROR_RETRY_DELAY = 60 * 1000; //1min
    private const int CONSENSUS_ACTIVATION_RETRY_DELAY = 30 * 1000; //30 sec
    public const string PROPAGATE_FUNDS = "fundPropagation";
    public const string PROCESS_COURTORDERS = "courtOrderProcessing";
    public const string PROCESS_CONSENSUS_ACTIVATION = "consensusActivation";
    public const string PROCESS_EXPIRED_COURTORDERS = "expiredCourtOrderProcessing";
    public const string DOWNLOAD_COURTORDERS = "courtOrderDownload";
    public const string PROCESS_ACCEPTANCES = "courtOrderAcceptance";

    /// <summary>
    /// Defines delay in millisecond for job retry after error
    /// </summary>
    protected virtual int OnErrorRetryDelay => ON_ERROR_RETRY_DELAY;

    /// <summary>
    /// Defines delay in millisecond between consensus activation checks
    /// </summary>
    protected virtual int ConsensusActivationRetryDelay => CONSENSUS_ACTIVATION_RETRY_DELAY;


    protected virtual BackgroundTasks BackgroundTasks => backgroundTasks;

    public async Task StartPropagateFundsStatesAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
        PROPAGATE_FUNDS,
        async (cancellationToken, progress, serviceProvider) =>
        {
          logger.LogDebug("Funds states propagation started");
          bool propagationSuccessful = false;
          do
          {
            try
            {
              if (cancellationToken.IsCancellationRequested)
              {
                break;
              }
              var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();
              var propagationEvents = serviceProvider.GetService<IPropagationEvents>();
              var propagation = await courtOrders.PropagateFundsStateAsync(cancellationToken);
              propagationSuccessful = propagation.WasSuccessful;
              propagationEvents?.Finished(propagationSuccessful);

              if (!propagationSuccessful)
              {
                logger.LogWarning($"Funds states propagation did not succeed. Will retry in {OnErrorRetryDelay}ms");
                await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
              }
            }
            // All exceptions are handled here because background job should continue running, despite failing this time
            catch (Exception ex)
            {
              logger.LogError(LogEvents.BackgroundJobs, ex, $"Funds states propagation aborted with error. Will retry in {OnErrorRetryDelay}ms");
              await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
            }
          } while (!propagationSuccessful && !cancellationToken.IsCancellationRequested);

          if (cancellationToken.IsCancellationRequested)
          {
            logger.LogDebug("Funds states propagation canceled");
            cancellationToken.ThrowIfCancellationRequested();
          }
          logger.LogDebug("Funds states propagation ended");
        });
    }

    public async Task StartProcessCourtOrdersAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
        PROCESS_COURTORDERS,
        async (cancellationToken, progress, serviceProvider) =>
        {
          logger.LogDebug("Court order processing  started");
          bool processingSuccessful = false;
          do
          {
            try
            {
              if (cancellationToken.IsCancellationRequested)
              {
                break;
              }
              var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();
              var activation = await courtOrders.ActivateCourtOrdersAsync(cancellationToken);
              processingSuccessful = activation.WasSuccessful;

              if (!processingSuccessful)
              {
                logger.LogWarning($"Court order processing did not succeed. Will retry in {OnErrorRetryDelay}ms");
                await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
              }
            }
            // All exceptions are handled here because background job should continue running, despite failing this time
            catch (Exception ex)
            {
              logger.LogError(LogEvents.BackgroundJobs, ex, $"Court order processing aborted with error. Will retry in {OnErrorRetryDelay}ms");
              await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
            }
          } while (!processingSuccessful && !cancellationToken.IsCancellationRequested);

          if (cancellationToken.IsCancellationRequested)
          {
            logger.LogDebug("Court order processing canceled");
            cancellationToken.ThrowIfCancellationRequested();
          }
          logger.LogDebug("Court order processing ended");
        });
    }

    public async Task StartProcessConsensusActivationAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
        PROCESS_CONSENSUS_ACTIVATION,
        async (cancellationToken, progress, serviceProvider) =>
        {
          logger.LogDebug("Consensus activation processing  started");
          bool processingSuccessful = false;
          bool processingDone = false;
          do
          {
            try
            {
              if (cancellationToken.IsCancellationRequested)
              {
                break;
              }

              var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();
              var result = await courtOrders.ProcessConsensusActivationsAsync(cancellationToken);
              processingSuccessful = result.WasSuccessful;
              processingDone = !result.AnyConsensusActivationsStillPending;

              if (!processingSuccessful)
              {
                logger.LogWarning($"Consensus activation processing did not succeed. Will retry in {OnErrorRetryDelay}ms");
                await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
              }
              else if (!processingDone)
              {
                logger.LogWarning($"Pending consensus activations exists. Will retry in {ConsensusActivationRetryDelay}ms");
                await new LongWait().WaitUntilAsync(ConsensusActivationRetryDelay, cancellationToken);
              }
            }
            // All exceptions are handled here because background job should continue running, despite failing this time
            catch (Exception ex)
            {
              logger.LogError(LogEvents.BackgroundJobs, ex, $"Consensus activation processing aborted with error. Will retry in {OnErrorRetryDelay}ms");
              await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
            }
          } while (!processingDone && !cancellationToken.IsCancellationRequested);

          if (cancellationToken.IsCancellationRequested)
          {
            logger.LogDebug("Consensus activation processing canceled");
            cancellationToken.ThrowIfCancellationRequested();
          }
          logger.LogDebug("Consensus activation processing ended");
        });
    }

    public Task StopAllAsync()
    {
      return BackgroundTasks.StopAllAsync();
    }

    public async Task StartAllAsync()
    {
      await StartGetCourtOrdersAsync();
      await StartProcessCourtOrderAcceptancesAsync();
      await StartProcessCourtOrdersAsync();
      await StartProcessConsensusActivationAsync();
      await StartPropagateFundsStatesAsync();
    }

    public async Task StartGetCourtOrdersAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
      DOWNLOAD_COURTORDERS,
      async (cancellationToken, progress, serviceProvider) =>
      {
        var domainAction = serviceProvider.GetRequiredService<IDomainAction>();
        var legalEndpointsService = serviceProvider.GetRequiredService<ILegalEndpoints>();
        var longWait = serviceProvider.GetRequiredService<ILongWait>();
        var legalEntityFactory = serviceProvider.GetRequiredService<ILegalEntityFactory>();
        logger.LogDebug("Retrieving of court orders from legal endpoints starting");
        var delayTime = appSettings.BackgroundJobDelayTime;

        do
        {
          try
          {
            var ntEndpoints = await legalEndpointsService.GetLegalEntitiyEndpointsAsync();
            bool newCOInserted = false;
            foreach (var notaryTool in ntEndpoints)
            {
              try
              {
                ILegalEntity legalEntityClient = null;
                bool ignoreAPIMethod = false;

                if (string.IsNullOrEmpty(notaryTool.CourtOrderDeltaLink))
                {
                  legalEntityClient = legalEntityFactory.Create(notaryTool.BaseUrl, notaryTool.APIKey);
                }
                else
                {
                  legalEntityClient = legalEntityFactory.Create(notaryTool.CourtOrderDeltaLink, notaryTool.APIKey);
                  ignoreAPIMethod = true;
                }

                string deltaLink = null;
                do
                {
                  var coViewModel = await legalEntityClient.GetCourtOrdersAsync(ignoreAPIMethod);

                  if (coViewModel == null)
                  {
                    deltaLink = notaryTool.CourtOrderDeltaLink;
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
                        string errorMessage = "NextLink host differs from BaseUrl host. BaseUrl '{legalEntityClient.BaseUrl}', NextLink '{coViewModel.NextLink}'.";
                        logger.LogError(LogEvents.BackgroundJobs, $"Error while processing Legal Entity Endpoint {notaryTool.BaseUrl}: '{errorMessage}'.");
                        legalEndpointsService.UpdateLastError(notaryTool.LegalEntityEndpointId, errorMessage);
                        break;
                      }
                    }
                    catch (System.UriFormatException)
                    {
                      string errorMessage = "NextLink host differs from BaseUrl host. BaseUrl '{legalEntityClient.BaseUrl}', NextLink '{coViewModel.NextLink}'.";
                      logger.LogError(LogEvents.BackgroundJobs, $"Error while processing Legal Entity Endpoint {notaryTool.BaseUrl}: '{errorMessage}'.");
                      legalEndpointsService.UpdateLastError(notaryTool.LegalEntityEndpointId, errorMessage);
                      break;
                    }

                    legalEntityClient.BaseUrl = coViewModel.NextLink;
                    ignoreAPIMethod = true;
                  }
                  else
                  {
                    legalEntityClient.BaseUrl = null;
                    deltaLink = coViewModel.DeltaLink;
                  }

                  foreach (var signedPayload in coViewModel.CourtOrders)
                  {
                    Models.CourtOrder domainOrder = null;

                    domainOrder = System.Text.Json.JsonSerializer
                      .Deserialize<ExternalServiceViewModel.CourtOrder>(signedPayload.Payload, SerializerOptions.SerializeOptions)
                      .ToDomainObject(
                        SignatureTools.GetSigDoubleHash(signedPayload.Payload, signedPayload.Encoding));
                    if (domainOrder.ValidFrom > DateTime.UtcNow)
                    {
                      logger.LogWarning(LogEvents.BackgroundJobs, $"Error while processing court order {domainOrder.CourtOrderHash}. Message: ValidFrom date is set into the future, skiping order.");
                      continue;
                    }
                    try
                    {
                      var processCOResult = await domainAction.ProcessSignedCourtOrderAsync(signedPayload.ToJsonEnvelope(), domainOrder, notaryTool.LegalEntityEndpointId, false);
                      if (!processCOResult.AlreadyImported)
                      {
                        newCOInserted = true;
                      }
                    }
                    catch (Exception ex)
                    {
                      logger.LogError(LogEvents.BackgroundJobs, $"Error while processing court order {domainOrder.CourtOrderHash}. Message: {ex.GetBaseException().Message}");
                      legalEndpointsService.UpdateLastError(notaryTool.LegalEntityEndpointId, ex.GetBaseException().Message);
                    }
                  }
                } while (legalEntityClient.BaseUrl != null);

                legalEndpointsService.UpdateDeltaLink(notaryTool.LegalEntityEndpointId, deltaLink);
              }
              catch (Exception ex)
              {
                logger.LogError(LogEvents.BackgroundJobs, ex, $"Error while reading and processing orders from {notaryTool.BaseUrl}");
                legalEndpointsService.UpdateLastError(notaryTool.LegalEntityEndpointId, ex.GetBaseException().Message);
              }
            }
            if (newCOInserted)
            {
              await StartProcessCourtOrdersAsync();
              await StartProcessCourtOrderAcceptancesAsync();
              await StartProcessConsensusActivationAsync();
            }
          }
          // All exceptions are handled here because background job should continue running, despite failing this time
          catch (Exception ex)
          {
            logger.LogError(LogEvents.BackgroundJobs, ex, "Exception while calling legal endpoints and processing orders");
          }
          await longWait.WaitUntilAsync(delayTime, cancellationToken);
        }
        while (!cancellationToken.IsCancellationRequested);

        if (cancellationToken.IsCancellationRequested)
        {
          logger.LogDebug("Retrieving of court orders from legal endpoints canceled");
          cancellationToken.ThrowIfCancellationRequested();
        }
      });
    }

    public async Task StartProcessCourtOrderAcceptancesAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
      PROCESS_ACCEPTANCES,
      async (cancellationToken, progress, serviceProvider) =>
      {
        var domainAction = serviceProvider.GetRequiredService<IDomainAction>();
        var legalEndpointsService = serviceProvider.GetRequiredService<ILegalEndpoints>();
        var longWait = serviceProvider.GetRequiredService<ILongWait>();
        var courtOrderRepository = serviceProvider.GetRequiredService<ICourtOrderRepository>();
        var bitcoindFactory = serviceProvider.GetRequiredService<IBitcoindFactory>();
        var nodesRepository = serviceProvider.GetRequiredService<INodeRepository>();
        var legalEntityFactory = serviceProvider.GetRequiredService<ILegalEntityFactory>();
        var configParams = serviceProvider.GetRequiredService<IConfigurationParams>();

        var delegatedKeys = serviceProvider.GetRequiredService<IDelegatedKeys>();
        var delayTime = appSettings.BackgroundJobDelayTime;

        bool processingSuccessful = true;

        logger.LogDebug("Court order acceptances processing started");
        do
        {

          try
          {
            // We take first node because it doesn't matter which node we take to get best block hash
            var node = nodesRepository.GetNodes().FirstOrDefault(); 
            if (node == null)
            {
              await longWait.WaitUntilAsync(delayTime, cancellationToken);
              continue;
            }
            var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password, cancellationToken);

            var currentBestBlockHash = await bitcoind.GetBestBlockHashAsync();
            var courtOrders = await courtOrderRepository.GetCourtOrdersToSendAcceptancesAsync();
            var ntEndpoints = await legalEndpointsService.GetLegalEntitiyEndpointsAsync();
            var activeKey = await delegatedKeys.GetActiveKeyForSigningAsync();

            if (activeKey == null)
            {
              await longWait.WaitUntilAsync(delayTime, cancellationToken);
              continue;
            }

            foreach (var courtOrder in courtOrders)
            {
              foreach (var notaryTool in ntEndpoints.Where(x => courtOrder.CourtOrderAcceptances.Any(y => y.LegalEntityEndpointId == x.LegalEntityEndpointId)))
              {
                if (cancellationToken.IsCancellationRequested)
                {
                  processingSuccessful = false;
                  break;
                }

                var courtOrderAcceptance = courtOrder.CourtOrderAcceptances.Single(x => x.LegalEntityEndpointId == notaryTool.LegalEntityEndpointId);

                try
                {
                  var legalEntityClient = legalEntityFactory.Create(notaryTool.BaseUrl, notaryTool.APIKey);

                  var coAcceptance = new CourtOrderAcceptanceViewModel
                  {
                    CreatedAt = DateTime.UtcNow,
                    CourtOrderHash = courtOrder.CourtOrderHash,
                    CurrentBlockHash = currentBestBlockHash,
                    DesiredHashrateAcceptancePercent = await configParams.GetDesiredHashrateAcceptancePercentAsync(),
                    DocumentType = DocumentType.CourtOrderAcceptance
                  };
                  if (activeKey.DelegationRequired)
                  {
                    coAcceptance.DelegatedKeys = activeKey.SignedDelegatedKeyJSON;
                  }

                  var jsonCoAcceptance = System.Text.Json.JsonSerializer.Serialize(coAcceptance, SerializerOptions.SerializeOptions);
                  var wifKey = Encryption.Decrypt(activeKey.DelegatedPrivateKey, appSettings.EncryptionKey);
                  var coAcceptanceJsonEnvelope = SignatureTools.CreateJSonSignature(jsonCoAcceptance, wifKey);

                  await legalEntityClient.PostCourtOrderAcceptanceAsync(coAcceptance.CourtOrderHash, coAcceptanceJsonEnvelope);

                  await courtOrderRepository.SetCourtOrderAcceptanceStatusAsync(courtOrderAcceptance.CourtOrderAcceptanceId, coAcceptanceJsonEnvelope, DateTime.UtcNow, null);
                  logger.LogInformation($"Court order acceptance for '{courtOrder.CourtOrderHash}' and '{notaryTool.BaseUrl}' processed successfully");
                }
                catch (Exception ex)
                {
                  processingSuccessful = false;
                  logger.LogError(LogEvents.BackgroundJobs, ex,
                    $"Court order acceptance for '{courtOrder.CourtOrderHash}' and '{notaryTool.BaseUrl}' aborted with exception");
                  await courtOrderRepository.SetCourtOrderAcceptanceStatusAsync(courtOrderAcceptance.CourtOrderAcceptanceId, null, null, ex.GetBaseException().ToString());
                  legalEndpointsService.UpdateLastError(notaryTool.LegalEntityEndpointId, ex.GetBaseException().Message);
                }
              }
            }
          }
          // All exceptions are handled here because background job should continue running, despite failing this time
          catch (Exception ex)
          {
            processingSuccessful = false;
            logger.LogError(LogEvents.BackgroundJobs, ex, $"Error while processing court order acceptances.");
          }

          if (!processingSuccessful)
          {
            await longWait.WaitUntilAsync(delayTime, cancellationToken);
          }

        } while (!processingSuccessful && !cancellationToken.IsCancellationRequested);

        if (cancellationToken.IsCancellationRequested)
        {
          logger.LogDebug("Processing of court order acceptances canceled");
          cancellationToken.ThrowIfCancellationRequested();
        }
        logger.LogDebug("Processing of court order acceptances ended");
      });
    }
  }
}
