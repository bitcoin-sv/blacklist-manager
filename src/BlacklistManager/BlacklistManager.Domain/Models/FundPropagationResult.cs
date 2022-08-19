// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  /// <summary>
  /// Class that holds information about funds propagation process
  /// </summary>
  public class FundPropagationResult
  {
    readonly List<FundStatePropagated> _propagated = new List<FundStatePropagated>();
    readonly List<Node> _nodesWithError = new List<Node>();
    readonly List<Node> _recoveredNodes = new List<Node>();
    readonly bool _internalError = false;

    public FundPropagationResult(bool internalError = false)
    {
      this._internalError = internalError;
    }

    public void AddPropagated(IEnumerable<Propagation> propagations, Node node)
    {
      _propagated.AddRange(propagations.Select(p => new FundStatePropagated(p.StateToPropagate, node, DateTime.UtcNow)));
    }

    public void AddNodeWithError(Node node, Exception httpException)
    {
      var n = node.Clone(); // don't want to change input instance
      n.SetError(httpException);
      _nodesWithError.Add(n);
    }

    public void AddRecoveredNode(Node node)
    {
      var n = node.Clone(); // don't want to change input instance
      n.ClearError();
      _recoveredNodes.Add(n);
    }

    /// <summary>
    /// Nodes that are in error state, are not reachable or some other network error occurred during propagation
    /// </summary>
    public IEnumerable<Node> NodesWithError => _nodesWithError;
    /// <summary>
    /// Nodes that used to be in error state but this time fund propagation was successful
    /// </summary>
    public IEnumerable<Node> RecoveredNodes => _recoveredNodes;
    /// <summary>
    /// List of funds successfully propagated to nodes
    /// </summary>
    public List<FundStatePropagated> PropagatedFunds => _propagated.ToList();
    /// <summary>
    /// If true then propagation of funds was successful
    /// </summary>
    public bool WasSuccessful => !_internalError && !NodesWithError.Any();
  }
}
