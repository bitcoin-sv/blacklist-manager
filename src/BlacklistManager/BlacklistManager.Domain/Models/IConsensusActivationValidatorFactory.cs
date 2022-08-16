// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public interface IConsensusActivationValidatorFactory
  {
    IConsensusActivationValidator Create(ConsensusActivation consensusActivation, string courtOrderHash);
  }
}
