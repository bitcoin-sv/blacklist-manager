// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class ProcessConsensusActivationResult
  {
    private bool internalError = false;
    private bool anyConsensusActivationsPending = false;

    public ProcessConsensusActivationResult(bool internalError = false)
    {
      this.internalError = internalError;
    }

    public void SetConsensusActivationsPending()
    {
      anyConsensusActivationsPending = true;
    }

    public void SetInternalError()
    {
      internalError = true;
    }

    public int Processed { get; set; }

    /// <summary>
    /// If true then processing ended without errors
    /// </summary>
    public bool WasSuccessful => !internalError;

    /// <summary>
    /// if true then not all consensus activations were processed
    /// </summary>
    public bool AnyConsensusActivationsStillPending => anyConsensusActivationsPending;
  }
}
