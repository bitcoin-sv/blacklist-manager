// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;
using BlacklistManager.Domain.Models;

namespace BlacklistManager.Domain.Repositories
{
  public interface INodeRepository
  {


    /// <summary>
    /// Returns null if node already exists
    /// </summary>
    Task<Node> CreateNodeAsync(Node node);

    
    /// <summary>
    /// Returns false if not found, Can not be used to update nodeStatus
    /// </summary>
    Task<bool> UpdateNodeAsync(Node node);
    
    /// <summary>
    /// Updates lastError and lastErrorAt fields
    /// </summary>
    /// <returns>false if not updated</returns>
    Task<bool> UpdateNodeErrorAsync(Node node);

    Task<Node> GetNodeAsync(string hostAndPort);

    Task<int> DeleteNodeAsync(string hostAndPort);

    Task<IEnumerable<Node>> GetNodesAsync(); 
  }
}
