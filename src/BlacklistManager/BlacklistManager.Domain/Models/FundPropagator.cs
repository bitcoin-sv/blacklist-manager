// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static BlacklistManager.Domain.Models.FundPropagator.Propagations;

namespace BlacklistManager.Domain.Models
{
  public class FundPropagator
  {
    public enum PropagationAction
    {
      AddPolicy,
      AddConsensus,
      RemovePolicy,
      DoNothing
    }

    private readonly IBitcoindFactory bitcoindFactory;
    private readonly IEnumerable<Node> nodes;
    private readonly CancellationToken cancellationToken;
    private readonly ILogger logger;
    private readonly ILogger loggerRpc;

    public FundPropagator(IBitcoindFactory bitcoindFactory, IEnumerable<Node> nodes, CancellationToken cancellationToken, ILoggerFactory logger)
    {
      this.bitcoindFactory = bitcoindFactory;
      this.nodes = nodes;
      this.cancellationToken = cancellationToken;
      this.logger = logger.CreateLogger(LogCategories.Domain);
      this.loggerRpc = logger.CreateLogger(LogCategories.DomainRPC);
    }

    /// <summary>
    /// Propagate fund states to bitcoin nodes.
    /// </summary>
    /// <param name="fundStatesToPropagate">list of fund states needed to propagate in chronological order of state changes (ordered by fundId and fundStateId)</param>
    /// <returns>successfully propagated fund states</returns>
    public async Task<FundPropagationResult> PropagateAsync(IEnumerable<FundStateToPropagate> fundStatesToPropagate)
    {
      var propagations = new Propagations(GetNodesForPropagation(), fundStatesToPropagate, cancellationToken, logger);
      return await PropagateAsync(propagations.GroupByNode());
    }

    private async Task<FundPropagationResult> PropagateAsync(NodesPropagations nodesPropagations)
    {
      var result = new FundPropagationResult();
      int propagationCount = 0;

      foreach (var nodePropagations in nodesPropagations.All)
      {
        var node = nodePropagations.Node;
        propagationCount += nodePropagations.All.Count();

        try
        {
          logger.LogDebug($"Propagations for node '{node}' started. {nodePropagations.All.Count()} items to propagate");

          var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password, cancellationToken);

          PropagationAction? previousPropagationAction = null;
          var sameActionPropagations = new List<Propagation>();

          // execute propagations grouped by propagation actions
          foreach (var propagation in nodePropagations.All)
          {
            if (cancellationToken.IsCancellationRequested)
            {
              break;
            }
            if (propagation.Action == PropagationAction.DoNothing)
            {
              result.AddPropagated(propagation, node);
              continue;
            }
            if (IsNewPropagationAction(previousPropagationAction, propagation.Action))
            {
              await PropagateAsync(bitcoind, node, previousPropagationAction.Value, sameActionPropagations, result);
              sameActionPropagations.Clear();
            }
            sameActionPropagations.Add(propagation);
            previousPropagationAction = propagation.Action;
          }

          if (previousPropagationAction.HasValue)
          {
            await PropagateAsync(bitcoind, node, previousPropagationAction.Value, sameActionPropagations, result);
          }

          if (node.HasErrors() && result.PropagatedFunds.Any())
          {
            result.AddRecoveredNode(node);
          }

          if (cancellationToken.IsCancellationRequested)
          {
            logger.LogWarning($"Propagation for node '{node}' canceled.");
          }
          else
          {
            logger.LogDebug($"Propagations for node '{node}' ended");
          }
        }
        catch (System.Net.Http.HttpRequestException httpException)
        {
          logger.LogWarning($"Propagation for node '{node}' aborted with error: {httpException.Message}");          
          result.AddNodeWithError(node, httpException);
        }
      }
      return result;
    }

    private async Task PropagateAsync(IBitcoind bitcoind, Node node, PropagationAction action, List<Propagation> propagations, FundPropagationResult result)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return;
      }
      switch (action)
      {
        case PropagationAction.AddPolicy:
          await bitcoind.AddToPolicyBlacklistAsync(propagations.Select(p => new Fund(p.StateToPropagate)));
          logger.LogDebug($"AddToPolicyBlacklist called for {propagations.Count} funds");
          break;
        case PropagationAction.AddConsensus:
          await bitcoind.AddToConsensusBlacklistAsync(propagations.Select(p => new Fund(p.StateToPropagate)));
          logger.LogDebug($"AddToConsensusBlacklist called for {propagations.Count} funds");
          break;
        case PropagationAction.RemovePolicy:
          await bitcoind.RemoveFromPolicyBlacklistAsync(propagations.Select(p => new Fund(p.StateToPropagate)));
          logger.LogDebug($"RemoveFromPolicyBlacklist called for {propagations.Count} funds");
          break;
        case PropagationAction.DoNothing:
          logger.LogDebug($"No call for {propagations.Count} funds");
          break;
        default:
          throw new BadRequestException($"Unknown propagation action '{action}'");
      }
      foreach (var propagation in propagations)
      {
        loggerRpc.LogInformation(
          LogEvents.FundStatePropagation, 
          $"Fund '{propagation.StateToPropagate.TxOut.TxId}/{propagation.StateToPropagate.TxOut.Vout}' " +
          $"state '{propagation.StateToPropagate.EnforceAtHeight.ToStringShort()}' propagated to '{node}'");
      }
      result.AddPropagated(propagations, node);
    }

    private bool IsNewPropagationAction(PropagationAction? previousPropagationAction, PropagationAction currentPropagationAction)
    {
      return previousPropagationAction.HasValue && previousPropagationAction != currentPropagationAction;
    }

    private IEnumerable<Node> GetNodesForPropagation()
    {
      return nodes
        .Where(n => n.Status == NodeStatus.Connected)
        .ToArray();
    }

    public class Propagations
    {
      private readonly IEnumerable<Node> nodes;
      private readonly CancellationToken cancellationToken;
      private readonly IEnumerable<FundStateToPropagate> fundStatesToPropagate;
      private readonly ILogger logger;

      public Propagations(IEnumerable<Node> nodes, IEnumerable<FundStateToPropagate> fundStatesToPropagate, CancellationToken cancellationToken, ILogger logger)
      {
        this.nodes = nodes;
        this.fundStatesToPropagate = fundStatesToPropagate;
        this.cancellationToken = cancellationToken;
        this.logger = logger;
      }

      /// <summary>
      /// Group list of funds states to list of propagations by node
      /// </summary>
      /// <returns>list of propagations by node</returns>
      public NodesPropagations GroupByNode()
      {
        int? previousNodeId = null;
        var result = new NodesPropagations(nodes);
        var singleNodeStatesToPropagate = new List<FundStateToPropagate>();

        // group fundStatesToPropagete by node
        foreach (var fundStateToPropagate in fundStatesToPropagate)  // fundStatesToPropagate is ordered by nodeId and fundStateId
        {
          if (cancellationToken.IsCancellationRequested)
          {
            // we stop calculation - nothing will be propagated to nodes
            result.Clear();
            logger.LogDebug("Grouping of propagations by node canceled");
            return result;
          }

          if (IsNewNode(previousNodeId, fundStateToPropagate.NodeId))
          {
            result.Add(previousNodeId.Value, ConvertToPropagationUnits(singleNodeStatesToPropagate));
            singleNodeStatesToPropagate.Clear();
          }
          singleNodeStatesToPropagate.Add(fundStateToPropagate);
          previousNodeId = fundStateToPropagate.NodeId;
        }

        if (previousNodeId.HasValue)
        {
          result.Add(previousNodeId.Value, ConvertToPropagationUnits(singleNodeStatesToPropagate));
        }

        logger.LogDebug("Grouping of propagations by node stopped");

        return result;
      }

      private IEnumerable<Propagation> ConvertToPropagationUnits(List<FundStateToPropagate> statesToPropagate)
      {
        var propagationUnits = new List<Propagation>();
        foreach (var stateToPropagate in statesToPropagate)
        {
          propagationUnits.Add(new Propagation(
            stateToPropagate,
            FundStateToPropagationAction(stateToPropagate)));
        }
        return propagationUnits;
      }

      private PropagationAction FundStateToPropagationAction(FundStateToPropagate fsp)
      {
        if (fsp.Status == FundStatus.Imported)
        {
          throw new BadRequestException($"Can not set propagation action for imported fund '{fsp.TxOut}'");
        }

        if (fsp.EnforceAtHeight.ContainsIsConsensusFrozen)
        {
          if (EnforceAtHeightList.AreSameIntervals(fsp.EnforceAtHeight, fsp.EnforceAtHeightPrevious))
          {
            if (fsp.EnforceAtHeight.ContainsIsPolicyFrozen != fsp.EnforceAtHeightPrevious.ContainsIsPolicyFrozen)
            {
              // only PolicyExpiresWithConsensus changes
              return PropagationAction.AddConsensus;
            }
            return PropagationAction.DoNothing;
          }
          return PropagationAction.AddConsensus;
        }
        else if (fsp.EnforceAtHeight.ContainsIsPolicyFrozen)
        {
          if (fsp.EnforceAtHeightPrevious.ContainsIsConsensusFrozen)
          {
            ThrowIntervalMismathException(fsp);
          }
          else if (fsp.EnforceAtHeightPrevious.ContainsIsPolicyFrozen)
          {
            return PropagationAction.DoNothing;
          }
          return PropagationAction.AddPolicy;
        }
        else if (fsp.EnforceAtHeight.ContainsIsSpendable)
        {
          if (fsp.EnforceAtHeightPrevious.ContainsIsConsensusFrozen)
          {
            ThrowIntervalMismathException(fsp);
          }
          else if (fsp.EnforceAtHeightPrevious.ContainsIsPolicyFrozen)
          {
            return PropagationAction.RemovePolicy;
          }
          return PropagationAction.DoNothing;
        }

        ThrowIntervalMismathException(fsp);
        return PropagationAction.DoNothing;
      }
    
      private void ThrowIntervalMismathException(FundStateToPropagate fsp)
      {
        throw new BadRequestException($"Fund state interval mismatch for '{fsp.TxOut}'. Previous interval: {fsp.EnforceAtHeightPrevious}, current interval: {fsp.EnforceAtHeight}");
      }

      private bool IsNewNode(int? previousNodeId, int currentNodeId)
      {
        return previousNodeId.HasValue && currentNodeId != previousNodeId;
      }

      public class Propagation
      {
        public Propagation(FundStateToPropagate stateToPropagate, PropagationAction action)
        {
          FundStateId = stateToPropagate.Id;
          Action = action;
          StateToPropagate = stateToPropagate;
        }

        public long FundStateId { get; private set; }
        public FundStateToPropagate StateToPropagate { get; set; }
        public PropagationAction Action { get; private set; }

        public override string ToString()
        {
          return $"{StateToPropagate}, {Action}";
        }        
      }

      public class NodePropagations
      {
        private List<Propagation> propagations = new List<Propagation>();
        public IReadOnlyCollection<Propagation> All => propagations;

        public NodePropagations(Node node)
        {
          Node = node;
        }

        public Node Node { get; private set; }

        public void AddRange(IEnumerable<Propagation> propagationUnits)
        {
          this.propagations.AddRange(propagationUnits);
        }

        public override string ToString()
        {
          return $"Node:{Node},Propagation count:{propagations.Count()}";
        }
      }

      public class NodesPropagations
      {
        private readonly List<NodePropagations> nodesPropagations = new List<NodePropagations>();
        public IReadOnlyCollection<NodePropagations> All => nodesPropagations;

        public NodesPropagations(IEnumerable<Node> nodes)
        {
          Init(nodes);
        }

        public void Clear()
        {
          nodesPropagations.Clear();
        }

        private void Init(IEnumerable<Node> nodes)
        {
          foreach (var node in nodes)
          {
            nodesPropagations.Add(new NodePropagations(node));
          }
        }

        public void Add(int nodeId, IEnumerable<Propagation> propagationUnits)
        {
          var nodePropagationUnits = nodesPropagations.FirstOrDefault(p => p.Node.Id == nodeId);
          if (nodePropagationUnits == null)
          {
            throw new BadRequestException($"Node with id {nodeId} could not be found in propagationUnitsByNodes");
          }
          nodePropagationUnits.AddRange(propagationUnits);
        }

        public override string ToString()
        {
          return $"Node count:{nodesPropagations.Count()}";
        }
      }
    }
  }
}
