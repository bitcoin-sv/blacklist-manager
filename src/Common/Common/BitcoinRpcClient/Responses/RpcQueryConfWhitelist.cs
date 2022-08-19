// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public class RpcQueryConfWhitelist
  {
    [JsonPropertyName("confiscationTxs")]
    public List<ConfiscationItem> ConfiscationTxs { get; set; }
  }

  public class ConfiscationItem
  {
    [JsonPropertyName("confiscationTx")]
    public ConfiscationTx ConfiscationTx { get; set; }
  }

  public class ConfiscationTx
  {
    [JsonPropertyName("txId")]
    public string TxId { get; set; }

    [JsonPropertyName("enforceAtHeight")]
    public int EnforceAtHeight { get; set; }
  }
}
