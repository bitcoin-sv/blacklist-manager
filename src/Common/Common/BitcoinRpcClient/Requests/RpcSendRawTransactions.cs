// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Requests
{
  public class RpcSendRawTransactions
  {
    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    [JsonPropertyName("allowhighfees")]
    public bool? AllowHighFees { get; set; }

    [JsonPropertyName("dontcheckfee")]
    public bool? DontCheckFee { get; set; }
  }
}
