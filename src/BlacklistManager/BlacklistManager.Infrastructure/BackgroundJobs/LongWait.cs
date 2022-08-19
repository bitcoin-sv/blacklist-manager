// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.BackgroundJobs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.BackgroundJobs
{
  public class LongWait : ILongWait
  {
    private const int ONE_DAY = 1000 * 60 * 60 * 24;

    /// <summary>
    /// Wait for specified amount of milliseconds
    /// </summary>
    /// <returns>true if not canceled</returns>
    public async Task<bool> WaitUntilAsync(int delayMs, CancellationToken cancellationToken)
    {
      try
      {
        await TaskDelayAsync(delayMs, cancellationToken);
      }
      catch (TaskCanceledException)
      {
        return false;
      }
      return true;
    }

    /// <summary>
    /// Wait until specified time is reached
    /// </summary>
    public async Task WaitUntilAsync(DateTime waitUntilUTC, CancellationToken cancellationToken, Action<string> logMessage)
    {
      var currentTime = GetCurrentTime();

      if (waitUntilUTC.Kind != DateTimeKind.Utc || currentTime.Kind != DateTimeKind.Utc)
      {
        throw new ArgumentException("UTC time expected");
      }

      if (waitUntilUTC > currentTime)
      {
        var timeSpan = waitUntilUTC - currentTime;
        if (timeSpan.TotalDays > 1)
        {
          logMessage($"Still to wait {Math.Floor(timeSpan.TotalDays)} days");
          var waited = await WaitUntilAsync(ONE_DAY, cancellationToken);
          if (waited)
          {
            await WaitUntilAsync(waitUntilUTC, cancellationToken, logMessage);
          }
        }
        else
        {
          logMessage($"Still to wait {Math.Floor(timeSpan.TotalMinutes)} minutes");
          await WaitUntilAsync(Convert.ToInt32(timeSpan.TotalMilliseconds), cancellationToken);
        }
      }
    }

    public virtual DateTime GetCurrentTime()
    {
      return DateTime.UtcNow;
    }

    protected virtual async Task TaskDelayAsync(int delay, CancellationToken cancellationToken)
    {
      await Task.Delay(delay, cancellationToken);
    }
  }
}
