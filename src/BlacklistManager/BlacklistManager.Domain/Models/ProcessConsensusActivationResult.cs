// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class ProcessConsensusActivationResult
  {
    private bool _internalError = false;
    private bool _anyConsensusActivationsPending = false;
    private List<string> _activatedConsensusActivations = new();

    public ProcessConsensusActivationResult(bool internalError = false)
    {
      this._internalError = internalError;
    }
    public void SetConsensusActivationsPending(bool increaseFailureCount = true)
    {
      _anyConsensusActivationsPending = true;
      if (increaseFailureCount)
      {
        Failed++;
      }
    }

    public void SetInternalError()
    {
      _internalError = true;
    }

    public int Processed { get; set; }
    public int Failed { get; private set; }

    /// <summary>
    /// If true then processing ended without errors
    /// </summary>
    public bool WasSuccessful => !_internalError;

    /// <summary>
    /// if true then not all consensus activations were processed
    /// </summary>
    public bool AnyConsensusActivationsStillPending => _anyConsensusActivationsPending;

    public IEnumerable<string> ConsensusActivations => _activatedConsensusActivations;

    public void AddActivated(string coHash)
    {
      _activatedConsensusActivations.Add(coHash);
    }
  }
}
