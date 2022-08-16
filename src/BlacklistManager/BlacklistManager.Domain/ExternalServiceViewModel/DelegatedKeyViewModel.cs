// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class DelegatedKeyViewModel
  {
    public string DocumentType { get; set; }

    public DateTime CreatedAt { get; set; }

    public string DelegatingPublicKey { get; set; }

    public string DelegatingPublicKeyAddress { get; set; }

    public string Purpose { get; set; }

    public List<string> DelegatedPublicKeys { get; set; }
  }
}
