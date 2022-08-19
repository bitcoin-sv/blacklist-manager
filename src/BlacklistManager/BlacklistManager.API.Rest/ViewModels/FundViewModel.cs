// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class FundViewModel
  {
    [JsonPropertyName("txOut")]
    public TxOutViewModelGet TxOut { get; set; }

    [JsonPropertyName("effectiveStatus")]
    public string Status { get; set; }

    [JsonPropertyName("enforceAtHeight")]
    public List<EnforceAtHeightViewModel> EnforceAtHeight { get; set; }

    public FundViewModel() { }

    public FundViewModel(FundQuery fundQuery)
    {
      Status = fundQuery.Status;
      TxOut = new TxOutViewModelGet
      {
        TxId = fundQuery.TxOut.TxId,
        Vout = fundQuery.TxOut.Vout
      };

      EnforceAtHeight = fundQuery.EnforceAtHeight.Select(x => new EnforceAtHeightViewModel(x)).ToList();
    }
  }
}
