// Copyright (c) 2020 Bitcoin Association

using System;

namespace Common
{
  public class TxOut
  {
    public string TxId { get; set; }
    public long n { get; set; }
    public DateTime? BlockTime { get; set; }
    public string BlockHash { get; set; }
    public long? BlockHeight { get; set; }

    public override string ToString()
    {
      return $"{TxId}-{n}";
    }
  }
}
