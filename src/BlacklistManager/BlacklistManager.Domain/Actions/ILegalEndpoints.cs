// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public interface ILegalEndpoints
  {
    Task UpdateDeltaLinkAsync(int legalEntityEndpointId, string deltaLink, int processedOrdersCount);

    Task UpdateLastErrorAsync(int legalEntityEndpointId, string lastError, bool increaseFailureCount);

    Task<LegalEntityEndpoint> CreateAsync(string baseUrl, string apiKey);
    Task<IEnumerable<LegalEntityEndpoint>> GetAsync();
    Task<LegalEntityEndpoint> GetAsync(int id);
    Task<bool> UpdateAsync(int id, string baseUrl, string apiKey);
    Task<bool> DisableAsync(int id);
    Task<bool> EnableAsync(int id);
    Task<bool> ResetAsync(int id);
  }
}
