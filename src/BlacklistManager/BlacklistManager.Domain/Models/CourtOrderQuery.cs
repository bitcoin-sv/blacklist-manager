// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;
using System.Collections.Generic;

namespace BlacklistManager.Domain.Models{
  public partial class CourtOrderQuery
  {
    public string CourtOrderHash { get; set; }

    public string DocumentType { get; set; }

    public List<FundQuery> Funds { get; set; }

    public string CourtOrderId { get; set; }

    public int CourtOrderStatus { get; set; }

    /* fields specific to query */

    public DateTime? PolicyEnforcementStartedAt { get; set; }

    public int? ConsensusEnforcementStartedAtHeight { get; set; }

    public int? ConsensusEnforcementStoppedAtHeight { get; set; }

    public string[] RelatedOrders { get; set; }

    public CourtOrderQuery(
      string courtOrderHash_c, int courtOrderStatus, int courtOrderType, string courtOrderId,
      DateTime? policyEnforcementStartedAt,
      int? consensusEnforcementStartedAtHeight,
      int? consensusEnforcementStoppedAtHeight,
      Array relatedOrders)
    {
      Funds = new List<FundQuery>();
      CourtOrderHash = courtOrderHash_c;
      DocumentType = (DocumentType)(CourtOrderType)courtOrderType;
      CourtOrderStatus = courtOrderStatus;
      CourtOrderId = courtOrderId;
      PolicyEnforcementStartedAt = policyEnforcementStartedAt;
      ConsensusEnforcementStartedAtHeight = consensusEnforcementStartedAtHeight;
      ConsensusEnforcementStoppedAtHeight = consensusEnforcementStoppedAtHeight;
      RelatedOrders = ConvertToStringArray(relatedOrders);
    }

    private string[] ConvertToStringArray(Array array)
    {
      string[] strArray = new string[array.Length];
      for (int i = 0; i < array.Length; i++)
      {
        strArray[i] = array.GetValue(i) as string;
      }
      return strArray;
    }
  }
}
