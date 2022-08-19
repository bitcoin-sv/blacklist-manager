// Copyright (c) 2021 Bitcoin Association

using Common.BitcoinRpcClient.Responses;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Common.BitcoinRpcClient
{
  public interface IRpcClient
  {
    TimeSpan RequestTimeout { get; set; }
    int NumOfRetries { get; set; }

    Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null);
    async Task<RpcGetBlockHeader> GetBlockHeaderAsync(long height, CancellationToken? token = null)
    {
      return await GetBlockHeaderAsync(await GetBlockHashAsync(height));
    }

    Task<RpcGetChainTips[]> GetChainTipsAsync(CancellationToken? token = null);

    Task<RPCBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash, CancellationToken? token = null);
    Task<string> GetBlockHashAsync(long height, CancellationToken? token = null);

    Task<string> GetBestBlockHashAsync(CancellationToken? token = null);

    Task<RpcFrozenFunds> AddToPolicyBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null);
    Task<RpcFrozenFunds> AddToConsensusBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null);
    Task<RpcFrozenFunds> RemoveFromPolicyBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null);
    Task<RPCClearAllBlackLists> ClearBlacklistsAsync(Requests.RpcClearBlacklist clear, CancellationToken? token = null);
    Task<long> GetBlockCountAsync(CancellationToken? token = null);
    Task<RpcGetBlockWithTxIds> GetBlockWithTxIdsAsync(string blockHash, CancellationToken? token = null);
    Task<RpcGetBlock> GetBlockAsync(string blockHash, int verbosity, CancellationToken? token = null);
    Task<string> GetBlockHeaderAsHexAsync(string blockHash, CancellationToken? token = null);
    Task<RpcGetRawTransaction> GetRawTransactionAsync(string txId, CancellationToken? token = null);
    Task<string> GetRawTransactionAsHexAsync(string txId, CancellationToken? token = null);
    Task<RpcSendRawTransactions> SendRawTransactionsAsync((string transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors)[] transactions, CancellationToken? token = null);

    Task StopAsync(CancellationToken? token = null);
    Task<string> SendToAddressAsync(string address, double amount, CancellationToken? token = null);
    Task<byte[]> GetRawTransactionAsBytesAsync(string txId, CancellationToken? token = null);
    Task<string[]> GenerateAsync(int n, CancellationToken? token = null);
    Task<string[]> GenerateToAddressAsync(int n, string address, CancellationToken? token = null);
    Task<string> SubmitBlockAsync(byte[] block, CancellationToken? token = null);
    Task<RpcQueryBlacklist> QueryBlacklistAsync(CancellationToken? token = null);
    Task<RpcConfiscationResult> AddToConfiscationTxIdWhitelistAsync(Requests.RpcConfiscation confiscationData, CancellationToken? token = null);
    Task<string[]> GetRawMempoolAsync(CancellationToken? token = null);
    Task<RpcBlockChainInfo> GetBlockChainInfoAsync(CancellationToken? token = null);
    Task<RpcQueryConfWhitelist> QueryConfiscationTxidWhitelistAsync(CancellationToken? token = null);

    public Task<string> DumpPrivKeyAsync(string address);
    public Task<string> GetNewAddressAsync();
    public Task<string> AddNodeAsync(string host, int port, CancellationToken? token = null);
    public Task<string> DisconnectNodeAsync(string host, int port, CancellationToken? token = null);
    public Task<RpcGetPeerInfo[]> GetPeerInfoAsync(CancellationToken? token = null);
    Task<string> SignMessageAsync(string address, string message, CancellationToken? token = null);
    Task<string> SendRawTransactionAsync(string txHex, CancellationToken? token = null);
  }
}
