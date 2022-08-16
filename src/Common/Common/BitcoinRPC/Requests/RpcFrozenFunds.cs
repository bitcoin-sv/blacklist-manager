// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpc.Requests
{
  public class RpcFrozenFunds
  {
    [JsonPropertyName("funds")]
    public IEnumerable<RpcFund> Funds { get; set; }

    public class RpcFund
    {
      [JsonPropertyName("txOut")]
      public RpcTxOut TxOut { get; set; }

      [JsonPropertyName("enforceAtHeight")]
      public IEnumerable<RpcEnforceAtHeight> EnforceAtHeight { get; set; }

      [JsonPropertyName("policyExpiresWithConsensus")]
      public bool? PolicyExpiresWithConsensus { get; set; }

      public class RpcTxOut
      {
        [JsonPropertyName("txId")]
        public string TxId { get; set; }

        [JsonPropertyName("vout")]
        public long Vout { get; set; }
      }

      public class RpcEnforceAtHeight
      {
        public RpcEnforceAtHeight(int start, int stop)
        {
          if (start != -1)
          {
            Start = start;
          }
          if (stop != -1)
          {
            Stop = stop;
          }
        }

        [JsonPropertyName("start")]
        public int? Start { get; set; }
        [JsonPropertyName("stop")]
        public int? Stop { get; set; }
      }
    }    
  }
}
