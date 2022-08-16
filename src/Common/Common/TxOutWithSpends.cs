// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace Common
{
  public class TxOutWithSpends
  {
    public TxOut TxOut { get; set; }
    public IEnumerable<TxOut> SpentBy { get; set; }
  }
}
