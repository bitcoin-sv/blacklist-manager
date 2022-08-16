// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.Domain.Models
{
  public class DelegatingKey
  {
    public int DelegatingKeyId { get; set; }
    public string PublicKeyAddress { get; set; }
    public string PublicKey { get; set; }
    public string DataToSign { get; set; }
    public string SignedDelegatedKeyJSON { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public int DelegatedKeyId { get; set; }
    public string DelegatedPublicKey { get; set; }
  }
}
