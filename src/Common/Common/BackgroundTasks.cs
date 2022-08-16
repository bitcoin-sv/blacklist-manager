// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{

  /// <summary>
  /// Class that makes sure that exactly 1 task from a task group runs at the same time 
  /// </summary>
  public class BackgroundTasks
  {
    private readonly IServiceProvider serviceProvider;
    private readonly ILogger<BackgroundTask> logger;

    public event Action<string, Task> TaskCreated;

    public BackgroundTasks(IServiceProvider serviceProvider, ILogger<BackgroundTask> logger)
    {
      this.serviceProvider = serviceProvider;
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected object objLock = new object();

    // When task is completed it is kept in the group, but we return null as its progress,
    // So this implementation detail is not observable to external users.
    protected readonly Dictionary<string, BackgroundTask> groups = new Dictionary<string, BackgroundTask>();

    bool stoppingOrStopped;

    /// <summary>
    ///  Terminate old task from same group (if there are any) and start a new one
    /// </summary>
    public async Task CancelOldAndStartNewTaskAsync(string groupKey, Func<CancellationToken, IProgress<long>, IServiceProvider, Task> action)
    {
      BackgroundTask group;
      lock (objLock)
      {
        if (stoppingOrStopped)
        {
          throw new InvalidOperationException("Can not start new tasks - already stopping or stopped");
        }
        groups.TryGetValue(groupKey, out group);
      }
      if (group != null)
      {
        await group.CancelTaskAsync();
      }

      lock (objLock)
      {
        if (groups.TryGetValue(groupKey, out group))
        {
          groups.Remove(groupKey);
        }

        group = new BackgroundTask(groupKey, action, serviceProvider, logger);
        groups[groupKey] = group;
        TaskCreated?.Invoke(groupKey, group.Task);
      }
    }

    public async Task CancelTaskAsync(string groupKey)
    {
      BackgroundTask group;
      lock (objLock)
      {
        if (stoppingOrStopped)
        {
          throw new InvalidOperationException("Can not start new tasks - already stopping or stopped");
        }
        groups.TryGetValue(groupKey, out group);
      }
      if (group != null)
      {
        await group.CancelTaskAsync();
      }

      lock (objLock)
      {
        if (groups.TryGetValue(groupKey, out group))
        {
          groups.Remove(groupKey);
        }
      }
    }

    /// <summary>
    /// Return current progress of task group or null if group is not executing
    /// </summary>
    public long? GetProgress(string groupKey)
    {
      lock (objLock)
      {
        if (!stoppingOrStopped && groups.TryGetValue(groupKey, out var group))
        {
          return group.ProgressCounter;
        }
        return null;
      }
    }

    public string[] GetRunningGroups()
    {
      lock (objLock)
      {
        if (stoppingOrStopped)
        {
          return new string[0];
        }
        return groups.Where(kv => kv.Value.ProgressCounter != null).Select(kv => kv.Key).ToArray();
      }
    }

    public async Task StopAllAsync()
    {
      logger.LogDebug("Stopping all task groups");
      lock (objLock)
      {
        stoppingOrStopped = true;
      }
      // to prevent deadlocks this part of code must not be under lock statement
      //  - backgroundtask A is running and trying to start new backgroundtask B just before StopAll was called
      foreach (var kv in groups)
      {
        await kv.Value.CancelTaskAsync();
      }
      logger.LogDebug("Stopping completed");
    }
  }
}
