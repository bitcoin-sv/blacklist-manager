// Copyright (c) 2020 Bitcoin Association

using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public interface IConfigurationParams
  {
    Task<int> GetDesiredHashrateAcceptancePercentAsync();
  }
}