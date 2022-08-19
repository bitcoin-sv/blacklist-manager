// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class FundQuery
  {
    public TxOut TxOut { get; set; }

    public string Status { get; set; }

    public List<EnforceAtHeightQuery> EnforceAtHeight { get; set; }

    public FundQuery(string txId, Int64 vout)
    {
      EnforceAtHeight = new List<EnforceAtHeightQuery>();
      TxOut = new TxOut(txId, vout);
    }

    public string GetKey()
    {
      return TxOut.TxId + TxOut.Vout;
    }
  }
}
