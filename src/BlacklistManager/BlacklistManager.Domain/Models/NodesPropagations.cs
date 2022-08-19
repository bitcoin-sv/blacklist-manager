// Copyright (c) 2020 Bitcoin Association

using Common;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  public class NodesPropagations
  {
    private readonly List<NodePropagations> nodesPropagations = new List<NodePropagations>();
    public IReadOnlyCollection<NodePropagations> All => nodesPropagations;

    public NodesPropagations(IEnumerable<Node> nodes)
    {
      nodesPropagations.AddRange(nodes.Select(x => new NodePropagations(x)));
    }

    public void Clear()
    {
      nodesPropagations.Clear();
    }

    public void Add(int nodeId, Propagation[] propagationUnits)
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
