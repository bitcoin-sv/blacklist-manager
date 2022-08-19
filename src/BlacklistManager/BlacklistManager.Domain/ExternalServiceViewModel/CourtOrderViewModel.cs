// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class CourtOrderViewModel
  {
    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("courtOrderId")]
    public string CourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderId")]
    public string FreezeCourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderHash")]
    public string FreezeCourtOrderHash { get; set; }

    [JsonPropertyName("blockchain")]
    public string Blockchain { get; set; }

    [JsonPropertyName("validFrom")]
    public DateTime? ValidFrom { get; set; }

    [JsonPropertyName("freezeChildren")]
    public bool FreezeChildren { get; set; }

    [JsonPropertyName("funds")]
    public List<Fund> Funds { get; set; }

    [JsonPropertyName("destination")]
    public ConfiscationDestinationVM Destination { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("author")]
    public string Author { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("signedDate")]
    public DateTime SignedDate { get; set; }

    [JsonPropertyName("discoveryDate")]
    public DateTime? DiscoveryDate { get; set; }

    [JsonPropertyName("discoveryBlockHash")]
    public string DiscoveryBlockHash { get; set; }


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

    public Domain.Models.CourtOrder ToDomainObject(string courtOrderHash, Network network)
    {
      var courtOrder = new Models.CourtOrder
      {
        CourtOrderId = CourtOrderId,
        CourtOrderHash = courtOrderHash,
        DocumentType = (DocumentType)DocumentType,
        ValidFrom = ToDateTime(ValidFrom),
        FreezeCourtOrderId = FreezeCourtOrderId,
        FreezeCourtOrderHash = FreezeCourtOrderHash,
        Blockchain = Blockchain,
        SignedDate = SignedDate,
        Destination = new Models.ConfiscationDestination 
        { 
          Address = Destination?.Address,
          Amount = Destination?.Amount
        }
      };
      if (Funds != null)
      {
        foreach (var fund in Funds)
        {
          //  a fund can contain either TxOut or an address. In our domain model, we care only about TxOuts
          if (fund.TxOut != null)
          {
            courtOrder.AddFund(new Domain.Models.TxOut(
              fund.TxOut.TxId,
              fund.TxOut.Vout),
              fund.Value);
          }
        }
      }
      return courtOrder;
    }

  }

  public class Fund
  {
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("txOut")]
    public TxOut TxOut { get; set; }

    [JsonPropertyName("discoveredFrom")]
    public List<FundDiscoveredFrom> DiscoveredFrom { get; set; }

    [JsonPropertyName("value")]
    public long Value { get; set; }
  }

  public class TxOut
  {
    [JsonPropertyName("txId")]
    public string TxId { get; set; }

    [JsonPropertyName("vout")]
    public long Vout { get; set; }
  }

  public class FundDiscoveredFrom
  {
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("txOut")]
    public TxOut TxOut { get; set; }
  }

  public class ConfiscationDestinationVM
  {
    [JsonPropertyName("address")]
    public string Address { get; set; }

    [JsonPropertyName("amount")]
    public long Amount { get; set; }
  }
}
