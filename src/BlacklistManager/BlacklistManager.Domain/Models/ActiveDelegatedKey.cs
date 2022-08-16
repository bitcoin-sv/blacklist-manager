// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class ActiveDelegatingKey
  {
    public bool DelegationRequired { get; set; }
    public string DelegatedPublicKey { get; set; }
    public byte[] DelegatedPrivateKey { get; set; }
    public string[] SignedDelegatedKeyJSON { get; set; }
  }
}
