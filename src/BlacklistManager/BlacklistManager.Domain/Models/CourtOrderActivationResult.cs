// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class CourtOrderActivationResult
  {
    private readonly bool _internalError = false;
    private readonly List<string> _activatedCourtOrders = new List<string>();

    public CourtOrderActivationResult(bool internalError = false)
    {
      _internalError = internalError;
    }

    public void AddActivated(string courtOrderHash)
    {
      _activatedCourtOrders.Add(courtOrderHash);
    }

    public IEnumerable<string> ActivatedCourtOrders => _activatedCourtOrders;

    /// <summary>
    /// If true then some internal error occurred during court order processing
    /// </summary>
    public bool InternalError => _internalError;
    /// <summary>
    /// If true then activation of court order was successful
    /// </summary>
    public bool WasSuccessful => !_internalError;
  }
}