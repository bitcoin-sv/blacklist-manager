// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Common.Test.Unit
{
  [TestClass]
  public class BackGroundTasksTest
  {
    public ILogger<BackgroundTask> Logger;

    public BackGroundTasksTest()
    {
      var hostBuilder = Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                  logging.AddSimpleConsole((options) => 
                  { 
                    options.IncludeScopes = true; 
                    options.TimestampFormat = "HH:mm:ss:fff ";
                    options.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Enabled;
                  });
                });

      var serviceProvider = hostBuilder.Build().Services;

      Logger = (ILogger<BackgroundTask>)serviceProvider.GetService(typeof(ILogger<BackgroundTask>));

    }

    static void WaitUntil(Func<bool> predicate)
    {
      for (int i = 0; i < 10; i++)
      {
        if (predicate())
        {
          return;
        }

        Thread.Sleep(100);
      }

      throw new Exception("Timeout - WaitUntil did not complete in allocated time");
    }

    [TestMethod]
    public async Task ShecduledTasksShouldBeExecutedAsync()
    {
      var bt = new BackgroundTasks(null, Logger);

      bool exectued = false;
      await bt.CancelOldAndStartNewTaskAsync("group1",
         (token, progress, serviceProvider) => { exectued = true; return Task.CompletedTask; }
      );

      WaitUntil(() => exectued == true);
    }

    [TestMethod]
    public async Task ProgressShouldBeReportedAsync()
    {
      var bt = new BackgroundTasks(null, Logger);
      AutoResetEvent waitSync = new AutoResetEvent(false);
      AutoResetEvent waitSync2 = new AutoResetEvent(false);

      await bt.CancelOldAndStartNewTaskAsync("group1",
        (token, progress, serviceProvider) =>
        {
          progress.Report(123);
          waitSync.Set();
          waitSync2.WaitOne();
          return Task.CompletedTask;
        }
      );

      waitSync.WaitOne();
      WaitUntil(() => bt.GetProgress("group1") == 123);
      waitSync2.Set();
      WaitUntil(() => bt.GetProgress("group1") == null); // when task is finished it should be reset to null
    }

    [TestMethod]
    public async Task SubmitingNewTaskShouldCancelOldOneWithSameGroupAsync()
    {

      var bt = new BackgroundTasks(null, Logger);

      bool task1Started = false, task1Finished = false;

      await bt.CancelOldAndStartNewTaskAsync("group1",
        async (token, progress, serviceProvider) =>
        {
          task1Started = true;
          await Task.Delay(200, token);
          token.ThrowIfCancellationRequested();
          task1Finished = true;
        }
      );


      bool taskGroup2Started = false, taskGroup2Finished = false;
      await bt.CancelOldAndStartNewTaskAsync("group2",
        async (token, progress, serviceProvider) =>
        {
          taskGroup2Started = true;
          await Task.Delay(200, token);
          token.ThrowIfCancellationRequested();
          taskGroup2Finished = true;
        }
      );


      WaitUntil(() => task1Started); // wait until it is started

      WaitUntil(() => taskGroup2Started); // wait until task from group2 has started

      bool task2Started = false;
      // submit a new one
      await bt.CancelOldAndStartNewTaskAsync("group1",
        async (token, progress, serviceProvider) =>
        {
          task2Started = true;
          await Task.Delay(200, token);
        }
      );

      WaitUntil(() => task2Started);
      Assert.IsFalse(task1Finished); // task1 was canceled

      WaitUntil(() => taskGroup2Finished); // But task from group 2 run to the end

    }

    [TestMethod]
    public async Task StopAllShouldStopAllTasksAsync()
    {

      var bt = new BackgroundTasks(null, Logger);

      bool task1Started = false, task1Finished = false;

      await bt.CancelOldAndStartNewTaskAsync("group1",
        async (token, progress, serviceProvider) =>
        {
          task1Started = true;
          await Task.Delay(200, token);
          token.ThrowIfCancellationRequested();
          task1Finished = true;
        }
      );


      bool taskGroup2Started = false, taskGroup2Finished = false;
      await bt.CancelOldAndStartNewTaskAsync("group2",
        async (token, progress, serviceProvider) =>
        {
          taskGroup2Started = true;
          await Task.Delay(2000, token);
          token.ThrowIfCancellationRequested();
          taskGroup2Finished = true;
        }
      );

      WaitUntil(() => task1Started );
      WaitUntil(() => taskGroup2Started);


      CollectionAssert.AreEquivalent(new [] {"group1", "group2" }, bt.GetRunningTasks());
      WaitUntil(() => task1Finished);

      CollectionAssert.AreEquivalent(new[] { "group2" }, bt.GetRunningTasks());

      await bt.StopAllAsync();
      Assert.AreEqual(0, bt.GetRunningTasks().Length);

      Assert.IsTrue(task1Finished);
      Assert.IsFalse(taskGroup2Finished);
    }


    [TestMethod]
    public async Task TasksThatThrowsShouldNotCrashProcessAsync()
    {

      var bt = new BackgroundTasks(null, Logger);

      bool task1Started = false;

      await bt.CancelOldAndStartNewTaskAsync("group1",
        async (token, progress, serviceProvider) =>
        {
          task1Started = true;
          await Task.Delay(200, token);
          throw new Exception("The sky is falling!");

        }
      );


      WaitUntil(() => task1Started);

      // Wait until task stops executing
      WaitUntil(() => !bt.GetRunningTasks().Any());
      
      Assert.IsTrue(true); // Ok, we are still alive - the process did not crash
      // The process would crash if the delagte would return void instead of Task, since in that scenario,
      // there is noway to atatch the Exception too, so it is raised on background thread pool , which crashes the process

    }
  }
}