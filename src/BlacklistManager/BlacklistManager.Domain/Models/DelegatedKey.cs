// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.Domain.Models
{
  public class DelegatedKey
  {
    public int DelegatedKeyId { get; set; }
    public byte[] PrivateKey { get; set; }
    public string PublicKey { get; set; }
    public bool DelegationRequired { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ActivatedAt { get; set; }
    public DateTime CreatedAt { get; set; }
  }
}
