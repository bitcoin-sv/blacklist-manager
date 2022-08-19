// Copyright (c) 2021 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class TransactionToSend
  {
    public string TxId { get; set; }
    public byte[] Body { get; set; }
    public int EnforceAtHeight { get; set; }
    public bool RewardTransaction { get; set; }
  }
}
