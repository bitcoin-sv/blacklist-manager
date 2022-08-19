// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  public class NodePropagations
  {
    readonly List<Propagation> propagations = new List<Propagation>();
    public IReadOnlyCollection<Propagation> All => propagations;

    public NodePropagations(Node node)
    {
      Node = node;
    }

    public Node Node { get; private set; }

    public void AddRange(Propagation[] propagationUnits)
    {
      this.propagations.AddRange(propagationUnits);
    }

    public override string ToString()
    {
      return $"Node:{Node},Propagation count:{propagations.Count}";
    }
  }
}
