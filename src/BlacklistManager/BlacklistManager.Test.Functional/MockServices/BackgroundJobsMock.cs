// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using BlacklistManager.Domain.BackgroundJobs;
using Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class BackgroundJobsMock : BackgroundJobs
  {
    public BackgroundJobsMock(
      IServiceProvider serviceProvider, 
      ILogger<BackgroundTask> bgtLogger, 
      ILoggerFactory logger,
      IOptions<AppSettings> options) 
      : base(serviceProvider, bgtLogger, logger, options)
    {
      this.logger = logger.CreateLogger(TestBase.LOG_CATEGORY);
      Tasks = new TaskList();
      RetryDelayOverride = 100;
      backgroundTasks.TaskCreated += BackgroundTasks_TaskCreated;
    }

    public TaskList Tasks = null;
    public int RetryDelayOverride = 100;

    private readonly ILogger logger;
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
        allStart = Tasks.All.Count;
        logger.LogDebug("Waiting for all background jobs to end");
        await WaitForCourtOrderProcessingAsync();
        await WaitForPropagationAsync();
        await WaitForCourtOrderAcceptanceAsync();
        await WaitForConsensusActivationAsync();
        allEnd = Tasks.All.Count;
        if (allEnd > allStart)
        {
          logger.LogDebug("While waiting new background jobs were started");
        }
      } while (allEnd > allStart);
      logger.LogDebug("All background jobs ended");
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
        await WaitGenericAsync(Tasks.ProcessConsensusActivations.Last(), BackgroundJobs.PROCESS_CONSENSUS_ACTIVATION);
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
      var cts = new CancellationTokenSource(WAIT_TIMEOUT);
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
      public void AssertEqualTo(params string[] expected)
      {
        Assert.AreEqual(string.Join(Environment.NewLine, expected), ToString());
      }

      public override string ToString()
      {
        var sb = new StringBuilder();
        foreach (var task in items)
        {
          sb.AppendLine(task.ToString());
        }

        return sb.ToString().Trim();
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
        .Where(i => i.Key == BackgroundJobs.PROCESS_CONSENSUS_ACTIVATION)
        .Select(i => i.Task)
        .ToList();

      public IReadOnlyList<Task> ProcessCourtOrderAcceptances => items
        .Where(i => i.Key == BackgroundJobs.PROCESS_ACCEPTANCES)
        .Select(i => i.Task)
        .ToList();
    }
  }
}
