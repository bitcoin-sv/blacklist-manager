// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.Actions
{
  public partial class CourtOrderQuery
  {
    [JsonPropertyName("courtOrderHash")]
    public string CourtOrderHash { get; set; }

    [JsonPropertyName("documentType")]
    public string DocumentType { get; set; }

    [JsonPropertyName("funds")]
    public List<Fund> Funds { get; set; }

    [JsonPropertyName("courtOrderId")]
    public string CourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderId")]
    public string FreezeCourtOrderId { get; set; }

    [JsonPropertyName("freezeCourtOrderHash")]
    public string FreezeCourtOrderHash { get; set; }

    /* fields specific to query */

    [JsonPropertyName("policyEnforcementStartedAt")]
    public DateTime? PolicyEnforcementStartedAt { get; set; }

    [JsonPropertyName("consensusEnforcementStartedAtHeight")]
    public int? ConsensusEnforcementStartedAtHeight { get; set; }

    [JsonPropertyName("consensusEnforcementStoppedAtHeight")]
    public int? ConsensusEnforcementStoppedAtHeight { get; set; }

    [JsonPropertyName("relatedOrders")]
    public string[] RelatedOrders { get; set; }

    public CourtOrderQuery(
      string courtOrderHash_c, Int32 courtOrderType, string courtOrderId,
      DateTime? policyEnforcementStartedAt,
      Int32? consensusEnforcementStartedAtHeight,
      Int32? consensusEnforcementStoppedAtHeight,
      Array relatedOrders)
    {
      Funds = new List<Fund>();
      CourtOrderHash = courtOrderHash_c;
      DocumentType = CourtOrder.ToDocumentType(courtOrderType);
      CourtOrderId = courtOrderId;
      PolicyEnforcementStartedAt = policyEnforcementStartedAt;
      ConsensusEnforcementStartedAtHeight = consensusEnforcementStartedAtHeight;
      ConsensusEnforcementStoppedAtHeight = consensusEnforcementStoppedAtHeight;
      RelatedOrders = ConvertToStringArray(relatedOrders);
    }

    private string[] ConvertToStringArray(Array array)
    {
      string[] strArray = new string[array.Length];
      for (int i = 0; i < array.Length; i++)
      {
        strArray[i] = array.GetValue(i) as string;
      }
      return strArray;
    }

    public class Fund
    {
      [JsonPropertyName("txOut")]
      public TxOut TxOut { get; set; }

      [JsonPropertyName("effectiveStatus")]
      public string Status { get; set; }

      [JsonPropertyName("enforceAtHeight")]
      public List<EnforceAtHeight> EnforceAtHeight { get; set; }

      public Fund(string txId, Int64 vout)
      {
        EnforceAtHeight = new List<CourtOrderQuery.EnforceAtHeight>();
        TxOut = new TxOut(txId, vout);
      }

      public string GetKey()
      {
        return TxOut.TxId + TxOut.Vout;
      }
    }

    public class EnforceAtHeight
    {
      [JsonPropertyName("courtOrderHash")]
      public string CourtOrderHash { get; set; }
      
      [JsonPropertyName("courtOrderHashUnfreeze")]
      public string CourtOrderHashUnfreeze { get; set; }
      
      [JsonPropertyName("startEnforceAtHeight")]
      public int? StartEnforceAtHeight { get; set; }
      
      [JsonPropertyName("stopEnforceAtHeight")]
      public int? StopEnforceAtHeight { get; set; }
    }

    public class TxOut
    {
      [JsonPropertyName("txId")]
      public string TxId { get; set; }

      [JsonPropertyName("vout")]
      public long Vout { get; set; }

      public TxOut()
      {
      }

      public TxOut(string txId, long vout)
      {
        TxId = txId.ToLower();
        Vout = vout;
      }
    }
  }
}
