// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Bitcoin
{
  public class BitcoinRpcBCH : BitcoinRpcCommon, IBitcoinRpc
  {
    public BitcoinRpcBCH(IRpcClient rpcClient, ILogger<BitcoinRpc> logger, Network network) : base(rpcClient, logger, network) { }

    public override async Task<GetBlock> GetBlockWithCoinbaseTransactionAsync(string blockHash, CancellationToken? token = null)
    {
      return await InternalGetBlockWithCoinbaseTransactionAsync(blockHash, 2, token);
    }

    public override Task<IEnumerable<SendRawTransactionsResult>> SendRawTransactionsAsync((string TxId, string Hex)[] transactions, CancellationToken? token = null)
    {
      throw new System.NotImplementedException();
    }
  }
}
