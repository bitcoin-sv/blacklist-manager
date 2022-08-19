// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Common
{
  /// <summary>
  /// Internal class that holds a state for single group of tasks. Works together with BackgroundTasks
  /// </summary>
  public class BackgroundTask
  {
    readonly string groupKey;
    Task runningTask;
    CancellationTokenSource groupCancellationSource;
    readonly object objLock = new object();
    long? progressCounter;
    readonly ILogger<BackgroundTask> logger;
    readonly IServiceProvider serviceProvider;
    readonly Func<CancellationToken, IProgress<long>, IServiceProvider, Task> action;
    
    public Task Task => runningTask;
    public BackgroundTaskStatus Status { get; set; }

    public BackgroundTask(
      string groupKey, 
      Func<CancellationToken, IProgress<long>, IServiceProvider, Task> action, 
      IServiceProvider serviceProvider, 
      ILogger<BackgroundTask> logger)
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.action = action ?? throw new ArgumentNullException(nameof(action));
      this.groupKey = groupKey;
      this.serviceProvider = serviceProvider;
      this.groupCancellationSource = new CancellationTokenSource();
      ProgressCounter = 0; // Set initial value to null to differentiate from null value which means that task is no longer running
      Initialize();
    }

    private void Initialize()
    {
      Status = BackgroundTaskStatus.Starting;
      var progress = new Progress<long>(myTaskNum => { ProgressCounter = myTaskNum; });
      logger.LogDebug($"Starting new task for group {groupKey} ");
      runningTask = Task.Run(async () =>
      {

        try
        {
          Status = BackgroundTaskStatus.Started;
          if (serviceProvider == null)
          {
            await action(groupCancellationSource.Token, progress, null);
          }
          else
          {
            using var serviceScope = serviceProvider.CreateScope();
            await action(groupCancellationSource.Token, progress, serviceScope.ServiceProvider);
          }
          logger.LogDebug($"Starting for group {groupKey} ended without exceptions");
          Status = BackgroundTaskStatus.Finished;
          return;
        }
        catch (OperationCanceledException)
        {
          // Just rethrow OperationCanceledException and abort execution no need do anything else here
          throw;
        }
        catch (Exception ex)
        {
          // Catch exceptions to prevent host from crashing
          logger.LogError($"Executing tasks group {groupKey} threw an exception " + ex);
        }
        Status = BackgroundTaskStatus.FinishedWithError;
      }, groupCancellationSource.Token);
    }

    public long? ProgressCounter
    {
      get
      {
        if (runningTask.IsCompleted)
        {
          return null; // return null if task was already completed
        }

        lock (objLock)
        {
          return progressCounter;
        }
      }
      private set
      {
        lock (objLock)
        {
          progressCounter = value;
        }
      }
    }

    /// <summary>
    /// Blocks until the task is canceled
    /// </summary>
    public async Task CancelTaskAsync() 
    {
      Status = BackgroundTaskStatus.Cancelling;
      logger.LogDebug($"Requesting cancel for group {groupKey}");

      lock (objLock)
      {
        groupCancellationSource?.Cancel();
      }

      try
      {
        await runningTask;
        logger.LogDebug($"Task for group {groupKey} finished without cancellation");
      }
      catch (OperationCanceledException)
      {
        logger.LogDebug($"Task for group {groupKey} was canceled");
      }
      catch (Exception ex)
      {
        logger.LogDebug($"Task for group {groupKey} threw an exception {ex}");
      }
      finally
      {
        lock (objLock)
        {
          groupCancellationSource?.Dispose();
          groupCancellationSource = null;
        }
        Status = BackgroundTaskStatus.Cancelled;
      }
    }
  }
}