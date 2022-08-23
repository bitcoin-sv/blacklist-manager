﻿// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using BlacklistManager.Domain.Models;

namespace BlacklistManager.Domain.Repositories
{
  public interface INodeRepository
  {


    /// <summary>
    /// Returns null if node already exists
    /// </summary>
    Node CreateNode(Node node);

    
    /// <summary>
    /// Returns false if not found, Can not be used to update nodeStatus
    /// </summary>
    bool UpdateNode(Node node);
    
    /// <summary>
    /// Updates lastError and lastErrorAt fields
    /// </summary>
    /// <returns>false if not updated</returns>
    bool UpdateNodeError(Node node);

    Node GetNode(string hostAndPort);

    int DeleteNode(string hostAndPort);

    public IEnumerable<Node> GetNodes(); 
  }
}
