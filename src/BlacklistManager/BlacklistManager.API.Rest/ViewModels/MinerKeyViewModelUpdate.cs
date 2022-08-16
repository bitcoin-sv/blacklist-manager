// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class MinerKeyViewModelUpdate
  {
    [Required]
    [JsonPropertyName("id")]
    public int Id { get; set; }
    [Required]
    [JsonPropertyName("signature")]
    public string Signature { get; set; }
    [Required]
    [JsonPropertyName("activateKey")]
    public bool ActivateKey { get; set; }
  }
}
