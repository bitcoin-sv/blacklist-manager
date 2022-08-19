// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Requests
{
  public class RpcConfiscationTxData
  {
    [JsonPropertyName("txId")]
    public string TxId { get; set; }
    [JsonPropertyName("enforceAtHeight")]
    public int EnforceAtHeight { get; set; }
    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }

  public class RpcConfiscationTx
  {
    [JsonPropertyName("confiscationTx")]
    public RpcConfiscationTxData ConfiscationTxData { get; set; }
  }

  public class RpcConfiscation
  {
    [JsonPropertyName("confiscationTxs")]
    public IList<RpcConfiscationTx> ConfiscationTxs { get; set; }
  }
}
