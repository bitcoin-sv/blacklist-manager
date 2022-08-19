// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  public class ProcessCourtOrderResult
  {
    public string CourtOrderHash { get; set; }
    public long? InternalCourtOrderId { get; set; }
    public bool AlreadyImported { get; set; }
    public List<string> Errors { get; set; }

    public ProcessCourtOrderResult(string courtOrderHash, long? internalCourtOrderId)
    {
      CourtOrderHash = courtOrderHash;
      InternalCourtOrderId = internalCourtOrderId;
      AlreadyImported = !internalCourtOrderId.HasValue;
      Errors = new List<string>();
    }

    public ProcessCourtOrderResult(string courtOrderHash, string error)
    {
      CourtOrderHash = courtOrderHash;
      Errors = new List<string>();
      Errors.Add(error);

    }

    public ProcessCourtOrderResult(string courtOrderHash, string[] errors)
    {
      CourtOrderHash = courtOrderHash;
      Errors = errors.ToList();
    }

    public ProcessCourtOrderResult() { }
  }
}
