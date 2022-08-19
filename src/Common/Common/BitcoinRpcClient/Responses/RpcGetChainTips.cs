// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public class RpcGetChainTips
  {
    [JsonPropertyName("height")]
    public long Height { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("branchlen")]
    public long BranchLen { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }
  }
}
