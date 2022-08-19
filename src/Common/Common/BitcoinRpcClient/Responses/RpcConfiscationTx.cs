// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient.Requests;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public class RpcConfiscationTx
  {
    [JsonPropertyName("confiscationTx")]
    public RpcConfiscationTxData ConfiscationTx { get; set; }
    [JsonPropertyName("reason")]
    public string Reason { get; set; }
  }

  public class RpcConfiscationResult
  {
    [JsonPropertyName("notProcessed")]
    public IList<RpcConfiscationTx> NotProcessed { get; set; }
  }
}
