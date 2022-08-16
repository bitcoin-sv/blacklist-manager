// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace Common
{
  public class TxOutOfAddress
  {
    public string Address { get; set; }
    public IEnumerable<TxOut> TxOuts { get; set; }
  }
}
