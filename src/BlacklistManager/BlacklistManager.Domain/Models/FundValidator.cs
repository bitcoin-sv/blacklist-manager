// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class FundValidator
  {
    private readonly Fund fund;
    public FundValidator(Fund fund)
    {
      this.fund = fund;
    }

    public string[] Validate()
    {
      var errors = new List<string>();
      if (fund.TxOut == null)
      {
        errors.Add($"'txOut' is required");
      }
      else
      {
        if (string.IsNullOrWhiteSpace(fund.TxOut.TxId))
        {
          errors.Add($"'txId' is required");
        }
        if (fund.TxOut.Vout == long.MinValue)
        {
          errors.Add($"'vout' is required");
        }
      }
      return errors.ToArray();
    }
  }
}
