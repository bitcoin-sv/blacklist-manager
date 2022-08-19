// Copyright (c) 2020 Bitcoin Association

using System.Diagnostics.Metrics;

namespace BlacklistManager.Domain.Actions
{
  public interface IMetrics
  {
    public Counter<long> ProcessedCourtOrders { get; }
    public Counter<long> FailedCourtOrders { get; }
    public Counter<long> ProcessedConsensusActivations { get; }
    public Counter<long> FailedConsensusActivations { get; }
    public Counter<long> FailedNodesForPropagation { get; }
    public Counter<long> PropagatedFunds { get; }
    public Counter<long> SubmittedTxs { get; init; }
    public Counter<long> RejectedTxs { get; init; }
  }
}
