// Copyright (c) 2020 Bitcoin Association

using Common;
using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using BlacklistManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using BlacklistManager.Domain.Actions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain;

namespace BlacklistManager.Infrastructure.BackgroundJobs
{
  public class BackgroundJobs : IBackgroundJobs
  {
    protected readonly BackgroundTasks _backgroundTasks;
    private readonly ILogger<BackgroundJobs> _logger;
    private readonly AppSettings _appSettings;
    private bool _offlineMode;

    public BackgroundJobs(
      IServiceProvider serviceProvider,
      ILogger<BackgroundTask> bgtLogger,
      ILogger<BackgroundJobs> logger,
      IOptions<AppSettings> options)
    {
      _backgroundTasks = new BackgroundTasks(serviceProvider, bgtLogger);
      _logger = logger;
      _appSettings = options.Value;
    }

    public const string PROPAGATE_FUNDS = "fundPropagation";
    public const string PROCESS_COURTORDERS = "courtOrderProcessing";
    public const string DOWNLOAD_CONSENSUS_ACTIVATION = "consensusActivationDownload";
    public const string PROCESS_CONSENSUS_ACTIVATIONS = "consensusActivationProcessing";
    public const string DOWNLOAD_COURTORDERS = "courtOrderDownload";
    public const string PROCESS_ACCEPTANCES = "courtOrderAcceptanceProcessing";
    public const string PROCESS_CONFISCATIONS = "confiscationTxsProcessing";
    public const string SUBMIT_WHITELIST = "submitWhitelist";
    public const string CONFISCATION_TX_BLOCK_CHECK = "confiscationsInBlockCheck";
    public const string PROCESS_FAILED_ORDERS = "processFailedOrders";

    /// <summary>
    /// Defines delay in millisecond after job is successful
    /// </summary>
    protected virtual int BackgroundJobDelay => _appSettings.BackgroundJobDelayTime * 1000;

    /// <summary>
    /// Defines delay in millisecond for job retry after error
    /// </summary>
    protected virtual int OnErrorRetryDelay => _appSettings.OnErrorRetryDelayTime * 1000;

    /// <summary>
    /// Defines delay in millisecond between consensus activation checks
    /// </summary>
    protected virtual int ConsensusActivationRetryDelay => _appSettings.ConsensusActivationRetryDelayTime * 1000;

    public virtual BackgroundTasks BackgroundTasks => _backgroundTasks;

    public bool OfflineMode => _offlineMode;

    public async Task SetOfflineModeAsync(bool offline)
    {
      if (!_offlineMode && offline)
      {
        _logger.LogInformation("Initiating off-line mode. Stopping background jobs");
        await Task.WhenAll(BackgroundTasks.CancelTaskAsync(DOWNLOAD_COURTORDERS),
                           BackgroundTasks.CancelTaskAsync(DOWNLOAD_CONSENSUS_ACTIVATION),
                           BackgroundTasks.CancelTaskAsync(PROCESS_COURTORDERS),
                           BackgroundTasks.CancelTaskAsync(PROCESS_ACCEPTANCES),
                           BackgroundTasks.CancelTaskAsync(PROCESS_CONSENSUS_ACTIVATIONS),
                           BackgroundTasks.CancelTaskAsync(PROPAGATE_FUNDS)
                           );
        _logger.LogInformation("All background jobs stopped.");
      }
      else if (_offlineMode && !offline)
      {
        _logger.LogInformation("Resuming off-line mode. Starting background jobs for court order and consensus activation download.");
        await Task.WhenAll(StartGetCourtOrdersAsync(),
                           StartGetConsensusActivationsAsync());
        _logger.LogInformation("Background jobs started.");

      }
      _offlineMode = offline;
    }

    public void CheckForOfflineMode()
    {
      if (!_offlineMode)
      {
        throw new BadRequestException("Application must be in off-line mode to perform this action.");
      }
    }

    public Task StopAllAsync()
    {
      return BackgroundTasks.StopAllAsync();
    }

    public async Task StartAllAsync()
    {
      await Task.WhenAll(
      StartGetCourtOrdersAsync(),
      StartProcessCourtOrderAcceptancesAsync(),
      StartProcessCourtOrdersAsync(),
      StartGetConsensusActivationsAsync(),
      StartProcessConsensusActivationsAsync(),
      StartPropagateFundsStatesAsync(),
      StartSubmitWhitelistTxIdsAsync(),
      StartSendConfiscationTransactionsAsync(),
      StartConfiscationsInBlockCheckAsync());
    }

    public async Task StartPropagateFundsStatesAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
        PROPAGATE_FUNDS,
        async (cancellationToken, progress, serviceProvider) =>
        {
          _logger.LogDebug("Funds states propagation started");
          var fundPropagator = serviceProvider.GetRequiredService<IFundPropagator>();
          var propagationEvents = serviceProvider.GetService<IPropagationEvents>();

          bool propagationSuccessful = false;
          do
          {
            try
            {
              if (cancellationToken.IsCancellationRequested)
              {
                break;
              }
              var propagation = await fundPropagator.PropagateFundsStateAsync(cancellationToken);
              propagationSuccessful = propagation.WasSuccessful;
              propagationEvents?.Finished(propagationSuccessful);

              if (!propagationSuccessful)
              {
                _logger.LogWarning($"Funds states propagation did not succeed. Will retry in {OnErrorRetryDelay}ms");
                await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
              }
            }
            // All exceptions are handled here because background job should continue running, despite failing this time
            catch (Exception ex)
            {
              _logger.LogError(LogEvents.BackgroundJobs, ex, $"Funds states propagation aborted with error. Will retry in {OnErrorRetryDelay}ms");
              await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
            }
          } while (!propagationSuccessful && !cancellationToken.IsCancellationRequested);

          if (cancellationToken.IsCancellationRequested)
          {
            _logger.LogDebug("Funds states propagation canceled");
            cancellationToken.ThrowIfCancellationRequested();
          }
          _logger.LogDebug("Funds states propagation ended");
        });
    }

    public async Task StartProcessCourtOrdersAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
        PROCESS_COURTORDERS,
        async (cancellationToken, progress, serviceProvider) =>
        {
          _logger.LogDebug("Court order processing  started");
          var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();

          bool processingSuccessful = false;
          do
          {
            try
            {
              var activation = await courtOrders.ActivateCourtOrdersAsync(cancellationToken);
              processingSuccessful = activation.WasSuccessful;

              if (cancellationToken.IsCancellationRequested)
              {
                break;
              }

              if (!processingSuccessful)
              {
                _logger.LogWarning($"Court order processing did not succeed. Will retry in {OnErrorRetryDelay}ms");
                await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
              }
              else if (activation.ActivatedCourtOrders.Any())
              {
                await StartPropagateFundsStatesAsync();
              }
            }
            // All exceptions are handled here because background job should continue running, despite failing this time
            catch (Exception ex)
            {
              _logger.LogError(LogEvents.BackgroundJobs, ex, $"Court order processing aborted with error. Will retry in {OnErrorRetryDelay}ms");
              await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
            }
          } while (!processingSuccessful && !cancellationToken.IsCancellationRequested);

          if (cancellationToken.IsCancellationRequested)
          {
            _logger.LogDebug("Court order processing canceled");
            cancellationToken.ThrowIfCancellationRequested();
          }
          _logger.LogDebug("Court order processing ended");
        });
    }

    public async Task StartGetConsensusActivationsAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
        DOWNLOAD_CONSENSUS_ACTIVATION,
        async (cancellationToken, progress, serviceProvider) =>
        {
          _logger.LogDebug("Consensus activation processing  started");
          var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();
          
          bool processingDone = false;
          do
          {
            var result = await courtOrders.GetConsensusActivationsAsync(cancellationToken);
            processingDone = !result.AnyConsensusActivationsStillPending;

            if (cancellationToken.IsCancellationRequested)
            {
              break;
            }

            if (result.WasSuccessful && result.Processed > 0)
            {
              await StartProcessConsensusActivationsAsync();
            }
            if (!result.WasSuccessful)
            {
              _logger.LogWarning($"Consensus activation processing did not succeed. Will retry in {OnErrorRetryDelay}ms");
              await new LongWait().WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
            }
            else if (!processingDone)
            {
              _logger.LogWarning($"Pending consensus activations exists. Will retry in {ConsensusActivationRetryDelay}ms");
              await new LongWait().WaitUntilAsync(ConsensusActivationRetryDelay, cancellationToken);
            }
          } while (!cancellationToken.IsCancellationRequested);

          if (cancellationToken.IsCancellationRequested)
          {
            _logger.LogDebug("Consensus activation processing canceled");
            cancellationToken.ThrowIfCancellationRequested();
          }
          _logger.LogDebug("Consensus activation processing ended");
        });
    }

    public async Task StartProcessConsensusActivationsAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
        PROCESS_CONSENSUS_ACTIVATIONS,
        async (cancellationToken, progress, serviceProvider) =>
        {
          _logger.LogDebug("Consensus activation processing  started");
          var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();

          bool processingSuccessfull = false;
          do
          {
            var result = await courtOrders.ActivateConsensusActivationsAsync(cancellationToken);
            processingSuccessfull = result.WasSuccessful;

            if (cancellationToken.IsCancellationRequested)
            {
              break;
            }

            if (!processingSuccessfull)
            {
              _logger.LogWarning($"Consensus activations waiting to be processed still exist. Will retry in {ConsensusActivationRetryDelay}ms");
              await new LongWait().WaitUntilAsync(ConsensusActivationRetryDelay, cancellationToken);
            }
            else if (result.ConsensusActivations.Any())
            {
              await Task.WhenAll(StartSubmitWhitelistTxIdsAsync(),
                                 StartPropagateFundsStatesAsync());
            }
          } while (!processingSuccessfull && !cancellationToken.IsCancellationRequested);

          if (cancellationToken.IsCancellationRequested)
          {
            _logger.LogDebug("Consensus activation processing canceled");
            cancellationToken.ThrowIfCancellationRequested();
          }
          _logger.LogDebug("Consensus activation processing ended");
        });
    }

    public async Task StartGetCourtOrdersAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
      DOWNLOAD_COURTORDERS,
      async (cancellationToken, progress, serviceProvider) =>
      {
        _logger.LogDebug("Retrieving of court orders from legal endpoints starting");
        var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();
        var legalEndpointsService = serviceProvider.GetRequiredService<ILegalEndpoints>();
        var longWait = serviceProvider.GetRequiredService<ILongWait>();

        do
        {
          try
          {
            var retryErrorsTask = courtOrders.CheckAndResendProcessingErrorsToLEsAsync(cancellationToken);
            var ntEndpoints = await legalEndpointsService.GetAsync();

            await courtOrders.ProcessGetCourtOrdersAsync(ntEndpoints, cancellationToken);

            await Task.WhenAll(
              StartProcessCourtOrdersAsync(),
              StartProcessCourtOrderAcceptancesAsync());

            // TODO: Consider if this should be moved to a separate job
            await retryErrorsTask;
          }
          // All exceptions are handled here because background job should continue running, despite failing this time
          catch (Exception ex)
          {
            _logger.LogError(LogEvents.BackgroundJobs, ex, "Exception while calling legal endpoints and processing orders");
          }
          await longWait.WaitUntilAsync(BackgroundJobDelay, cancellationToken);
        }
        while (!cancellationToken.IsCancellationRequested);

        if (cancellationToken.IsCancellationRequested)
        {
          _logger.LogDebug("Retrieving of court orders from legal endpoints canceled");
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
        _logger.LogDebug("Court order acceptances processing started");
        var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();
        var longWait = serviceProvider.GetRequiredService<ILongWait>();
        var nodesRepository = serviceProvider.GetRequiredService<INodeRepository>();
        bool processingSuccessful = true;

        do
        {
          int processedCourtOrderCount = 0;
          try
          {
            var nodes = (await nodesRepository.GetNodesAsync()).ToArray();
            if (!nodes.Any())
            {
              await longWait.WaitUntilAsync(BackgroundJobDelay, cancellationToken);
              continue;
            }

            HelperTools.ShuffleArray(nodes);
            (processingSuccessful, processedCourtOrderCount) = await courtOrders.ProcessCourtOrderAcceptancesAsync(nodes.FirstOrDefault(), cancellationToken);
          }
          // All exceptions are handled here because background job should continue running, despite failing this time
          catch (Exception ex)
          {
            processingSuccessful = false;
            _logger.LogError(LogEvents.BackgroundJobs, ex, $"Error while processing court order acceptances.");
          }


          if (!processingSuccessful)
          {
            await longWait.WaitUntilAsync(OnErrorRetryDelay, cancellationToken);
          }
        } while (!processingSuccessful && !cancellationToken.IsCancellationRequested);

        if (cancellationToken.IsCancellationRequested)
        {
          _logger.LogDebug("Processing of court order acceptances canceled");
          cancellationToken.ThrowIfCancellationRequested();
        }
        _logger.LogDebug("Processing of court order acceptances ended");
      });
    }

    public async Task StartSendConfiscationTransactionsAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
      PROCESS_CONFISCATIONS,
      async (cancellationToken, progress, serviceProvider) =>
      {
        _logger.LogDebug("Send confiscation transactions processing started");
        var confiscationTxProcessing = serviceProvider.GetRequiredService<IConfiscationTxProcessing>();
        var nodesRepository = serviceProvider.GetRequiredService<INodeRepository>();
        var longWait = serviceProvider.GetRequiredService<ILongWait>();

        do
        {
          try
          {
            var nodes = (await nodesRepository.GetNodesAsync()).ToArray();
            if (nodes.Any())
            {
              HelperTools.ShuffleArray(nodes);
              var node = nodes.First();
              await confiscationTxProcessing.SendConfiscationTransactionsAsync(node, cancellationToken);
            }
          }
          // All exceptions are handled here because background job should continue running, despite failing this time
          catch (Exception ex)
          {
            _logger.LogError(LogEvents.BackgroundJobs, ex, "Exception while sending transaction to bitcoin node.");
          }

          await longWait.WaitUntilAsync(BackgroundJobDelay, cancellationToken);
        } 
        while (!cancellationToken.IsCancellationRequested);

        if (cancellationToken.IsCancellationRequested)
        {
          _logger.LogDebug("Sending of confiscation transactions canceled");
          cancellationToken.ThrowIfCancellationRequested();
        }
        _logger.LogDebug("Sending of confiscation transactions ended");

      });
    }

    public async Task StartSubmitWhitelistTxIdsAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
      SUBMIT_WHITELIST,
      async (cancellationToken, progress, serviceProvider) =>
      {
        _logger.LogDebug("Submitting of whitelisted transaction ids started");
        var confiscationTxProcessing = serviceProvider.GetRequiredService<IConfiscationTxProcessing>();
        var nodesRepository = serviceProvider.GetRequiredService<INodeRepository>();
        var longWait = serviceProvider.GetRequiredService<ILongWait>();

        bool processingSuccessful = true;
          
        do
        {
          try
          {
            var nodes = (await nodesRepository.GetNodesAsync()).ToArray();
            if (!nodes.Any())
            {
              await longWait.WaitUntilAsync(BackgroundJobDelay, cancellationToken);
              continue;
            }

            HelperTools.ShuffleArray(nodes);
            processingSuccessful = await confiscationTxProcessing.SubmitWhitelistTxIdsAsync(nodes, cancellationToken);
          }
          catch (Exception ex)
          {
            processingSuccessful = false;
            _logger.LogError(LogEvents.BackgroundJobs, ex, "Exception while submitting transaction id whitelist to bitcoin node.");
          }
          var delay = processingSuccessful ? BackgroundJobDelay : OnErrorRetryDelay;
          await longWait.WaitUntilAsync(delay, cancellationToken);

        } while (!processingSuccessful && !cancellationToken.IsCancellationRequested);

        if (cancellationToken.IsCancellationRequested)
        {
          _logger.LogDebug("Submit of whitelisted transaction ids canceled");
          cancellationToken.ThrowIfCancellationRequested();
        }
        _logger.LogDebug("Submit of whitelisted transaction ids ended");
      });
    }

    public async Task StartConfiscationsInBlockCheckAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
      CONFISCATION_TX_BLOCK_CHECK,
      async (cancellationToken, progress, serviceProvider) =>
      {
        _logger.LogDebug("Started checking blocks for reorg");
        var longWait = serviceProvider.GetRequiredService<ILongWait>();
        var confiscationTxProcessingcourtOrders = serviceProvider.GetRequiredService<IConfiscationTxProcessing>();
        var nodesRepository = serviceProvider.GetRequiredService<INodeRepository>();

        bool processingSuccessful = true;

        do
        {
          try
          {
            var nodes = (await nodesRepository.GetNodesAsync()).ToArray();
            if (!nodes.Any())
            {
              await longWait.WaitUntilAsync(BackgroundJobDelay, cancellationToken);
              continue;
            }

            HelperTools.ShuffleArray(nodes);
            processingSuccessful = await confiscationTxProcessingcourtOrders.ConfiscationsInBlockCheckAsync(nodes.FirstOrDefault(), cancellationToken);
          }
          catch (Exception ex)
          {
            processingSuccessful = false;
            _logger.LogError(LogEvents.BackgroundJobs, ex, "Exception while checking if reorg occurred.");
          }

          var delay = processingSuccessful ? BackgroundJobDelay : OnErrorRetryDelay;
          await longWait.WaitUntilAsync(delay, cancellationToken);
        } while (!cancellationToken.IsCancellationRequested);

        if (cancellationToken.IsCancellationRequested)
        {
          _logger.LogDebug("Block checking for reorg canceled");
          cancellationToken.ThrowIfCancellationRequested();
        }
        _logger.LogDebug("Block checking for reorg ended");
      });
    }

    public async Task StartFailedCourtOrdersProcessingAsync()
    {
      await BackgroundTasks.CancelOldAndStartNewTaskAsync(
      PROCESS_FAILED_ORDERS,
      async (cancellationToken, progress, serviceProvider) =>
      {
        _logger.LogDebug("Started processing for failed orders");

        var courtOrders = serviceProvider.GetRequiredService<ICourtOrders>();

        try
        {
          var success = await courtOrders.ProcessFailedCourtOrdersAsync(cancellationToken);

          if (success)
          {
            await Task.WhenAll(
              StartProcessCourtOrdersAsync(),
              StartProcessCourtOrderAcceptancesAsync());
          }
        }
        catch (OperationCanceledException)
        {
          _logger.LogDebug("Processing for failed orders canceled");
          cancellationToken.ThrowIfCancellationRequested();
        }

        _logger.LogDebug("Processing for failed orders ended");
      });
    }
  }
}
