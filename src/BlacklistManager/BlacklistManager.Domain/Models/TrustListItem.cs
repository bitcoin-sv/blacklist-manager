// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.Domain.Models
{
  public class TrustListItem
  {
    public string PublicKey { get; set; }
    public bool Trusted { get; set; }

    public string Remarks { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdateAt { get; set; }
    public string ReplacedBy { get; set; }
  }
}
