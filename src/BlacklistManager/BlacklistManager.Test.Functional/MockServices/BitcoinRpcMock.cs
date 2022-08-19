// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Common.BitcoinRpcClient.Responses;
using Common.Bitcoin;
using Common.BitcoinRpcClient.Requests;
using Common.BitcoinRpcClient;

namespace BlacklistManager.Test.Functional.MockServices
{
  //TODO: If possible this should be unified with similar class in NT tests
  public class BitcoinRpcMock : IBitcoinRpc
  {
    private readonly string _nodeId;
    private readonly ILogger _logger;
    private readonly SortedSet<string> _doNotTraceMethods;
    private readonly AutoResetEvent _propagationSyncWaitForPropagation = new AutoResetEvent(false);
    private readonly int? _cancellOnPropagationIndex = null;
    private readonly BitcoindCallList _callList = null;
    private readonly SortedSet<string> _disconnectedNodes = null;

    public NBitcoin.Network Network => throw new NotImplementedException();

    public event Action<string> OnError;

    public BitcoinRpcMock(
      string nodeId, 
      ILogger logger, 
      BitcoindCallList callList,
      AutoResetEvent propagationSyncWaitForPropagation,
      int? cancellOnPropagationIndex,
      SortedSet<string> doNotTraceMethods,
      SortedSet<string> disconnectedNodes)
    {
      _nodeId = nodeId;
      _logger = logger;
      _callList = callList;
      _propagationSyncWaitForPropagation = propagationSyncWaitForPropagation;
      _cancellOnPropagationIndex = cancellOnPropagationIndex;
      _doNotTraceMethods = doNotTraceMethods;
      _disconnectedNodes = disconnectedNodes;
    }

    public void ThrowIfDisconnected(string methodName)
    {
      if (_disconnectedNodes.Contains(_nodeId))
      {
        OnError?.Invoke(methodName);

        // use RpcException, since this is what is handled by FundPropagator
        throw new RpcException(1, $"Node '{_nodeId}' can not be reached (simulating error)");
      }

    }

    public void ThrowIfDisconnected()
    {
      if (_disconnectedNodes.Contains(_nodeId))
      {
        OnError?.Invoke(_nodeId);

        // use RpcException, since this is what is handled by FundPropagator
        throw new RpcException(1, $"Node '{_nodeId}' can not be reached (simulating error)"); 
      }
    }

    private void IfRequestedWaitOnPropagationCancelation(CancellationToken? cancellationToken)
    {
      if (_cancellOnPropagationIndex.HasValue && _callList?.AllPropagationCalls.Count == _cancellOnPropagationIndex.Value && cancellationToken.HasValue)
      {
        _propagationSyncWaitForPropagation.Set();
        _logger.LogDebug("Waiting until cancellationToken.IsCancellationRequested");
        Utils.WaitUntil(() => cancellationToken.Value.IsCancellationRequested);
      }
    }

    public Task AddToConsensusBlacklistAsync(Common.BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      ThrowIfDisconnected();
      if (!_doNotTraceMethods.Contains(BitcoindCallList.Methods.AddToConsensus))
      {
        _callList?.AddToConsensusCall(_nodeId, funds);
      }
      IfRequestedWaitOnPropagationCancelation(token);
      return Task.CompletedTask;
    }

    public Task AddToPolicyBlacklistAsync(Common.BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      ThrowIfDisconnected(nameof(AddToPolicyBlacklistAsync));
      if (!_doNotTraceMethods.Contains(BitcoindCallList.Methods.AddToPolicy))
      {
        _callList?.AddToPolicyCall(_nodeId, funds);
      }
      IfRequestedWaitOnPropagationCancelation(token);
      return Task.CompletedTask;
    }
    
    public Task RemoveFromPolicyBlacklistAsync(Common.BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      ThrowIfDisconnected();
      _callList?.RemoveFromPolicyCall(_nodeId, funds);
      IfRequestedWaitOnPropagationCancelation(token);
      return Task.CompletedTask;
    }

    public Task ClearBlacklistsAsync(bool removeAllEntries, int? expirationHeightDelta = null, CancellationToken? token = null)
    {
      ThrowIfDisconnected();
      if (!_doNotTraceMethods.Contains(BitcoindCallList.Methods.ClearAllBlacklists))
      {
        _callList?.ClearAllBlacklistsCall(_nodeId);
      }
      return Task.CompletedTask;
    }

    public Task<long> GetBlockCountAsync(CancellationToken? token)
    {
      ThrowIfDisconnected();
      if (!_doNotTraceMethods.Contains(BitcoindCallList.Methods.GetBlockCount))
      {
        _callList?.GetBlockCountCall(_nodeId);
      }

      return Task.FromResult(1000L);
    }

    public Task<long> TestNodeConnectionAsync()
    {
      ThrowIfDisconnected();
      if (!_doNotTraceMethods.Contains(BitcoindCallList.Methods.GetBlockCount))
      {
        _callList?.GetBlockCountCall(_nodeId);
      }

      return Task.FromResult(1000L);
    }

    public Task<string> GetBestBlockHashAsync(CancellationToken? token)
    {
      return Task.FromResult("000000000000000003ab604cf99c47ffe2a81c530788773281873b6890274fe1");
    }

    public Task<IEnumerable<SendRawTransactionsResult>> SendRawTransactionsAsync(string[] hexStrings)
    {
      throw new NotImplementedException();
    }

    public Task<Common.BitcoinRpcClient.Responses.RpcConfiscationResult> AddToConfiscationTxIdWhiteListAsync(IEnumerable<TransactionToSend> confiscationTxs)
    {
      throw new NotImplementedException();
    }

    public Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash)
    {
      throw new NotImplementedException();
    }

    public Task<TransactionItem> GetTransactionAsync(string txId, long? height)
    {
      throw new NotImplementedException();
    }

    public Task TestGetBlockCountAsync()
    {
      throw new NotImplementedException();
    }

    public Task TestTxIndexEnabledAsync()
    {
      throw new NotImplementedException();
    }

    public Task<GetBlock> GetBlockWithCoinbaseTransactionAsync(string blockHash, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<GetChainTipsItem[]> GetChainTipsAsync(CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<BlockHeaderItem> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<IEnumerable<SendRawTransactionsResult>> SendRawTransactionsAsync((string TxId, string Hex)[] transactions, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<RpcConfiscationResult> AddToConfiscationTxIdWhiteListAsync(RpcConfiscation confiscation, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<string[]> GetRawMempoolAsync()
    {
      throw new NotImplementedException();
    }

    public Task<RpcQueryConfWhitelist> QueryConfiscationTxidWhitelistAsync(CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }

    public Task<BlockHeaderItem> GetBlockHeaderAsync(long height, CancellationToken? token = null)
    {
      throw new NotImplementedException();
    }
  }
}
