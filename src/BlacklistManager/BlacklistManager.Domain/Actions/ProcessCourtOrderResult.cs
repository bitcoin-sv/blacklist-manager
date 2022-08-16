// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Actions
{
  public class ProcessCourtOrderResult
  {
    public string CourtOrderHash { get; private set; }
    public bool AlreadyImported { get; private set; }

    public ProcessCourtOrderResult(string courtOrderHash, bool alreadyImported)
    {
      this.CourtOrderHash = courtOrderHash;
      this.AlreadyImported = alreadyImported;
    }
  }
}
