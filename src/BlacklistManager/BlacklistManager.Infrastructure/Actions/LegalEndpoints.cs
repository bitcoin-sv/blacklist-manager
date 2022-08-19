// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Actions
{
  public class LegalEndpoints : ILegalEndpoints
  {
    readonly ILegalEntityRepository _legalEntityRepository;
    readonly ILegalEntityFactory _legalEntityFactory;
    readonly ITrustListRepository _trustListRepository;
    readonly IBackgroundJobs _backgroundJobs;
    readonly ILogger _logger;

    public LegalEndpoints(ILegalEntityRepository legalEntityRepository,
      ITrustListRepository trustListRepository,
      ILegalEntityFactory legalEntityFactory,
      IBackgroundJobs backgroundJobs,
      ILogger<LegalEndpoints> logger)
    {
      _legalEntityRepository = legalEntityRepository ?? throw new ArgumentNullException(nameof(legalEntityRepository));
      _trustListRepository = trustListRepository ?? throw new ArgumentNullException(nameof(trustListRepository));
      _legalEntityFactory = legalEntityFactory ?? throw new ArgumentNullException(nameof(legalEntityFactory));
      _backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
      _logger = logger;
    }

    public async Task<LegalEntityEndpoint> CreateAsync(string baseUrl, string apiKey)
    {
      _backgroundJobs.CheckForOfflineMode();
      var legalEntityEndpoint = _legalEntityFactory.Create(baseUrl, null, Common.Consts.ApiKeyHeaderName, apiKey, null);
      var publicKey = await legalEntityEndpoint.GetPublicKeyAsync();

      if (!await _trustListRepository.IsPublicKeyTrustedAsync(publicKey))
      {
        throw new BadRequestException($"Public key '{publicKey}' used by '{baseUrl}' to sign documents is not trusted.");
      }

      var r = await _legalEntityRepository.InsertAsync(new LegalEntityEndpoint(baseUrl, apiKey));
      if (r == null)
      {
        _logger.LogWarning($"Legal entity endpoint create failed. Endpoint '{baseUrl}' already exists");
      }
      else
      {
        _logger.LogInformation($"Legal entity endpoint '{r.LegalEntityEndpointId}' for '{r.BaseUrl}' created");
      }
      return r;
    }

    public Task UpdateDeltaLinkAsync(int legalEntityEndpointId, string deltaLink, int processedOrdersCount)
    {
      return _legalEntityRepository.UpdateDeltaLinkAsync(legalEntityEndpointId, DateTime.UtcNow, deltaLink, processedOrdersCount);
    }

    public Task UpdateLastErrorAsync(int legalEntityEndpointId, string lastError, bool increaseFailureCount)
    {
      return _legalEntityRepository.SetErrorAsync(legalEntityEndpointId, lastError, DateTime.UtcNow, increaseFailureCount);
    }

    public Task<IEnumerable<LegalEntityEndpoint>> GetAsync()
    {
      return _legalEntityRepository.GetAsync();
    }

    public Task<LegalEntityEndpoint> GetAsync(int id)
    {
      return _legalEntityRepository.GetAsync(id);
    }

    public async Task<bool> UpdateAsync(int id, string baseUrl, string apiKey)
    {
      _backgroundJobs.CheckForOfflineMode();
      var r = await _legalEntityRepository.UpdateAsync(new LegalEntityEndpoint(id, baseUrl, apiKey));
      var bUpdated = baseUrl != null ? $"[baseUrl={baseUrl}]" : string.Empty;
      var aUpdated = apiKey != null ? "[apiKey=***]" : string.Empty;
      _logger.LogInformation($"Legal entity endpoint '{id}' updated: {bUpdated}{aUpdated}");
      return r;
    }

    public async Task<bool> DisableAsync(int id)
    {
      _backgroundJobs.CheckForOfflineMode();
      var r = await _legalEntityRepository.UpdateStatusAsync(id, false);
      _logger.LogInformation($"Legal entity endpoint '{id}' disabled");
      return r;
    }

    public async Task<bool> EnableAsync(int id)
    {
      _backgroundJobs.CheckForOfflineMode();
      var r = await _legalEntityRepository.UpdateStatusAsync(id, true);
      _logger.LogInformation($"Legal entity endpoint '{id}' enabled");
      return r;
    }

    public async Task<bool> ResetAsync(int id)
    {
      var r = await _legalEntityRepository.ResetDeltaLinkAsync(id);
      _logger.LogInformation($"Legal entity endpoint '{id}' deltalink was cleared");
      return r;
    }
  }
}
