// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.Bitcoin;
using Common.BitcoinRpcClient;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.Infrastructure.Actions
{
  public class Nodes : INodes
  {
    readonly IBitcoinFactory _bitcoinFactory;
    readonly IBackgroundJobs _backgroundJobs;

    readonly INodeRepository _nodeRepository;
    readonly ILogger<BlackListManagerLogger> _logger;

    public Nodes(INodeRepository nodeRepository,
      IBitcoinFactory bitcoinFactory,
      IBackgroundJobs backgroundJobs,
      ILogger<BlackListManagerLogger> logger
    )
    {
      _bitcoinFactory = bitcoinFactory ?? throw new ArgumentNullException(nameof(bitcoinFactory));
      _nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      _backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
      _logger = logger;
    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
      // Try to connect to node
      var bitcoind = _bitcoinFactory.Create(node.Host, node.Port, node.Username, node.Password);
      try
      {
        // try to call some method to test if connectivity parameters are correct
        _ = await bitcoind.GetBlockCountAsync();
      }
      catch (RpcException ex)
      {
        throw new BadRequestException($"The node was not added. Unable to connect to node {node.Host}:{node.Port}.", ex);
      }
      
      var createdNode = await _nodeRepository.CreateNodeAsync(node);
      if (createdNode != null)
      {
        // only if node was created/inserted we clear blacklist to make sure that node database is in sync with BM database
        // if node is already inserted we do nothing and return conflict (http code 409) to client
        await bitcoind.ClearBlacklistsAsync(true);

        await Task.WhenAll(_backgroundJobs.StartPropagateFundsStatesAsync(),
                           _backgroundJobs.StartSubmitWhitelistTxIdsAsync(),
                           _backgroundJobs.StartProcessCourtOrderAcceptancesAsync());
      }

      return createdNode;
    }

    public async Task<bool> UpdateNodeAsync(Node node)
    {
      // Try to connect to node
      var bitcoind = _bitcoinFactory.Create(node.Host, node.Port, node.Username, node.Password);
      try
      {
        // try to call some method to test if connectivity parameters are correct
        await RetryUtils.ExecuteWithRetriesAsync(5, null, () => bitcoind.GetBlockCountAsync(), 3000);
      }
      catch (Exception ex)
      {
        throw new BadRequestException($"The node was not updated. Can not connect to node {node.Host}:{node.Port}.", ex);
      }

      return await _nodeRepository.UpdateNodeAsync(node);
    }

    public Task<IEnumerable<Node>> GetNodesAsync()
    {
      return _nodeRepository.GetNodesAsync();
    }

    public Task<Node> GetNodeAsync(string id)
    {
      return _nodeRepository.GetNodeAsync(id);
    }

    public Task<int> DeleteNodeAsync(string id)
    {
      return _nodeRepository.DeleteNodeAsync(id);
    }
  }
}
