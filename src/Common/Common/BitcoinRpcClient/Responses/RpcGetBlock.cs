// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public class RpcGetBlockWithTxIds
  {
    [JsonPropertyName("tx")]
    public List<string> Tx { get; set; }
    [JsonPropertyName("hash")]
    public string Hash { get; set; }
    [JsonPropertyName("confirmations")]
    public long Confirmations { get; set; }
    [JsonPropertyName("size")]
    public long Size { get; set; }
    [JsonPropertyName("height")]
    public long Height { get; set; }
    [JsonPropertyName("version")]
    public long Version { get; set; }
    [JsonPropertyName("versionHex")]
    public string VersionHex { get; set; }
    [JsonPropertyName("merkleroot")]
    public string Merkleroot { get; set; }
    [JsonPropertyName("num_tx")]
    public long NumTx { get; set; }
    [JsonPropertyName("time")]
    public long Time { get; set; }
    [JsonPropertyName("mediantime")]
    public long Mediantime { get; set; }
    [JsonPropertyName("nonce")]
    public long Nonce { get; set; }
    [JsonPropertyName("bits")]
    public string Bits { get; set; }
    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; }
    [JsonPropertyName("chainwork")]
    public string Chainwork { get; set; }
    [JsonPropertyName("previousblockhash")]
    public string Previousblockhash { get; set; }

  }
  public partial class RpcGetBlock
  {
    [JsonPropertyName("tx")]
    public RpcTx[] Tx { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("confirmations")]
    public long Confirmations { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("height")]
    public long Height { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("versionHex")]
    public string VersionHex { get; set; }

    [JsonPropertyName("merkleroot")]
    public string Merkleroot { get; set; }

    [JsonPropertyName("num_tx")]
    public long NumTx { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("mediantime")]
    public long Mediantime { get; set; }

    [JsonPropertyName("nonce")]
    public long Nonce { get; set; }

    [JsonPropertyName("bits")]
    public string Bits { get; set; }

    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; }

    [JsonPropertyName("chainwork")]
    public string Chainwork { get; set; }

    [JsonPropertyName("previousblockhash")]
    public string Previousblockhash { get; set; }

    [JsonPropertyName("nextblockhash")]
    public string Nextblockhash { get; set; }
  }

  [Serializable]
  public partial class RpcTx
  {
    [JsonPropertyName("txid")]
    public string Txid { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("locktime")]
    public long Locktime { get; set; }

    [JsonPropertyName("vin")]
    public RpcVin[] Vin { get; set; }

    [JsonPropertyName("vout")]
    public RpcVout[] Vout { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }

  [Serializable]
  public partial class RpcVin
  {
    [JsonPropertyName("coinbase")]
    public string Coinbase { get; set; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }
  }

  [Serializable]
  public partial class RpcVout
  {
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("n")]
    public long N { get; set; }

    [JsonPropertyName("scriptPubKey")]
    public RpcScriptPubKey ScriptPubKey { get; set; }
  }

  [Serializable]
  public partial class RpcScriptPubKey
  {
    [JsonPropertyName("asm")]
    public string Asm { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    [JsonPropertyName("reqSigs")]
    public long ReqSigs { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("addresses")]
    public string[] Addresses { get; set; }
  }
}
