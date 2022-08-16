// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;
using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class CourtOrderValidator
  {
    private readonly CourtOrder courtOrder;

    public CourtOrderValidator(CourtOrder courtOrder)
    {
      this.courtOrder = courtOrder;
    }

    /// <summary>
    /// Returns array of errors or empty array if there are none
    /// This is  context free validation. 
    /// </summary>
    public string[] Validate()
    {
      var errors = new List<string>();
      // We use hardcoded string instead of nameof(), because we have different capitalization
      if (courtOrder.DocumentType == null)
      {
        errors.Add("'documentType' is required");
      }

      if (!(courtOrder.DocumentType == DocumentType.FreezeOrder || courtOrder.DocumentType == DocumentType.UnfreezeOrder))
      {
        errors.Add("Invalid value for 'documentType'");
      }

      if (string.IsNullOrWhiteSpace(courtOrder.CourtOrderId))
      {
        errors.Add("'courtOrderId' is required");
      }

      if (string.IsNullOrWhiteSpace(courtOrder.CourtOrderHash))
      {
        errors.Add("'courtOrderHash' is required");
      }

      if (courtOrder.DocumentType == DocumentType.FreezeOrder)
      {
        if (courtOrder.Funds.Count == 0)
        {
          errors.Add("Non empty 'funds' is required");
        }
        else
        {
          ValidateFunds(courtOrder.Funds, errors);
        }
      }

      else if (courtOrder.DocumentType == DocumentType.UnfreezeOrder)
      {
        if (string.IsNullOrWhiteSpace(courtOrder.FreezeCourtOrderId))
        {
          errors.Add($"'freezeCourtOrderId' is required for document of type '{DocumentType.FreezeOrder}'");
        }

        if (string.IsNullOrWhiteSpace(courtOrder.FreezeCourtOrderHash))
        {
          errors.Add($"'freezeCourtOrderHash' is required for document of type '{DocumentType.UnfreezeOrder}'");
        }        

        if (courtOrder.Funds != null)
        {
          ValidateFunds(courtOrder.Funds, errors);
        }
      }

      if (courtOrder.ValidTo.HasValue && courtOrder.ValidTo.Value.Kind != DateTimeKind.Utc)
      {
        errors.Add($"'validTo' must be UTC time");
      }

      if (courtOrder.ValidFrom.HasValue && courtOrder.ValidFrom.Value.Kind != DateTimeKind.Utc)
      {
        errors.Add($"'validFrom' must be UTC time");
      }

      if (courtOrder.ValidFrom.HasValue && courtOrder.ValidTo.HasValue && courtOrder.ValidFrom> courtOrder.ValidTo)
      {
        errors.Add($"'validFrom' is greater then 'validTo'");
      }

      return errors.ToArray();
    }

    private void ValidateFunds(IEnumerable<Fund> funds, List<string> errors)
    {
      int index = 0;
      foreach (var fund in funds)
      {
        if (!fund.IsValid())
        {
          foreach (var validationMessage in fund.ValidationMessages)
          {
            errors.Add($"Funds[{index}]: {validationMessage}");
          }
        }
        index++;
      }
    }
  }
}
