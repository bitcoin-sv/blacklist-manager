// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.BackgroundJobs
{
  public interface ILongWait
  {
    Task WaitUntilAsync(DateTime waitUntilUTC, CancellationToken cancellationToken, Action<string> logMessage);
    Task<bool> WaitUntilAsync(int delayMs, CancellationToken cancellationToken);
    DateTime GetCurrentTime();
  }
}