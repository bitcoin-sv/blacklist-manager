// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class ConfiscationTxDocumentViewModel
  {
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("confiscationOrderId")]
    public string ConfiscationCourtOrderId { get; set; }

    [JsonPropertyName("confiscationOrderHash")]
    public string ConfiscationCourtOrderHash { get; set; }

    [JsonPropertyName("confiscationTxs")]
    public List<ConfiscationTxViewModel> ConfiscationTxs { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("signedDate")]
    public DateTime SignedDate { get; set; }

    public Models.ConfiscationTxDocument ToDomainModel()
    {
      return new Models.ConfiscationTxDocument
      {
        ConfiscationCourtOrderId = ConfiscationCourtOrderId,
        ConfiscationTxs = ConfiscationTxs?.Select(x => new Models.ConfiscationTx { Hex = x.Hex }).ToList(),
        ConfiscationCourtOrderHash = ConfiscationCourtOrderHash,
        CreatedAt = CreatedAt,
        DocumentType = (Common.SmartEnums.DocumentType)DocumentType,
        SignedDate = SignedDate
      };
    }

  }
}
