// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public class LegalEndpoints : ILegalEndpoints
  {
    private readonly ILegalEntityRepository legalEntityRepository;
    private readonly ILogger logger;

    public LegalEndpoints(ILegalEntityRepository legalEntityRepository,
      ILoggerFactory logger)
    {
      this.legalEntityRepository = legalEntityRepository ?? throw new ArgumentNullException(nameof(legalEntityRepository));
      this.logger = logger.CreateLogger(LogCategories.Domain) ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IEnumerable<LegalEntityEndpoint>> GetLegalEntitiyEndpointsAsync()
    {
      return legalEntityRepository.GetAsync();
    }

    public void UpdateDeltaLink(int legalEntityEndpointId, string deltaLink)
    {
      legalEntityRepository.UpdateDeltaLink(legalEntityEndpointId, DateTime.UtcNow, deltaLink);
    }

    public void UpdateLastError(int legalEntityEndpointId, string lastError)
    {
      legalEntityRepository.SetError(legalEntityEndpointId, DateTime.UtcNow, lastError, DateTime.UtcNow);
    }

    public async Task<LegalEntityEndpoint> CreateAsync(string baseUrl, string apiKey)
    {
      var r = await legalEntityRepository.InsertAsync(new LegalEntityEndpoint(baseUrl, apiKey));
      if (r == null)
      {
        logger.LogWarning($"Legal entity endpoint create failed. Endpoint '{baseUrl}' already exists");
      }
      else
      {
        logger.LogInformation($"Legal entity endpoint '{r.LegalEntityEndpointId}' for '{r.BaseUrl}' created");
      }
      return r;
    }

    public async Task<IEnumerable<LegalEntityEndpoint>> GetAsync()
    {
      return await legalEntityRepository.GetAsync();
    }

    public async Task<LegalEntityEndpoint> GetAsync(int id)
    {
      return await legalEntityRepository.GetAsync(id);
    }

    public async Task<bool> UpdateAsync(int id, string baseUrl, string apiKey)
    {
      var r = await legalEntityRepository.UpdateAsync(new LegalEntityEndpoint(id, baseUrl, apiKey));
      var bUpdated = baseUrl != null ? $"[baseUrl={baseUrl}]" : string.Empty;
      var aUpdated = apiKey != null ? "[apiKey=***]" : string.Empty;
      logger.LogInformation($"Legal entity endpoint '{id}' updated: {bUpdated}{aUpdated}");
      return r;
    }

    public async Task<bool> DisableAsync(int id)
    {
      var r = await legalEntityRepository.UpdateStatusAsync(id, false);
      logger.LogInformation($"Legal entity endpoint '{id}' disabled");
      return r;
    }

    public async Task<bool> EnableAsync(int id)
    {
      var r = await legalEntityRepository.UpdateStatusAsync(id, true);
      logger.LogInformation($"Legal entity endpoint '{id}' enabled");
      return r;
    }

    public async Task<bool> ResetAsync(int id)
    {
      var r = await legalEntityRepository.ResetDeltaLinkAsync(id);
      logger.LogInformation($"Legal entity endpoint '{id}' deltalink was cleared");
      return r;
    }
  }
}
