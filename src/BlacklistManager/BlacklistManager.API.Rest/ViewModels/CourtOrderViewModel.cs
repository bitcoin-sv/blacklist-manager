// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class CourtOrderViewModelCreate
  {
    [JsonPropertyName("courtOrderHash")]
    public string CourtOrderHash { get; set; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("validTo")]
    public DateTimeOffset? ValidTo { get; set; }

    [JsonPropertyName("validFrom")]
    public DateTimeOffset? ValidFrom { get; set; }

    [JsonPropertyName("funds")]
    public List<Fund> Funds { get; set; }

    [JsonPropertyName("courtOrderId")]
    public string CourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderId")]
    public string FreezeCourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderHash")]
    public string FreezeCourtOrderHash { get; set; }

    public CourtOrderViewModelCreate()
    {
    }

    public CourtOrderViewModelCreate(Domain.Models.CourtOrder domain)
    {
      CourtOrderHash = domain.CourtOrderHash;
      DocumentType = domain.DocumentType;
      ValidFrom = domain.ValidFrom;
      ValidTo = domain.ValidTo;
      CourtOrderId = domain.CourtOrderId;
      FreezeCourtOrderId = domain.FreezeCourtOrderId;
      FreezeCourtOrderHash = domain.FreezeCourtOrderHash;
      if (domain.Funds != null)
      {
        Funds = new List<Fund>(domain.Funds.Select(MapFund));
      }
    }

    private DateTime? ToDateTime(DateTimeOffset? datetimeOffset)
    {
      if (datetimeOffset.HasValue)
      {
        if (datetimeOffset.Value.Offset != TimeSpan.Zero)
        {
          return DateTime.SpecifyKind(datetimeOffset.Value.DateTime, DateTimeKind.Unspecified);
        }
        return datetimeOffset.Value.UtcDateTime;
      }
      return null;
    }

    public Fund MapFund(Domain.Models.Fund domain)
    {
      return new Fund(domain);
    }


    public Domain.Models.CourtOrder ToDomainObject(string courtOrderHash)
    {
      var courtOrder = new Domain.Models.CourtOrder(
        this.CourtOrderId,
        courtOrderHash,
        (DocumentType)DocumentType,
        ToDateTime(ValidFrom),
        ToDateTime(ValidTo),
        this.FreezeCourtOrderId,
        this.FreezeCourtOrderHash);

      if (Funds != null)
      {
        foreach (var fund in Funds)
        {
          //  a fund can contain either TxOut or an address. In our domain model, we care only about TxOuts
          if (fund.TxOut != null)
          {
            courtOrder.AddFund(new Domain.Models.TxOut(
              fund.TxOut.TxId,
              fund.TxOut.Vout ?? long.MinValue));
          }
        }
      }
      return courtOrder;
    }

    public class Fund
    {
      [JsonPropertyName("txOut")]
      public TxOut TxOut { get; set; }

      public Fund()
      {
      }

      public Fund(BlacklistManager.Domain.Models.Fund domain)
      {
        TxOut = new TxOut(domain.TxOut.TxId, domain.TxOut.Vout);
      }
    }
  }

  public class TxOut
  {

    [JsonPropertyName("txId")]
    public string TxId { get; set; }

    [JsonPropertyName("vout")]
    public long? Vout { get; set; }

    public TxOut()
    {
    }

    public TxOut(string txId, long? vout)
    {
      TxId = txId;
      Vout = vout;
    }
  }
}
