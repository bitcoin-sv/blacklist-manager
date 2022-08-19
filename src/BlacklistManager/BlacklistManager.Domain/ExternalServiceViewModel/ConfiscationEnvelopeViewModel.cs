// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class ConfiscationEnvelopeViewModel
  {
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("confiscationOrderDocument")]
    public string ConfiscationCourtOrder { get; set; }

    [JsonPropertyName("confiscationTxsDocument")]
    public ConfiscationTxDocumentViewModel ConfiscationTxDocument { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("signedDate")]
    public DateTime SignedDate { get; set; }

    public Models.ConfiscationEnvelope ToDomainObject()
    {
      if (string.IsNullOrEmpty(ConfiscationCourtOrder))
      {
        throw new InvalidOperationException("Confiscation court order is missing in confiscation envelope.");
      }
      if (ConfiscationTxDocument is null)
      {
        throw new InvalidOperationException("Confiscation tx document is missing in confiscation envelope.");
      }

      return new Models.ConfiscationEnvelope
      {
        DocumentType = (DocumentType)this.DocumentType,
        ConfiscationCourtOrder = this.ConfiscationCourtOrder,
        ConfiscationTxDocument = this.ConfiscationTxDocument.ToDomainModel(),
        SignedDate = this.SignedDate,
      };
    }
  }
}
