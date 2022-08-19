// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.BackgroundJobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.API.Rest
{
  public class BackgroundJobStarter : IHostedService
  {
    private readonly IBackgroundJobs backgroundJobs;
    private readonly ILogger<BackgroundJobStarter> logger;

    public BackgroundJobStarter(
      ILogger<BackgroundJobStarter> logger,
      IBackgroundJobs backgroundJobs)
    {
      this.logger = logger;
      this.backgroundJobs = backgroundJobs;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
      logger.LogDebug("Starting background jobs");
      // we start background jobs to resume any interrupted background jobs
      await backgroundJobs.StartAllAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
      logger.LogDebug("Stopping background jobs");
      await backgroundJobs.StopAllAsync();
      logger.LogDebug("Background jobs stopped");
    }
  }
}
