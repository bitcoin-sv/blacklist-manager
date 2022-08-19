// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;

namespace BlacklistManager.Domain.Actions
{
  public interface IConsensusActivationValidatorFactory
  {
    IConsensusActivationValidator Create(ConsensusActivation consensusActivation, string courtOrderHash);
  }
}
