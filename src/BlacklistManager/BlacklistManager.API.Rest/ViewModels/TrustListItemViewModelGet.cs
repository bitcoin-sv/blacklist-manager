// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class TrustListItemViewModelGet
  {
    public TrustListItemViewModelGet()
    {
    }

    public TrustListItemViewModelGet(Domain.Models.TrustListItem domain)
    {
      Id = domain.PublicKey;
      Trusted = domain.Trusted;
      Remarks = domain.Remarks;
      CreatedAt = domain.CreatedAt;
      UpdatedAt = domain.UpdateAt;
      ReplacedBy = domain.ReplacedBy;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("trusted")]
    public bool? Trusted { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("replacedBy")]
    public string ReplacedBy { get; set; }
  }
}