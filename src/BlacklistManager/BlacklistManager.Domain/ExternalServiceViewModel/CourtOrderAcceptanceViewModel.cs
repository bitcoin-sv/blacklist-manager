// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class CourtOrderAcceptanceViewModel
  {
    public string DocumentType { get; set; }

    public string CourtOrderHash { get; set; }

    public int DesiredHashrateAcceptancePercent { get; set; }

    public string CurrentBlockHash { get; set; }

    public DateTime CreatedAt { get; set; }

    public string[] DelegatedKeys { get; set; }

    public string[] Rejection { get; set; }
  }
}
