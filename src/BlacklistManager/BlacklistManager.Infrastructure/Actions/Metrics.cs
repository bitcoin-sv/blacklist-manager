// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Actions;
using System.Diagnostics.Metrics;

namespace BlacklistManager.Infrastructure.Actions
{
  public class Metrics : IMetrics
  {
    const string METER_VERSION = "1.0.0";
    const string BASE_METER_NAME = "nChain.BlacklistManager.Infrastructure.";
    public const string COURT_ORDER_STATISTICS_METER = $"{BASE_METER_NAME}CourtStatisticsOrders";


    private Meter CourtOrderMeter { get; init; } = new Meter(COURT_ORDER_STATISTICS_METER, METER_VERSION);

    public Counter<long> ProcessedCourtOrders { get; init; }
    public Counter<long> FailedCourtOrders { get; init; }
    public Counter<long> ProcessedConsensusActivations { get; init; }
    public Counter<long> FailedConsensusActivations { get; init; }
    public Counter<long> FailedNodesForPropagation { get; init; }
    public Counter<long> PropagatedFunds { get; init; }
    public Counter<long> SubmittedTxs { get; init; }
    public Counter<long> RejectedTxs { get; init; }

    public Metrics()
    {
      ProcessedCourtOrders = CourtOrderMeter.CreateCounter<long>("blacklist_manager_processed_court_orders", description: "Number of processed court orders.");
      FailedCourtOrders = CourtOrderMeter.CreateCounter<long>("blacklist_manager_failed_court_orders", description: "Number of failed court orders.");

      ProcessedConsensusActivations = CourtOrderMeter.CreateCounter<long>("blacklist_manager_processed_consensus_activations", description: "Number of processed consensus activations.");
      FailedConsensusActivations = CourtOrderMeter.CreateCounter<long>("blacklist_manager_failed_consensus_activations", description: "Number of failed consensus activations.");

      FailedNodesForPropagation = CourtOrderMeter.CreateCounter<long>("blacklist_manager_failed_node_propagation", description: "Number of nodes that failed for blacklist update.");
      PropagatedFunds = CourtOrderMeter.CreateCounter<long>("blacklist_manager_successfully_propagated_funds", description: "Number of successfully propagated fund states to blacklist.");

      SubmittedTxs = CourtOrderMeter.CreateCounter<long>("blacklist_manager_submitted_transactions", description: "Number of successfully submitted transactions.");
      RejectedTxs = CourtOrderMeter.CreateCounter<long>("blacklist_manager_rejected_transactions", description: "Number of rejected transactions.");
    }
  }
}
