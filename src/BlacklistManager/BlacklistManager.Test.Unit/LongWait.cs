// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Test.Unit.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Unit
{
  [TestClass]
  public class LongWait
  {
    [TestMethod]
    public async Task LongWait_Canceled_ShouldStopWaitingAsync()
    {
      //arrange
      var lw = new LongWaitMock(50);
      CancellationTokenSource cancellationSource = new CancellationTokenSource();
      Action<string> logHandle = (m) => { };

      //act
      Stopwatch stopwatch = new Stopwatch();
      stopwatch.Start();

      _ = Task.Run(() => { System.Threading.Thread.Sleep(25); cancellationSource.Cancel(); });
      await lw.WaitUntilAsync(DateTime.UtcNow.AddMilliseconds(1000), cancellationSource.Token, logHandle);

      stopwatch.Stop();

      //assert
      Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, "WaitUntil did not stop before given date");
    }

    [TestMethod]
    public async Task LongWait_MoreThanOneDay_SplitsToDaysAsync()
    {
      //arrange
      var lw = new LongWaitMock();
      CancellationTokenSource cancellationSource = new CancellationTokenSource();
      var logMessages = new List<string>();
      Action<string> logHandle = (m) => { logMessages.Add(m); };

      //act
      await lw.WaitUntilAsync(DateTime.UtcNow.AddDays(5).AddMinutes(10), cancellationSource.Token, logHandle);

      //assert
      Assert.AreEqual(6, logMessages.Count, "Wrong number of date splits");
      Assert.AreEqual("Still to wait 5 days", logMessages[0]);
      Assert.AreEqual("Still to wait 4 days", logMessages[1]);
      Assert.AreEqual("Still to wait 3 days", logMessages[2]);
      Assert.AreEqual("Still to wait 2 days", logMessages[3]);
      Assert.AreEqual("Still to wait 1 days", logMessages[4]);
      Assert.AreEqual("Still to wait 10 minutes", logMessages[5]);
    }

    [TestMethod]
    public async Task LongWait_NoDaysAsync()
    {
      //arrange
      var lw = new LongWaitMock();
      CancellationTokenSource cancellationSource = new CancellationTokenSource();
      var logMessages = new List<string>();
      Action<string> logHandle = (m) => { logMessages.Add(m); lw.CurrentTime = lw.CurrentTime.AddDays(1); };

      //act
      await lw.WaitUntilAsync(DateTime.UtcNow.AddMinutes(10), cancellationSource.Token, logHandle);

      //assert
      Assert.AreEqual(1, logMessages.Count, "Wrong number of internal calls");
      Assert.AreEqual("Still to wait 10 minutes", logMessages[0]);
    }
  }
}
