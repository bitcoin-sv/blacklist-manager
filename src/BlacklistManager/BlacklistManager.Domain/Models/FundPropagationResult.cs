// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using static BlacklistManager.Domain.Models.FundPropagator.Propagations;

namespace BlacklistManager.Domain.Models
{
  /// <summary>
  /// Class that holds information about funds propagation process
  /// </summary>
  public class FundPropagationResult
  {
    private List<FundStatePropagated> propagated = new List<FundStatePropagated>();
    private List<Node> nodesWithError = new List<Node>();
    private List<Node> recoveredNodes = new List<Node>();
    private readonly bool internalError = false;

    public FundPropagationResult(bool internalError = false)
    {
      this.internalError = internalError;
    }

    public void AddPropagated(Propagation propagation, Node node)
    {
      propagated.Add(new FundStatePropagated(propagation.StateToPropagate, node, DateTime.UtcNow));
    }

    public void AddPropagated(IEnumerable<Propagation> propagations, Node node)
    {
      propagated.AddRange(propagations.Select(p => new FundStatePropagated(p.StateToPropagate, node, DateTime.UtcNow)));
    }

    public void AddNodeWithError(Node node, System.Net.Http.HttpRequestException httpException)
    {
      var n = node.Clone(); // don't want to change input instance
      n.SetError(httpException);
      nodesWithError.Add(n);
    }

    public void AddRecoveredNode(Node node)
    {
      var n = node.Clone(); // don't want to change input instance
      n.ClearError();
      recoveredNodes.Add(n);
    }

    /// <summary>
    /// Nodes that are in error state, are not reachable or some other network error occurred during propagation
    /// </summary>
    public IEnumerable<Node> NodesWithError => nodesWithError;
    /// <summary>
    /// Nodes that used to be in error state but this time fund propagation was successful
    /// </summary>
    public IEnumerable<Node> RecoveredNodes => recoveredNodes;
    /// <summary>
    /// If true then some internal error occurred during funds propagation
    /// </summary>
    public bool InternalError => internalError;
    /// <summary>
    /// List of funds successfully propagated to nodes
    /// </summary>
    public IEnumerable<FundStatePropagated> PropagatedFunds => propagated;
    /// <summary>
    /// If true then propagation of funds was successful
    /// </summary>
    public bool WasSuccessful => !internalError && !NodesWithError.Any();
  }
}
