// Copyright (c) 2020 Bitcoin Association

using System;

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

    public int SignerId{ get; set; }
    public bool DelegationRequired { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }

  }
}
