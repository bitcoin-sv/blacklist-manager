// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Repositories
{
  public interface IConfigurationParamRepository
  {
    public Task<IEnumerable<ConfigurationParam>> GetAsync();
    public Task<ConfigurationParam> GetAsync(ConfigurationParam configParam);
    public Task<ConfigurationParam> InsertAsync(ConfigurationParam configParam);
    public Task<bool> UpdateAsync(ConfigurationParam configParam);
    public Task DeleteAsync(ConfigurationParam configParam);
  }
}
