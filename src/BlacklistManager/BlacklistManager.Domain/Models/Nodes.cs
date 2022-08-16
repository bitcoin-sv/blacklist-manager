// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.BitcoinRpc;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.Domain.Models
{
  public class Nodes : INodes
  {
    readonly IBitcoindFactory bitcoindFactory;
    readonly IBackgroundJobs backgroundJobs;

    readonly INodeRepository nodeRepository;
    readonly ILogger<BlackListManagerLogger> logger;

    public Nodes(INodeRepository nodeRepository,
      IBitcoindFactory bitcoindFactory,
      IBackgroundJobs backgroundJobs,
      ILogger<BlackListManagerLogger> logger
    )
    {
      this.bitcoindFactory = bitcoindFactory ?? throw new ArgumentNullException(nameof(bitcoindFactory));
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
      this.logger = logger;
    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
      // Try to connect to node
      var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
      try
      {
        // try to call some method to test if connectivity parameters are correct
        _ = await bitcoind.GetBlockCountAsync();
      }
      catch (RpcException ex)
      {
        throw new BadRequestException($"The node was not added. Unable to connect to node {node.Host}:{node.Port}.", ex);
      }
      
      var createdNode = nodeRepository.CreateNode(node);
      if (createdNode != null)
      {
        // only if node was created/inserted we clear blacklist to make sure that node database is in sync with BM database
        // if node is already inserted we do nothing and return conflict (http code 409) to client
        await bitcoind.ClearBlacklistsAsync(true);        
        
        await backgroundJobs.StartPropagateFundsStatesAsync();
      }

      return createdNode;
    }

    public async Task<bool> UpdateNodeAsync(Node node)
    {
      // Try to connect to node
      var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
      try
      {
        // try to call some method to test if connectivity parameters are correct
        _ = await bitcoind.TestNodeConnectionAsync();
      }
      catch (RpcException ex)
      {
        throw new BadRequestException($"The node was not updated. Can not connect to node {node.Host}:{node.Port}.", ex);
      }

      return nodeRepository.UpdateNode(node);
    }

    public IEnumerable<Node> GetNodes()
    {
      return nodeRepository.GetNodes();
    }

    public Node GetNode(string id)
    {
      return nodeRepository.GetNode(id);
    }

    public int DeleteNode(string id)
    {
      return nodeRepository.DeleteNode(id);
    }
  }
}
