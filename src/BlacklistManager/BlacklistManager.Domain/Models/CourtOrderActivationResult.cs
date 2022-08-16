// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class CourtOrderActivationResult
  {
    private readonly bool internalError = false;
    private readonly List<string> activatedCourtOrders = new List<string>();

    public CourtOrderActivationResult(bool internalError = false)
    {
      this.internalError = internalError;
    }

    public void AddActivated(string courtOrderHash)
    {
      activatedCourtOrders.Add(courtOrderHash);
    }

    public IEnumerable<string> ActivatedCourtOrders => activatedCourtOrders;

    /// <summary>
    /// If true then some internal error occurred during court order processing
    /// </summary>
    public bool InternalError => internalError;
    /// <summary>
    /// If true then activation of court order was successful
    /// </summary>
    public bool WasSuccessful => !internalError;
  }
}