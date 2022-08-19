// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class TrustListItemViewModelCreate
  {
    [Required]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("trusted")]
    public bool? Trusted { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }
  }
}
