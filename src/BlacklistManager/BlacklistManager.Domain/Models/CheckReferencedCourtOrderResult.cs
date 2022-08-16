// Copyright (c) 2020 Bitcoin Association

using Common;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  public class CheckReferencedCourtOrderResult
  {
    public CheckReferencedCourtOrderResult()
    {
      ReferencedCourtOrderExists = true;
      UnreferencedFunds = Enumerable.Empty<Fund>();
    }

    public static CheckReferencedCourtOrderResult CreateNoReferencedCourtOrderResult()
    {
      return new CheckReferencedCourtOrderResult() { ReferencedCourtOrderExists = false };
    }

    public static CheckReferencedCourtOrderResult CreateUnreferencedFundsResult(IEnumerable<Fund> funds)
    {
      return new CheckReferencedCourtOrderResult() { UnreferencedFunds = funds };
    }

    public static CheckReferencedCourtOrderResult CreatePassResult()
    {
      return new CheckReferencedCourtOrderResult();
    }

    public bool CheckPassed
    {
      get { return ReferencedCourtOrderExists && !UnreferencedFunds.Any();  } 
    }

    public bool ReferencedCourtOrderExists { get; set; }

    public IEnumerable<Fund> UnreferencedFunds { get; set; }

    public void IfNotPassedThrowBadRequestException()
    {
      if (!CheckPassed)
      {
        string message = string.Empty;

        if (!ReferencedCourtOrderExists)
        {
          message = "Referenced court order does not exist";
        }
        if (UnreferencedFunds.Any())
        {
          message = $"Not all court order funds exist on referenced court order. First unreferenced fund is '{UnreferencedFunds.First().TxOut}'";
        }

        throw new BadRequestException(message);
      }
    }   
  }
}
