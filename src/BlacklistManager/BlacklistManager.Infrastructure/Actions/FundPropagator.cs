// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.Bitcoin;
using Common.BitcoinRpcClient;
using Common.BitcoinRpcClient.Requests;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Actions
{
  public class FundPropagator : IFundPropagator
  {
    readonly IBitcoinFactory _bitcoinFactory;
    readonly ICourtOrderRepository _courtOrderRepository;
    readonly INodeRepository _nodeRepository;
    readonly IMetrics _metrics;
    readonly ILogger<FundPropagator> _logger;

    public FundPropagator(IBitcoinFactory bitcoinFactory, ICourtOrderRepository courtOrderRepository, INodeRepository nodeRepository, IMetrics metrics, ILoggerFactory logger)
    {
      _bitcoinFactory = bitcoinFactory;
      _courtOrderRepository = courtOrderRepository;
      _nodeRepository = nodeRepository;
      _metrics = metrics;
      _logger = logger.CreateLogger<FundPropagator>();
    }

    /// <summary>
    /// Update nodes with latest funds states
    /// </summary>    
    public async Task<FundPropagationResult> PropagateFundsStateAsync(CancellationToken cancellationToken)
    {
      try
      {
        var result = new FundPropagationResult();
        var fundStateToPropagate = await _courtOrderRepository.GetFundStateToPropagateAsync();
        if (fundStateToPropagate.Any())
        {
          _logger.LogDebug($"Starting propagation for {fundStateToPropagate.Count()} fund states");
          result = await ProcessFundsForPropagationAsync(fundStateToPropagate, cancellationToken);
          await PersistPropagationResultAsync(result);
          _logger.LogInformation(LogEvents.FundStatePropagation, $"Propagation of fund states ended. {result.PropagatedFunds.Count}/{fundStateToPropagate.Count()} fund states propagated successfully");
        }
        _metrics.PropagatedFunds.Add(result.PropagatedFunds.Count);
        return result;
      }
      catch (Exception ex)
      {
        _logger.LogError(LogEvents.FundStatePropagation, ex, $"Propagation of fund states aborted with exception");
        return new FundPropagationResult(internalError: true);
      }
    }

    private async Task PersistPropagationResultAsync(FundPropagationResult propagationResult)
    {
      if (propagationResult.PropagatedFunds.Any())
      {
        await _courtOrderRepository.InsertFundStateNodeAsync(propagationResult.PropagatedFunds);
      }

      // Update state for failed and recovered nodes:
      foreach (var node in propagationResult.NodesWithError.Concat(propagationResult.RecoveredNodes))
      {
        await _nodeRepository.UpdateNodeErrorAsync(node);
      }
      _logger.LogDebug("Result for propagation of fund states persisted to database");
    }


    /// <summary>
    /// Propagate fund states to bitcoin nodes.
    /// </summary>
    /// <param name="fundStatesToPropagate">list of fund states needed to propagate in chronological order of state changes (ordered by fundId and fundStateId)</param>
    /// <returns>successfully propagated fund states</returns>
    private async Task<FundPropagationResult> ProcessFundsForPropagationAsync(IEnumerable<FundStateToPropagate> fundStatesToPropagate, CancellationToken cancellationToken)
    {
      var nodesPropagations = await GroupPropagationsByNodeAsync(fundStatesToPropagate, cancellationToken);
      var result = new FundPropagationResult();

      foreach (var nodePropagations in nodesPropagations.All)
      {
        cancellationToken.ThrowIfCancellationRequested();

        if (nodePropagations.All.Count == 0)
        {
          continue;
        }

        var node = nodePropagations.Node;
        try
        {
          _logger.LogDebug($"Propagations for node '{node}' started. {nodePropagations.All.Count(x => x.Action != PropagationAction.DoNothing)} items to propagate");
          var bitcoind = _bitcoinFactory.Create(node.Host, node.Port, node.Username, node.Password);

          result.AddPropagated(nodePropagations.All.Where(x => x.Action == PropagationAction.DoNothing).ToArray(), node);

          var waitingPropagations = nodePropagations.All.Where(x => x.Action != PropagationAction.DoNothing).ToArray();
          var currentPropagation = waitingPropagations.FirstOrDefault()?.Action ?? PropagationAction.DoNothing;
          var propagationsList = new List<Propagation>();

          foreach (var propagation in waitingPropagations)
          {
            if (currentPropagation != propagation.Action)
            {
              await InitiatePropagationsAsync(propagationsList.ToArray());
              propagationsList.Clear();
            }

            propagationsList.Add(propagation);
            currentPropagation = propagation.Action;
          }
          await InitiatePropagationsAsync(propagationsList.ToArray());

          if (_logger.IsEnabled(LogLevel.Debug))
          {
            result.PropagatedFunds.ForEach(x =>
            {
              _logger.LogDebug(
                LogEvents.FundStatePropagation,
                $"Fund '{x.StateToPropagate.TxOut.TxId}/{x.StateToPropagate.TxOut.Vout}' " +
                $"state '{x.StateToPropagate.EnforceAtHeight.ToStringShort()}' propagated to '{node}'");
            });
          }

          if (node.HasErrors() && result.PropagatedFunds.Any())
          {
            result.AddRecoveredNode(node);
          }

          _logger.LogDebug($"Propagations for node '{node}' ended");

          async Task InitiatePropagationsAsync(Propagation[] propagations)
          {
            if (propagations.Length == 0)
              return;

            var action = propagations[0].Action;
            switch (action)
            {
              case PropagationAction.AddPolicy:
                await PropagateAddPolicyAsync(propagations);
                break;
              case PropagationAction.AddConsensus:
                await PropagateAddConsensusAsync(propagations);
                break;
              case PropagationAction.RemovePolicy:
                await PropagateRemovePolicyAsync(propagations);
                break;
              default:
                throw new InvalidOperationException($"Unsupported propagation action. '{action}'");
            }
          }

          async Task PropagateAddPolicyAsync(Propagation[] propagations)
          {
            if (propagations.Any())
            {
              await bitcoind.AddToPolicyBlacklistAsync(ToRpc(propagations), cancellationToken);
              _logger.LogDebug($"AddToPolicyBlacklist called for {propagations.Length} funds");
              result.AddPropagated(propagations, node);
            }
          }
          async Task PropagateAddConsensusAsync(Propagation[] propagations)
          {
            if (propagations.Any())
            {
              await bitcoind.AddToConsensusBlacklistAsync(ToRpc(propagations, true), cancellationToken);
              _logger.LogDebug($"AddToConsensusBlacklist called for {propagations.Length} funds");
              result.AddPropagated(propagations, node);
            }
          }
          async Task PropagateRemovePolicyAsync(Propagation[] propagations)
          {
            if (propagations.Any())
            {
              await bitcoind.RemoveFromPolicyBlacklistAsync(ToRpc(propagations), cancellationToken);
              _logger.LogDebug($"RemoveFromPolicyBlacklist called for {propagations.Length} funds");
              result.AddPropagated(propagations, node);
            }
          }
        }
        catch (RpcException rpcException)
        {
          _metrics.FailedNodesForPropagation.Add(1);
          _logger.LogWarning($"Propagation for node '{node}' aborted with error: {rpcException.Message}");
          result.AddNodeWithError(node, rpcException);
        }
        catch (OperationCanceledException ex)
        {
          _metrics.FailedNodesForPropagation.Add(1);
          _logger.LogWarning($"Propagation for node '{node}' canceled.");
          result.AddNodeWithError(node, new Exception("Propagation was canceled.", ex));
        }
      }
      return result;
    }

    private static RpcFrozenFunds ToRpc(IEnumerable<Propagation> propagations, bool mapConsensusFields = false)
    {
      var frozenFundsInternal = new RpcFrozenFunds
      {
        Funds = propagations
          .Select(x => new RpcFrozenFunds.RpcFund()
          {
            TxOut = new RpcFrozenFunds.RpcFund.RpcTxOut() { TxId = x.StateToPropagate.TxOut.TxId, Vout = x.StateToPropagate.TxOut.Vout },
            EnforceAtHeight = mapConsensusFields ? x.StateToPropagate.EnforceAtHeight.GetConsolidatedList() : null,
            PolicyExpiresWithConsensus = mapConsensusFields ? !x.StateToPropagate.EnforceAtHeight.ContainsIsPolicyFrozen : null
          })
          .ToList()
      };

      return frozenFundsInternal;
    }

    /// <summary>
    /// Group list of funds states to list of propagations by node
    /// </summary>
    /// <returns>list of propagations by node</returns>
    private async Task<NodesPropagations> GroupPropagationsByNodeAsync(IEnumerable<FundStateToPropagate> fundStatesToPropagate, CancellationToken cancellationToken)
    {
      int previousNodeId = fundStatesToPropagate.First().NodeId;
      var allNodes = await _nodeRepository.GetNodesAsync();
      var result = new NodesPropagations(allNodes.Where(n => n.Status == NodeStatus.Connected).ToArray());
      var singleNodeStatesToPropagate = new List<FundStateToPropagate>();

      // group fundStatesToPropagate by node
      foreach (var fundStateToPropagate in fundStatesToPropagate)  // fundStatesToPropagate is ordered by nodeId,fundStateId
      {
        if (cancellationToken.IsCancellationRequested)
        {
          result.Clear();
          _logger.LogDebug("Preparing propagations for nodes was canceled. Nothing will be propagated.");
          return result;
        }

        if (previousNodeId != fundStateToPropagate.NodeId)
        {
          result.Add(previousNodeId, singleNodeStatesToPropagate.Select(x => new Propagation(x, FundStateToPropagationAction(x))).ToArray());
          singleNodeStatesToPropagate.Clear();
        }
        singleNodeStatesToPropagate.Add(fundStateToPropagate);
        previousNodeId = fundStateToPropagate.NodeId;
      }

      result.Add(previousNodeId, singleNodeStatesToPropagate.Select(x => new Propagation(x, FundStateToPropagationAction(x))).ToArray());

      _logger.LogDebug("Grouping of propagations by node stopped");

      return result;
    }

    private static PropagationAction FundStateToPropagationAction(FundStateToPropagate fsp)
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
          throw new BadRequestException($"Fund state interval mismatch for '{fsp.TxOut}'. Previous interval: {fsp.EnforceAtHeightPrevious}, current interval: {fsp.EnforceAtHeight}");
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
          throw new BadRequestException($"Fund state interval mismatch for '{fsp.TxOut}'. Previous interval: {fsp.EnforceAtHeightPrevious}, current interval: {fsp.EnforceAtHeight}");
        }
        else if (fsp.EnforceAtHeightPrevious.ContainsIsPolicyFrozen)
        {
          return PropagationAction.RemovePolicy;
        }
        return PropagationAction.DoNothing;
      }

      throw new BadRequestException($"Fund state interval mismatch for '{fsp.TxOut}'. Previous interval: {fsp.EnforceAtHeightPrevious}, current interval: {fsp.EnforceAtHeight}");
    }
  }
}
