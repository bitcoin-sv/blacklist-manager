// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpc.Responses
{
  public class RpcFrozenFunds
  {
    [JsonPropertyName("notProcessed")]
    public List<RpcFund> Funds { get; set; }

    public class RpcFund
    {
      [JsonPropertyName("txOut")]
      public RpcTxOut TxOut { get; set; }

      [JsonPropertyName("reason")]
      public string Reason { get; set; }

      public class RpcTxOut
      {
        [JsonPropertyName("txId")]
        public string TxId { get; set; }

        [JsonPropertyName("vout")]
        public int Vout { get; set; }
      }
    }

  }

  public class RPCClearAllBlackLists
  {
    [JsonPropertyName("numRemovedPolicy")]
    public long NumRemovedPolicy { get; set; }
    [JsonPropertyName("numRemovedConsensus")]
    public long NumRemovedConsensus { get; set; }
  }
}
