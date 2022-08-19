// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public interface INodes
  {
    Task<Node> CreateNodeAsync(Node node);
    Task<int> DeleteNodeAsync(string id);
    Task<Node> GetNodeAsync(string id);
    Task<IEnumerable<Node>> GetNodesAsync();
    Task<bool> UpdateNodeAsync(Node node);
  }
}