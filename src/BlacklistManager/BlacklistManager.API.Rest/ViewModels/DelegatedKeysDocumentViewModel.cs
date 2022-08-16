// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class DelegatedKeysDocumentViewModel
  {
    [JsonPropertyName("documentType")]
    public DocumentType DocumentType { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("delegatingPublicKey")]
    public string DelegatingPublicKey { get; set; }

    [JsonPropertyName("delegatingPublicKeyAddress")]
    public string DelegatingPublicKeyAddress { get; set; }

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; }

    [JsonPropertyName("delegatedPublicKeys")]
    public string[] DelegatedPublicKeys { get; set; }
  }
}
