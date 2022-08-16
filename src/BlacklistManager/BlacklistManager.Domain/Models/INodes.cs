// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public interface INodes
  {
    Task<Node> CreateNodeAsync(Node node);
    int DeleteNode(string id);
    Node GetNode(string id);
    IEnumerable<Node> GetNodes();
    Task<bool> UpdateNodeAsync(Node node);
  }
}