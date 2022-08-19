// Copyright (c) 2020 Bitcoin Association

using Common;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.BackgroundJobs
{
  public interface IBackgroundJobs
  {
    bool OfflineMode { get; }

    BackgroundTasks BackgroundTasks { get; }

    /// <summary>
    /// Set value for off-line mode
    /// </summary>
    Task SetOfflineModeAsync(bool offline);

    /// <summary>
    /// Check if background jobs are in off-line mode. If they are not a BadRequestException will be thrown
    /// </summary>
    void CheckForOfflineMode();

    /// <summary>
    /// Propagate funds states to nodes until all funds states are propagated successfully
    /// </summary>
    Task StartPropagateFundsStatesAsync();

    /// <summary>
    /// Process court orders until all court orders are processed successfully
    /// </summary>
    Task StartProcessCourtOrdersAsync();

    /// <summary>
    /// Process court order acceptances until all are processed successfully
    /// </summary>
    Task StartProcessCourtOrderAcceptancesAsync();

    /// <summary>
    /// Submit whitelisted transaction ids for confiscation transactions
    /// </summary>
    /// <returns></returns>
    Task StartSubmitWhitelistTxIdsAsync();

    /// <summary>
    /// Retry failed orders by downloading and processing them again
    /// </summary>
    Task StartFailedCourtOrdersProcessingAsync();

    /// <summary>
    /// Start all background jobs
    /// </summary>
    Task StartAllAsync();

    /// <summary>
    /// Stop all background jobs
    /// </summary>
    Task StopAllAsync();
  }
}
