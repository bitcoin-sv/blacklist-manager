// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Unit.Mocks
{
  /// <summary>
  /// This mocked class version does not mock any logic code but only mocks time functions
  /// </summary>
  public class LongWaitMock : BlacklistManager.Infrastructure.BackgroundJobs.LongWait
  {
    readonly int delayOverride;

    public LongWaitMock(int delay = 0)
    {
      CurrentTime = DateTime.UtcNow;
      this.delayOverride = delay;
    }

    protected override async Task TaskDelayAsync(int delay, CancellationToken cancellationToken)
    {
      if (delayOverride > 0)
      {
        await Task.Delay(delayOverride, cancellationToken);
      }
    }

    public DateTime CurrentTime { get; set; }

    public override DateTime GetCurrentTime()
    {
      var ct = CurrentTime;
      CurrentTime = CurrentTime.AddDays(1);
      return ct;
    }
  }
}
