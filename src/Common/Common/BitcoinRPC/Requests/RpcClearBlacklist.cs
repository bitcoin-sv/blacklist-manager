// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace Common.BitcoinRpc.Requests
{
  public class RpcClearBlacklist
  {
    public RpcClearBlacklist(bool removeAllEntries, int? expirationHeightDelta)
    {
      RemoveAllEntries = removeAllEntries;
      ExpirationHeightDelta = expirationHeightDelta;
    }

    [JsonPropertyName("removeAllEntries")]
    public bool RemoveAllEntries { get; set; }

    [JsonPropertyName("expirationHeightDelta")]
    public int? ExpirationHeightDelta { get; set; }
  }
}
