// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class EnforceAtHeightQuery
  {
    public string CourtOrderHash { get; set; }

    public string CourtOrderHashUnfreeze { get; set; }

    public int? StartEnforceAtHeight { get; set; }

    public int? StopEnforceAtHeight { get; set; }
  }
}
