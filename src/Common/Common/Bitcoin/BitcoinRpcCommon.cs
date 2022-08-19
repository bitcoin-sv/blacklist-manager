// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Bitcoin
{
  public abstract class BitcoinRpcCommon : IBitcoinRpc
  {
    protected Network _network;
    protected IRpcClient _rpcClient;
    protected readonly ILogger<BitcoinRpc> _logger;

    protected BitcoinRpcCommon(IRpcClient rpcClient, ILogger<BitcoinRpc> logger, Network network)
    {
      _logger = logger;
      _rpcClient = rpcClient;
      _network = network;
    }

    #region protected virtual methods

    protected virtual async Task<GetBlock> InternalGetBlockWithCoinbaseTransactionAsync(string blockHash, int blockVerbosity, CancellationToken? token = null)
    {
      if (blockVerbosity == 0 || blockVerbosity == 1)
      {
        throw new BadRequestException("'blockVerbosity' with values (0, 1) is not allowed");
      }

      var block = await _rpcClient.GetBlockAsync(blockHash, blockVerbosity, token);

      var cbTransaction = new TransactionItem
      {
        Blockhash = block.Hash,
        BlockHeight = block.Height,
        Time = block.Time,
        TxId = block.Tx.First().Txid,
        Vout = block.Tx.First().Vout.Select(y => new VOutItem
        {
          ScriptPubKey = new ScriptPubKey
          {
            Hex = y.ScriptPubKey.Hex
          }
        }).ToList()

      };
      return new GetBlock
      {
        Hash = block.Hash,
        Time = block.Time,
        Height = block.Height,
        PreviousBlockHash = block.Previousblockhash,
        Tx = new List<TransactionItem>(new TransactionItem[] { cbTransaction })
      };
    }
    #endregion

    public abstract Task<GetBlock> GetBlockWithCoinbaseTransactionAsync(string blockHash, CancellationToken? token = null);

    public abstract Task<IEnumerable<SendRawTransactionsResult>> SendRawTransactionsAsync((string TxId, string Hex)[] transactions, CancellationToken? token = null);

    public virtual async Task TestGetBlockCountAsync()
    {
      _rpcClient.RequestTimeout = TimeSpan.FromSeconds(10);
      _rpcClient.NumOfRetries = 1;

      await _rpcClient.GetBlockCountAsync();
    }

    public virtual async Task TestTxIndexEnabledAsync()
    {
      _rpcClient.RequestTimeout = TimeSpan.FromSeconds(10);
      _rpcClient.NumOfRetries = 1;

      var blockHash = await _rpcClient.GetBestBlockHashAsync();
      var block = await _rpcClient.GetBlockWithTxIdsAsync(blockHash);
      _ = await _rpcClient.GetRawTransactionAsHexAsync(block.Tx.First());
    }

    public virtual Task<string> GetBestBlockHashAsync(CancellationToken? token = null)
    {
      return _rpcClient.GetBestBlockHashAsync(token);
    }

    public virtual Task<long> GetBlockCountAsync(CancellationToken? token = null)
    {
      return _rpcClient.GetBlockCountAsync(token);
    }

    public virtual async Task<TransactionItem> GetTransactionAsync(string txId, long? height)
    {
      // Transactions that are still in mempool don't have blockheight 
      // so we cannot call getblockhash with height. Therefore we call 
      // getrawtransaction with verbosity set to true
      if (!height.HasValue)
      {
        var verboseTx = await _rpcClient.GetRawTransactionAsync(txId);

        return new TransactionItem
        {
          Blockhash = verboseTx.Blockhash,
          BlockHeight = verboseTx.Blockheight,
          Time = verboseTx.Blocktime,
          TxId = txId,
          VIn = verboseTx.Vin.Select(x => new VInItem
          {
            TxId = x.Txid,
            VOut = x.Vout
          }).ToList(),
          Vout = verboseTx.Vout.Select(x => new VOutItem
          {
            n = x.N,
            Value = Money.Coins(x.Value).Satoshi,
            ScriptPubKeyHash = HelperTools.ScriptPubKeyHexToHash(x.ScriptPubKey.Hex)
          }).ToList()
        };
      }
      else
      {
        long heightIn = height.Value;
        // Create 2 tasks so they can run in parallel
        IList<Task> taskList = new List<Task>();
        Task<string> taskGetRawTransaction = null;
        Task<BitcoinRpcClient.Responses.RpcGetBlockHeader> taskGetBlockHeader = null;

        if (!CacheClass.TxCache.TryGetValue(txId, out string txHex))
        {
          taskGetRawTransaction = _rpcClient.GetRawTransactionAsHexAsync(txId);
          taskList.Add(taskGetRawTransaction);
        }

        if (!CacheClass.BlockHeaderCache.TryGetValue(heightIn, out BlockHeaderItem blockHeader))
        {
          var blockHash = await _rpcClient.GetBlockHashAsync(heightIn);
          taskGetBlockHeader = _rpcClient.GetBlockHeaderAsync(blockHash);
          taskList.Add(taskGetBlockHeader);
        }

        await Task.WhenAll(taskList.ToArray());

        if (taskGetRawTransaction != null)
        {
          txHex = await taskGetRawTransaction;

          // Add transaction hex to cache for future use
          CacheClass.TxCache.TryAdd(txId, txHex);
        }

        if (taskGetBlockHeader != null)
        {
          var rpcBlockHeader = await taskGetBlockHeader;
          blockHeader = new BlockHeaderItem
          {
            Hash = new NBitcoin.uint256(rpcBlockHeader.Hash),
            Height = rpcBlockHeader.Height,
            Time = rpcBlockHeader.Time
          };

          // Add blockheader to cache for future use
          CacheClass.BlockHeaderCache.TryAdd(heightIn, blockHeader);
        }

        if (string.IsNullOrEmpty(txHex))
          throw new BadRequestException($"Transaction '{txId}' was not retrieved from bitcoind call to getrawtransaction.");

        if (blockHeader == null)
          throw new BadRequestException($"Blockheader data for height '{height}' was not retrieved from bitcoind.");

        var tx = Transaction.Parse(txHex, _network);
        return new TransactionItem
        {
          Blockhash = blockHeader.Hash.ToString(),
          BlockHeight = blockHeader.Height,
          Time = blockHeader.Time,
          TxId = txId,
          VIn = tx.Inputs.Select(x => new VInItem
          {
            TxId = x.PrevOut.Hash.ToString(),
            VOut = x.PrevOut.N
          }).ToList(),
          Vout = tx.Outputs.AsIndexedOutputs().Select(x => new VOutItem
          {
            n = x.N,
            Value = x.TxOut.Value.Satoshi,
            ScriptPubKeyHash = HelperTools.ScriptPubKeyHexToHash(x.TxOut.ScriptPubKey.ToHex())
          }).ToList()
        };
      }
    }

    public virtual async Task<GetChainTipsItem[]> GetChainTipsAsync(CancellationToken? token = null)
    {
      var chainTips = await _rpcClient.GetChainTipsAsync(token);
      return chainTips.Select(x => new GetChainTipsItem
                                  {
                                    BranchLen = x.BranchLen,
                                    Hash = x.Hash,
                                    Height = x.Height,
                                    Status = x.Status
                                  }).ToArray();
    }

    public virtual async Task<BlockHeaderItem> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null)
    {
      var blockHeader = await _rpcClient.GetBlockHeaderAsync(blockHash, token);
      return new BlockHeaderItem
      {
        Hash = new uint256(blockHash),
        Height = blockHeader.Height,
        Previousblockhash = blockHeader.Previousblockhash,
        Time = blockHeader.Time
      };
    }

    public virtual async Task<BlockHeaderItem> GetBlockHeaderAsync(long height, CancellationToken? token = null)
    {
      var blockHeader = await _rpcClient.GetBlockHeaderAsync(height, token);
      return new BlockHeaderItem
      {
        Hash = new uint256(blockHeader.Hash),
        Height = blockHeader.Height,
        Previousblockhash = blockHeader.Previousblockhash,
        Time = blockHeader.Time
      };
    }

    public virtual Task AddToPolicyBlacklistAsync(BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      return _rpcClient.AddToPolicyBlacklistAsync(funds, token);
    }

    public virtual Task AddToConsensusBlacklistAsync(BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      return _rpcClient.AddToConsensusBlacklistAsync(funds, token);
    }

    public virtual Task RemoveFromPolicyBlacklistAsync(BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      return _rpcClient.RemoveFromPolicyBlacklistAsync(funds, token);
    }

    public virtual Task ClearBlacklistsAsync(bool removeAllEntries, int? expirationHeightDelta = null, CancellationToken? token = null)
    {
      return _rpcClient.ClearBlacklistsAsync(new BitcoinRpcClient.Requests.RpcClearBlacklist(removeAllEntries, expirationHeightDelta), token);
    }

    public virtual Task<BitcoinRpcClient.Responses.RpcConfiscationResult> AddToConfiscationTxIdWhiteListAsync(BitcoinRpcClient.Requests.RpcConfiscation confiscation, CancellationToken? token = null)
    {
      return _rpcClient.AddToConfiscationTxIdWhitelistAsync(confiscation, token);
    }

    public virtual Task<string[]> GetRawMempoolAsync()
    {
      return _rpcClient.GetRawMempoolAsync();
    }

    public virtual Task<BitcoinRpcClient.Responses.RpcQueryConfWhitelist> QueryConfiscationTxidWhitelistAsync(CancellationToken? token = null)
    {
      return _rpcClient.QueryConfiscationTxidWhitelistAsync(token);
    }
  }
}
