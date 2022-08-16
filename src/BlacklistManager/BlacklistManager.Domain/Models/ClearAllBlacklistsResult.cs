// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class ClearAllBlacklistsResult
  {
    public long NumRemovedPolicy { get; set; }
    public long NumRemovedConsensus { get; set; }
  }
}
