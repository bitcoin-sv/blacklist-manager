// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class ConsensusActivationValidatorMock : IConsensusActivationValidator
  {
    public IEnumerable<string> Errors => new string[0];

    public Task<bool> IsValidAsync()
    {
      return Task.FromResult(true);
    }
  }
}
