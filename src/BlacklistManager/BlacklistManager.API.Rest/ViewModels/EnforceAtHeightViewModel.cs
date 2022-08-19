// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class EnforceAtHeightViewModel
  {
    [JsonPropertyName("courtOrderHash")]
    public string CourtOrderHash { get; set; }

    [JsonPropertyName("courtOrderHashUnfreeze")]
    public string CourtOrderHashUnfreeze { get; set; }

    [JsonPropertyName("startEnforceAtHeight")]
    public int? StartEnforceAtHeight { get; set; }

    [JsonPropertyName("stopEnforceAtHeight")]
    public int? StopEnforceAtHeight { get; set; }

    public EnforceAtHeightViewModel() { }

    public EnforceAtHeightViewModel(EnforceAtHeightQuery enforceAtHeightQuery)
    {
      CourtOrderHash = enforceAtHeightQuery.CourtOrderHash;
      CourtOrderHashUnfreeze = enforceAtHeightQuery.CourtOrderHashUnfreeze;
      StartEnforceAtHeight = enforceAtHeightQuery.StartEnforceAtHeight;
      StopEnforceAtHeight = enforceAtHeightQuery.StopEnforceAtHeight;
    }
  }
}
