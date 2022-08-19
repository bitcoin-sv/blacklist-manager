// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;

namespace BlacklistManager.Domain.Models
{
  public class ConfiscationEnvelope
  {

    public DocumentType DocumentType { get; init; }

    public string ConfiscationCourtOrder { get; set; }

    public ConfiscationTxDocument ConfiscationTxDocument { get; set; }
  
    public string[] ChainedTransactions { get; init; }
    
    public DateTime SignedDate { get; set; }
  }
}
