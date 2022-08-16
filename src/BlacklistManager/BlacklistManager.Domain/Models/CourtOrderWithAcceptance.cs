// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class CourtOrderWithAcceptance
  {
    public long InternalCourtOrderId { get; set; }

    public string CourtOrderHash { get; set; }

    public IEnumerable<CourtOrderAcceptance> CourtOrderAcceptances { get; set; }
  }
}
