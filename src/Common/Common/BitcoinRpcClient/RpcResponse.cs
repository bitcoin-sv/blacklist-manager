// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient.Responses;
using System.Text.Json.Serialization;

namespace Common.BitcoinRpcClient
{
  public class RpcResponse<T>
  {
    [JsonPropertyName("result")]
    public T Result { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("error")]
    public RpcError Error { get; set; }      
  }
}
