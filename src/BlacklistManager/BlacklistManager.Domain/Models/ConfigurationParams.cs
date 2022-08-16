// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public class ConfigurationParams : IConfigurationParams
  {
    private readonly IConfigurationParamRepository configurationParamRepository;
    private readonly ILogger logger;

    public ConfigurationParams(
      IConfigurationParamRepository configurationParamRepository,
      ILoggerFactory logger)
    {
      this.configurationParamRepository = configurationParamRepository;
      this.logger = logger.CreateLogger(LogCategories.Domain) ?? throw new ArgumentNullException(nameof(logger));
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

  public class ConfigurationParam
  {
    public ConfigurationParam(string paramKey, string paramValue)
    {
      Key = paramKey.ToLower();
      Value = paramValue;
    }

    public string Key { get; private set; }
    public string Value { get; private set; }
  }
}
