// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class ConsensusActivationViewModel
  {
    [Required]
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [Required]
    [JsonPropertyName("courtOrderHash")]
    public string CourtOrderHash { get; set; }

    [Required]
    [JsonPropertyName("enforceAtHeight")]
    public int? EnforceAtHeight { get; set; }

    [Required]
    [JsonPropertyName("acceptances")]
    public List<AcceptanceItem> Acceptances { get; set; }

    [Required]
    [JsonPropertyName("signedDate")]
    public DateTime SignedDate { get; set; }

    [JsonPropertyName("rewardTransactions")]
    public List<ConfiscationTxViewModel> ConfiscationTimelockedTxs { get; set; }
  }

  public class AcceptanceItem
  {
    [Required]
    [JsonPropertyName("acceptance")]
    public string SignedAcceptanceJson { get; set; }
  }

  public class AcceptanceViewModel
  {
    [Required]
    [JsonPropertyName("courtOrderHash")]
    public string CourtOrderHash { get; set; }

    [Required]
    [JsonPropertyName("currentBlockHash")]
    public string CurrentBlockHash { get; set; }
  }
}
