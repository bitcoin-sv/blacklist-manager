// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Repositories
{
  public interface ILegalEntityRepository
  {
    Task<IEnumerable<LegalEntityEndpoint>> GetAsync();

    Task<LegalEntityEndpoint> GetAsync(int id);

    Task UpdateDeltaLinkAsync(int legalEntityEndpointId, DateTime? lastContactedAt, string deltaLink, int processedOrdersCount);

    Task SetErrorAsync(int legalEntityEndpointId, string lastError, DateTime? lastErrorAt, bool increaseFailureCount);

    /// <summary>
    /// Legal entity is marked as enabled/disabled (validUntil=now/null)
    /// </summary>
    Task<bool> UpdateStatusAsync(int id, bool enabled);

    Task<bool> UpdateAsync(LegalEntityEndpoint legalEntityEndpoint);

    Task<bool> ResetDeltaLinkAsync(int id);

    Task<LegalEntityEndpoint> InsertAsync(LegalEntityEndpoint legalEntityEndpoint);
  }
}
