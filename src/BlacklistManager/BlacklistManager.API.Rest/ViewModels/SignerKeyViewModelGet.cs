// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class SignerKeyViewModelGet
  {
    public SignerKeyViewModelGet() { }
    public SignerKeyViewModelGet(Domain.Models.DelegatedKey domainDelegatedKey)
    {
      SignerId = domainDelegatedKey.DelegatedKeyId;
      DelegationRequired = domainDelegatedKey.DelegationRequired;
      IsActive = domainDelegatedKey.IsActive;
      ActivatedAt = domainDelegatedKey.ActivatedAt;
      CreatedAt = domainDelegatedKey.CreatedAt;
    }

    [JsonPropertyName("signerId")]
    public int SignerId{ get; set; }
    [JsonPropertyName("delegationRequired")]
    public bool DelegationRequired { get; set; }
    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; }
    [JsonPropertyName("activatedAt")]
    public DateTime? ActivatedAt { get; set; }
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
  }
}
