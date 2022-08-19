// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class TransactionStatus
  {
    public string TransactionId { get; init; }
    public int EnforceAtHeight { get; init; }
    public int SubmittedAtHeight { get; init; }
    public int LastErrorAtHeight { get; init; }
    public int LastErrorCode { get; init; }
    public string LastError { get; init; }
  }
}
