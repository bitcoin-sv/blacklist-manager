// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public enum CourtOrderStatus
  {
    // Freeze order:
    FreezePolicy = 100,
    FreezeConsensus = 101,

    //Unfreeze order:
    UnfreezeNoConsensusYet = 200,
    UnfreezeConsensus = 201,

    Imported = 199,

    ConfiscationPolicy = 300,
    ConfiscationConsensus = 301,
    ConfiscationConsensusWhitelisted = 302,
    ConfiscationCancelled = 303
  }

  public enum FundStatus
  {
    Processed = 500,
    Imported = 599
  }

  public enum NodeStatus
  {
    Connected = 600,
    Disconnected = 601
  }
}
