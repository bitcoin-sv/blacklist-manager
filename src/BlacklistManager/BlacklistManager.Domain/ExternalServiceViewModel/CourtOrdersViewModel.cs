// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class CourtOrdersViewModel
  {
    [JsonPropertyName("courtOrders")]
    public IEnumerable<SignedPayloadViewModel> CourtOrders { get; set; }

    [JsonPropertyName("@nextLink")]
    public string NextLink { get; set; }

    [JsonPropertyName("@deltaLink")]
    public string DeltaLink { get; set; }
  }
}
