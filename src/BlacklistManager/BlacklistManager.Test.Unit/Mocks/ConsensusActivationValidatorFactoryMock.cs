// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.Models;
using Moq;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Unit.Mocks
{
  public class ConsensusActivationValidatorFactoryMock : IConsensusActivationValidatorFactory
  {
    readonly IConsensusActivationValidator cav;
    readonly bool isValid;

    public ConsensusActivationValidatorFactoryMock(IConsensusActivationValidator cav)
    {
      this.cav = cav;
    }

    public ConsensusActivationValidatorFactoryMock(bool isValid)
    {
      this.isValid = isValid;
    }

    public IConsensusActivationValidator Create(ConsensusActivation consensusActivation, string courtOrderHash)
    {
      if (cav == null)
      {
        var c = new Mock<IConsensusActivationValidator>();
        c.Setup(x => x.IsValidAsync()).Returns(Task.FromResult(isValid));
        return c.Object;
      }
      return cav;
    }
  }
}
