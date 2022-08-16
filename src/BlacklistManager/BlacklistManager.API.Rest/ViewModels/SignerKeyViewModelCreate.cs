// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class SignerKeyViewModelCreate
  {
    [Required]
    [JsonPropertyName("privateKey")]
    public string PrivateKey { get; set; }
    [JsonPropertyName("delegationRequired")]
    public bool DelegationRequired { get; set; }
  }
}
