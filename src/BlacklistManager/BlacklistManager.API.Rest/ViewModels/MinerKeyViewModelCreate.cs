// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class MinerKeyViewModelCreate
  {
    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; }
    [JsonPropertyName("publicKeyAddress")]
    public string PublicKeyAddress { get; set; }
  }
}
