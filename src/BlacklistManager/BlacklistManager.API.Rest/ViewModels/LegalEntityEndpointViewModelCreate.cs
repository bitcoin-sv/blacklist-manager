// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class LegalEntityEndpointViewModelCreate
  {
    [Required]
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    [Required]
    [JsonPropertyName("apiKey")]
    public string APIKey { get; set; }
  }
}
