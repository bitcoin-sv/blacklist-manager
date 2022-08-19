// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Bitcoin
{
  public interface IBitcoinRpc
  {

    /// <summary>
    /// Retrieves transaction from blockchain. If height is supplied transaction is first checked against cache
    /// and if not found it is retrieved in HEX format from bitcoind and then deserialized to object.
    /// </summary>
    Task<TransactionItem> GetTransactionAsync(string txId, long? height);

    Task TestGetBlockCountAsync();

    Task TestTxIndexEnabledAsync();

    Task<long> GetBlockCountAsync(CancellationToken? token = null);

    Task<string> GetBestBlockHashAsync(CancellationToken? token = null);

    Task<GetBlock> GetBlockWithCoinbaseTransactionAsync(string blockHash, CancellationToken? token = null);

    Task<GetChainTipsItem[]> GetChainTipsAsync(CancellationToken? token = null);

    Task<BlockHeaderItem> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null);

    Task<BlockHeaderItem> GetBlockHeaderAsync(long height, CancellationToken? token = null);

    Task AddToPolicyBlacklistAsync(BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null);

    Task AddToConsensusBlacklistAsync(BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null);

    Task RemoveFromPolicyBlacklistAsync(BitcoinRpcClient.Requests.RpcFrozenFunds funds, CancellationToken? token = null);

    Task ClearBlacklistsAsync(bool removeAllEntries, int? expirationHeightDelta = null, CancellationToken? token = null);

    Task<IEnumerable<SendRawTransactionsResult>> SendRawTransactionsAsync((string TxId, string Hex)[] transactions, CancellationToken? token = null);

    Task<BitcoinRpcClient.Responses.RpcConfiscationResult> AddToConfiscationTxIdWhiteListAsync(BitcoinRpcClient.Requests.RpcConfiscation confiscation, CancellationToken? token = null);

    Task<string[]> GetRawMempoolAsync();

    Task<BitcoinRpcClient.Responses.RpcQueryConfWhitelist> QueryConfiscationTxidWhitelistAsync(CancellationToken? token = null);

    protected static Uri CreateAddress(string host, int port)
    {
      UriBuilder builder = new UriBuilder
      {
        Host = host,
        Scheme = "http",
        Port = port
      };
      return builder.Uri;
    }
  }
}
