// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Actions;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class ConsensusActivationValidatorFactoryMock : IConsensusActivationValidatorFactory
  {
    public IConsensusActivationValidator Create(Domain.Models.ConsensusActivation consensusActivation, string courtOrderHash)
    {
      return new ConsensusActivationValidatorMock();
    }
  }
}
