// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Common.Bitcoin
{
  public class BitcoinRpcBSV : BitcoinRpcCommon
  {
    public BitcoinRpcBSV(IRpcClient rpcClient, ILogger<BitcoinRpc> logger, Network network) : base(rpcClient, logger, network) { }

    public override async Task<GetBlock> GetBlockWithCoinbaseTransactionAsync(string blockHash, CancellationToken? token = null)
    {
      return await InternalGetBlockWithCoinbaseTransactionAsync(blockHash, 3, token);
    }

    public override async Task<IEnumerable<SendRawTransactionsResult>> SendRawTransactionsAsync((string TxId, string Hex)[] transactions, CancellationToken? token = null)
    {
      List<SendRawTransactionsResult> knownTxs = new();
      var txs = transactions.Select(x => (x.Hex, false, false, false)).ToArray();
      var resp = await _rpcClient.SendRawTransactionsAsync(txs, token);
      if (resp.RpcKnownTxes != null)
      {
        knownTxs.AddRange(resp.RpcKnownTxes.Select(x => new SendRawTransactionsResult { TxId = x, ErrorCode = (int)SendTransactionErrorCode.AlreadyKnown, ErrorDescription = "Transaction already known." }));
      }
      var knownTxsResp = resp.RpcInvalids?.Where(x => x.RejectReason == "txn-already-known");
      if (knownTxsResp != null)
      {
        knownTxs.AddRange(knownTxsResp.Select(x => new SendRawTransactionsResult { TxId = x.TxId, ErrorCode = (int)SendTransactionErrorCode.AlreadyKnown, ErrorDescription = "Transaction already known." }));
      }
      var evictedTxs = resp.RpcEvictedTxes?.Select(x => new SendRawTransactionsResult { TxId = x.TxId, ErrorCode = (int)SendTransactionErrorCode.Evicted, ErrorDescription = "Transaction accepted by the mempool and then evicted due to insufficient fee." });
      var missingInputsResp = resp.RpcInvalids?.Where(x => x.RejectReason.Contains("Missing inputs", System.StringComparison.OrdinalIgnoreCase));
      var missingInputsTxs = missingInputsResp?.Select(x => new SendRawTransactionsResult { TxId = x.TxId, ErrorCode = (int)SendTransactionErrorCode.MissingInputs, ErrorDescription = x.RejectReason });
      var invalidTxs = resp.RpcInvalids?.Except(missingInputsResp)
                                        .Except(knownTxsResp)
                                        .Select(x => new SendRawTransactionsResult { TxId = x.TxId, ErrorCode = x.RejectCode, ErrorDescription = x.RejectReason });

      IEnumerable<SendRawTransactionsResult> result = new List<SendRawTransactionsResult>();

      if (knownTxs != null)
      {
        result = result.Concat(knownTxs);
      }
      if (evictedTxs != null)
      {
        result = result.Concat(evictedTxs);
      }
      if (invalidTxs != null)
      {
        result = result.Concat(invalidTxs);
      }
      if (missingInputsTxs != null)
      {
        result = result.Concat(missingInputsTxs);
      }
      return result;
    }
  }
}
