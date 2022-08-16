// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class DelegatedKeysItemViewModel
  {
    public DocumentType DocumentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public string DelegatingPublicKey { get; set; }
  }
}
