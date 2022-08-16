// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class LegalEntityEndpointViewModelGet
  {
    public LegalEntityEndpointViewModelGet()
    {
    }

    public LegalEntityEndpointViewModelGet(LegalEntityEndpoint lee)
    {
      Id = lee.LegalEntityEndpointId;
      BaseUrl = lee.BaseUrl;
      APIKey = lee.APIKey;
      ValidUntil = lee.ValidUntil;
      CreatedAt = lee.CreatedAt;
      LastContactedAt = lee.LastContactedAt;
      LastErrorAt = lee.LastErrorAt;
      LastError = lee.LastError;
    }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    [JsonPropertyName("apiKey")]
    public string APIKey { get; set; }

    [JsonPropertyName("validUntil")]
    public DateTime? ValidUntil { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("lastContactedAt")]
    public DateTime? LastContactedAt { get; set; }

    [JsonPropertyName("lastErrorAt")]
    public DateTime? LastErrorAt { get; set; }

    [JsonPropertyName("lastError")]
    public string LastError { get; set; }
  }
}
