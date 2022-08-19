// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Actions
{
  public class ConfigurationParams : IConfigurationParams
  {
    private readonly IConfigurationParamRepository configurationParamRepository;
    private readonly ILogger logger;

    public ConfigurationParams(
      IConfigurationParamRepository configurationParamRepository,
      ILogger<ConfigurationParams> logger)
    {
      this.configurationParamRepository = configurationParamRepository;
      this.logger = logger;
    }

    public async Task<int> GetDesiredHashrateAcceptancePercentAsync()
    {
      var sValue = await InitializeAsync("desiredhashrateacceptancepercent");
      if (sValue != null && int.TryParse(sValue, out var iValue))
      {
        return iValue;
      }
      else
      {
        logger.LogError($"Could not retrieve value for configuration parameter 'DesiredHashRateAcceptancePercent'");
        return 75;
      }
    }

    private async Task<string> InitializeAsync(string paramName)
    {
      logger.LogTrace($"Reading configuration parameter '{paramName}'");
      var allParams = await configurationParamRepository.GetAsync();
      foreach (var param in allParams)
      {
        if (param.Key.ToLower() == paramName.ToLower())
        {
          return param.Value;
        }
      }

      return null;
    }
  }
}
