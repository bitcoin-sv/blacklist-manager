// Copyright (c) 2020 Bitcoin Association

namespace Common
{
  public class TxOutWithBlockHeight
  {
    public string TxId { get; set; }
    public long n { get; set; }
    public long? BlockHeight { get; set; }
  }
}
