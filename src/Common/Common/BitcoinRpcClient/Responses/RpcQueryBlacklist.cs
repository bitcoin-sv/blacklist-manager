// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public class RpcQueryBlacklist
  {
    [JsonPropertyName("funds")]
    public RpcQueryFund[] Funds { get; set; }
  }

  public class RpcQueryFund
  {
    [JsonPropertyName("txOut")]
    public RpcTxOut TxOut { get; set; }

    [JsonPropertyName("enforceAtHeight")]
    public RpcEnforceAtHeight[] EnforceAtHeight { get; set; }

    [JsonConverter(typeof(NullableBoolOrIntConverter))]
    [JsonPropertyName("policyExpiresWithConsensus")]
    public int? PolicyExpiresWithConsensus { get; set; }

    [JsonPropertyName("blacklist")]
    public string[] Blacklist { get; set; }
  }

  public class RpcEnforceAtHeight
  {
    [JsonPropertyName("start")]
    public int Start { get; set; }
    [JsonPropertyName("stop")]
    public int Stop { get; set; }
  }
}
