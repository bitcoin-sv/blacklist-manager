// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public partial class RpcSendTransactionsRequestOne
  {
    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    [JsonPropertyName("allowhighfees")]
    public bool AllowHighFees { get; set; }

    [JsonPropertyName("dontcheckfee")]
    public bool DontCheckFee { get; set; }

    [JsonPropertyName("listunconfirmedancestors")]
    public bool ListUnconfirmedAncestors { get; set; }
  }


  public class RpcSendRawTransactions
  {
    [JsonPropertyName("known")]
    public string[] RpcKnownTxes { get; set; }

    [JsonPropertyName("evicted")]
    public RpcEvictedTx[] RpcEvictedTxes { get; set; }

    [JsonPropertyName("invalid")]
    public RpcInvalid[] RpcInvalids { get; set; }
  }

  public class RpcEvictedTx
  {
    [JsonPropertyName("txid")]
    public string TxId { get; set; }
  }

  public class RpcInvalid
  {
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("reject_code")]
    public int RejectCode { get; set; }

    [JsonPropertyName("reject_reason")]
    public string RejectReason { get; set; }

    [JsonPropertyName("collidedWith")]
    public RpcCollidedWith[] RpcCollidedWiths { get; set; }
  }

  public class RpcCollidedWith
  {
    [JsonPropertyName("txid")]
    public string TxId { get; set; }
    
    [JsonPropertyName("size")]
    public long Size { get; set; }
    
    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }
}
