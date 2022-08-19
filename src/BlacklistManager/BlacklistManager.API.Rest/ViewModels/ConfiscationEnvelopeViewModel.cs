// Copyright (c) 2020 Bitcoin Association

using Common;
using Common.SmartEnums;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class ConfiscationEnvelopeViewModel
  {
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("order")]
    public string ConfiscationCourtOrder { get; set; }

    [JsonPropertyName("txs")]
    public ConfiscationTxDocumentViewModel ConfiscationTxDocument { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; }

    [JsonPropertyName("signedDate")]
    public string SignedDate { get; set; }

    [JsonPropertyName("chainedTransactions")]
    public string[] ChainedTransactions { get; set; }

    public Domain.Models.ConfiscationEnvelope ToDomainObject()
    {
      if (ConfiscationCourtOrder == null)
      {
        throw new BadRequestException("Parameter cannot be null 'order'.");
      }

      if (ConfiscationTxDocument == null)
      {
        throw new BadRequestException("Parameter cannot be null 'txs'.");
      }

      return new Domain.Models.ConfiscationEnvelope
      {
        ChainedTransactions = this.ChainedTransactions,
        ConfiscationCourtOrder = ConfiscationCourtOrder,
        ConfiscationTxDocument = ConfiscationTxDocument.ToDomainModel(),
        DocumentType = (DocumentType)this.DocumentType,
      };
    }
  }
}
