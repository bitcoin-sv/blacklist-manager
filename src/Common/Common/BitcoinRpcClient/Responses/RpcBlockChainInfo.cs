// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient.Responses
{
  public class RpcBlockChainInfo
  {
    [JsonPropertyName("chain")]
    public string Chain { get; set; }

    [JsonPropertyName("blocks")]
    public int Blocks { get; set; }

    [JsonPropertyName("headers")]
    public int Headers { get; set; }

    [JsonPropertyName("bestblockhash")]
    public string Bestblockhash { get; set; }

    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; }

    [JsonPropertyName("mediantime")]
    public int Mediantime { get; set; }

    [JsonPropertyName("verificationprogress")]
    public double Verificationprogress { get; set; }

    [JsonPropertyName("chainwork")]
    public string Chainwork { get; set; }

    [JsonPropertyName("pruned")]
    public bool Pruned { get; set; }
  }
}
