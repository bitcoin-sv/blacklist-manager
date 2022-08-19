// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels{
  public class CourtOrderQueryViewModel
  {
    [JsonPropertyName("courtOrderId")]
    public string CourtOrderId { get; set; }

    [JsonPropertyName("courtOrderHash")]
    public string CourtOrderHash { get; set; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("isCancelled")]
    public bool IsCancelled { get;set; }

    [JsonPropertyName("policyEnforcementStartedAt")]
    public DateTime? PolicyEnforcementStartedAt { get; set; }

    [JsonPropertyName("consensusEnforcementStartedAtHeight")]
    public int? ConsensusEnforcementStartedAtHeight { get; set; }

    [JsonPropertyName("consensusEnforcementStoppedAtHeight")]
    public int? ConsensusEnforcementStoppedAtHeight { get; set; }

    [JsonPropertyName("relatedOrders")]
    public string[] RelatedOrders { get; set; }

    [JsonPropertyName("funds")]
    public List<FundViewModel> Funds { get; set; }

    public CourtOrderQueryViewModel() { }

    public CourtOrderQueryViewModel(CourtOrderQuery courtOrderQuery)
    {
      CourtOrderHash = courtOrderQuery.CourtOrderHash;
      DocumentType = courtOrderQuery.DocumentType;
      CourtOrderId = courtOrderQuery.CourtOrderId;
      PolicyEnforcementStartedAt = courtOrderQuery.PolicyEnforcementStartedAt;
      ConsensusEnforcementStartedAtHeight = courtOrderQuery.ConsensusEnforcementStartedAtHeight;
      ConsensusEnforcementStoppedAtHeight = courtOrderQuery.ConsensusEnforcementStoppedAtHeight;
      RelatedOrders = courtOrderQuery.RelatedOrders;
      IsCancelled = courtOrderQuery.CourtOrderStatus == (int)CourtOrderStatus.ConfiscationCancelled;

      Funds = courtOrderQuery.Funds.Select(x => new FundViewModel(x)).ToList();
    }
  }
}
