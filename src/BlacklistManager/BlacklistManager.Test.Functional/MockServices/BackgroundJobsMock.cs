// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using BlacklistManager.Infrastructure.BackgroundJobs;
using Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class BackgroundJobsMock : BackgroundJobs
  {
    public BackgroundJobsMock(
      IServiceProvider serviceProvider, 
      ILogger<BackgroundTask> bgtLogger,
      ILogger<BackgroundJobs> logger,
      IOptions<AppSettings> options) 
      : base(serviceProvider, bgtLogger, logger, options)
    {
      this._logger = logger;
      Tasks = new TaskList();
      _backgroundTasks.TaskCreated += BackgroundTasks_TaskCreated;
    }

    public TaskList Tasks { get; set; }
    public int RetryDelayOverride { get; set; } = 100;

    private readonly ILogger _logger;
    protected override int OnErrorRetryDelay => RetryDelayOverride;
    protected override int ConsensusActivationRetryDelay => RetryDelayOverride;

    private void BackgroundTasks_TaskCreated(string groupKey, Task task)
    {
      Tasks?.Add(groupKey, task);
    }

    private const int WAIT_TIMEOUT = 15000;

    public async Task WaitAllAsync()
    {
      int allEnd, allStart;
      do
      {
        List<Task> taskList = new List<Task>();
        allStart = Tasks.All.Count;
        _logger.LogDebug("Waiting for all background jobs to end");
        taskList.Add(WaitForCourtOrderProcessingAsync());
        taskList.Add(WaitForPropagationAsync());
        taskList.Add(WaitForCourtOrderAcceptanceAsync());
        taskList.Add(WaitForConsensusActivationAsync());
        //taskList.Add(WaitForConfiscationTxsAsync());

        await Task.WhenAll(taskList);
        allEnd = Tasks.All.Count;
        if (allEnd > allStart)
        {
          _logger.LogDebug("While waiting new background jobs were started");
        }
      } while (allEnd > allStart);
      _logger.LogDebug("All background jobs ended");
    }

    public async Task WaitForCourtOrderProcessingAsync()
    {
      if (Tasks.ProcessCourtOrders.Any())
      {
        await WaitGenericAsync(Tasks.ProcessCourtOrders.Last(), BackgroundJobs.PROCESS_COURTORDERS);
      }
    }    

    public async Task WaitForPropagationAsync()
    {
      if (Tasks.PropagateFunds.Any())
      {
        await WaitGenericAsync(Tasks.PropagateFunds.Last(), BackgroundJobs.PROPAGATE_FUNDS);
      }
    }

    public async Task WaitForConsensusActivationAsync()
    {
      if (Tasks.ProcessConsensusActivations.Any())
      {
        await WaitGenericAsync(Tasks.ProcessConsensusActivations.Last(), BackgroundJobs.DOWNLOAD_CONSENSUS_ACTIVATION);
      }
    }

    public async Task WaitForCourtOrderAcceptanceAsync()
    {
      if (Tasks.ProcessCourtOrderAcceptances.Any())
      {
        await WaitGenericAsync(Tasks.ProcessCourtOrderAcceptances.Last(), BackgroundJobs.PROCESS_ACCEPTANCES);
      }
    }

    private async Task WaitGenericAsync(Task generic, string group)
    {
      bool finished = false;
      if (await Task.WhenAny(generic, Task.Delay(WAIT_TIMEOUT)) == generic)
      {
        finished = true;
      }
      Assert.IsTrue(finished, $"Background task for '{group}' expected to finish");
    }

    public class TaskList
    {
      private readonly List<TaskListItem> items = new List<TaskListItem>();

      public void Add(string key, Task task)
      {
        items.Add(new TaskListItem() { Key = key, Task = task });
      }

      public class TaskListItem
      {
        public string Key { get; set; }
        public Task Task { get; set; }

        public override string ToString()
        {
          return $"{Key}/{Task.Status}";
        }
      }

      public IReadOnlyList<Task> All => items
        .Select(i => i.Task)
        .ToList();

      public IReadOnlyList<Task> PropagateFunds => items
        .Where(i => i.Key == BackgroundJobs.PROPAGATE_FUNDS)
        .Select(i => i.Task)
        .ToList();

      public IReadOnlyList<Task> ProcessCourtOrders => items
        .Where(i => i.Key == BackgroundJobs.PROCESS_COURTORDERS)
        .Select(i => i.Task)
        .ToList();

      public IReadOnlyList<Task> ProcessConsensusActivations => items
        .Where(i => i.Key == BackgroundJobs.DOWNLOAD_CONSENSUS_ACTIVATION)
        .Select(i => i.Task)
        .ToList();

      public IReadOnlyList<Task> ProcessCourtOrderAcceptances => items
        .Where(i => i.Key == BackgroundJobs.PROCESS_ACCEPTANCES)
        .Select(i => i.Task)
        .ToList();
    }
  }
}
