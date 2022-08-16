// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpc.Responses
{
  [Serializable]
  public partial class RpcGetRawTransaction
  {
    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("vin")]
    public Vin[] Vin { get; set; }

    [JsonPropertyName("vout")]
    public Vout[] Vout { get; set; }

    [JsonPropertyName("blockhash")]
    public string Blockhash { get; set; }

    [JsonPropertyName("blocktime")]
    public long? Blocktime { get; set; }

    [JsonPropertyName("blockheight")]
    public long? Blockheight { get; set; }
  }

  [Serializable]
  public partial class Vin
  {
    [JsonPropertyName("coinbase")]
    public string Coinbase { get; set; }
    
    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("vout")]
    public long Vout { get; set; }
  }

  public partial class ScriptSig
  {
    [JsonPropertyName("asm")]
    public string Asm { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }

  [Serializable]
  public partial class Vout
  {
    [JsonPropertyName("value")]
    public decimal Value { get; set; }

    [JsonPropertyName("n")]
    public long N { get; set; }

    [JsonPropertyName("scriptPubKey")]
    public ScriptPubKey ScriptPubKey { get; set; }
  }

  [Serializable]
  public partial class ScriptPubKey
  {
    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }
}
