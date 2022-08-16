// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlacklistManager.Test.Functional.ViewModels
{
  public class CourtOrderQuery
  {
    [JsonPropertyName("courtOrderHash")]
    public string CourtOrderHash { get; set; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("funds")]
    public List<Fund> Funds { get; set; }

    [JsonPropertyName("courtOrderId")]
    public string CourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderId")]
    public string FreezeCourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderHash")]
    public string FreezeCourtOrderHash { get; set; }

    [JsonPropertyName("policyEnforcementStartedAt")]
    public DateTime? PolicyEnforcementStartedAt { get; set; }

    [JsonPropertyName("consensusEnforcementStartedAtHeight")]
    public int? ConsensusEnforcementStartedAtHeight { get; set; }

    [JsonPropertyName("consensusEnforcementStoppedAtHeight")]
    public int? ConsensusEnforcementStoppedAtHeight { get; set; }

    [JsonPropertyName("relatedOrders")]
    public string[] RelatedOrders { get; set; }

    public CourtOrderQuery()
    {
    }

    public class Fund : Domain.Actions.CourtOrderQuery.Fund
    {
      public Fund() : base("", 0)
      {
      }
    }
  }  
}
