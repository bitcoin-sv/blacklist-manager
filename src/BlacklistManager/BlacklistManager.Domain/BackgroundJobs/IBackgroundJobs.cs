// Copyright (c) 2020 Bitcoin Association

using System.Threading.Tasks;

namespace BlacklistManager.Domain.BackgroundJobs
{
  public interface IBackgroundJobs
  {    
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
    /// Process consensus activations until all are processed successfully
    /// </summary>
    Task StartProcessConsensusActivationAsync();

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
