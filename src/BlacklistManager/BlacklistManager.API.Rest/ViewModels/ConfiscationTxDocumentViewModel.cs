// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class ConfiscationTxDocumentViewModel
  {
    [JsonPropertyName("documentType")]
    public string DocumentType { get; init; }

    [JsonPropertyName("confiscationCourtOrderId")]
    public string ConfiscationCourtOrderId { get; init; }

    [JsonPropertyName("confiscationCourtOrderHash")]
    public string ConfiscationCourtOrderHash { get; init; }

    [JsonPropertyName("confiscationTxs")]
    public List<ConfiscationTxViewModel> ConfiscationTxs { get; init; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; init; }

    [JsonPropertyName("signedDate")]
    public DateTime SignedDate { get; init; }

    public Domain.Models.ConfiscationTxDocument ToDomainModel()
    {
      return new Domain.Models.ConfiscationTxDocument
      {
        ConfiscationCourtOrderId = ConfiscationCourtOrderId,
        ConfiscationTxs = ConfiscationTxs?.Select(x => new Domain.Models.ConfiscationTx { Hex = x.Hex }).ToList(),
        ConfiscationCourtOrderHash = ConfiscationCourtOrderHash,
        CreatedAt = CreatedAt,
        DocumentType = (Common.SmartEnums.DocumentType)DocumentType,
        SignedDate = SignedDate
      };
    }
  }
}
