// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public interface IConsensusActivationValidator
  {
    IEnumerable<string> Errors { get; }
    Task<bool> IsValidAsync();
  }
}