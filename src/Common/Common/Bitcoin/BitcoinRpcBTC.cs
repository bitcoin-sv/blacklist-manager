// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Bitcoin
{
  public class BitcoinRpcBTC : BitcoinRpcCommon
  {
    public BitcoinRpcBTC(IRpcClient rpcClient, ILogger<BitcoinRpc> logger, Network network) : base(rpcClient, logger, network) { }

    public override async Task<GetBlock> GetBlockWithCoinbaseTransactionAsync(string blockHash, CancellationToken? token = null)
    {
      return await InternalGetBlockWithCoinbaseTransactionAsync(blockHash, 2, token);
    }

    public override async Task<IEnumerable<SendRawTransactionsResult>> SendRawTransactionsAsync((string TxId, string Hex)[] transactions, CancellationToken? token = null)
    {
      var result = new List<SendRawTransactionsResult>();
      foreach(var tx in transactions)
      {
        try
        {
          var response = await _rpcClient.SendRawTransactionAsync(tx.Hex);
        }
        catch (RpcException ex)
        {
          result.Add(new SendRawTransactionsResult
          {
            TxId = tx.TxId,
            ErrorDescription = ex.Message,
          });
        }
      }

      return result;
    }
  }
}
