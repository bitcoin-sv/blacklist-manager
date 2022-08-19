// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class CancelConfiscationOrderViewModel
  {
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("confiscationOrderHash")]
    public string ConfiscationOrderHash { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("signedDate")]
    public DateTime SignedDate { get; set; }
  }
}
