// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public interface ILegalEndpoints
  {
    Task<IEnumerable<LegalEntityEndpoint>> GetLegalEntitiyEndpointsAsync();

    void UpdateDeltaLink(int legalEntityEndpointId, string deltaLink);

    void UpdateLastError(int legalEntityEndpointId, string lastError);

    Task<LegalEntityEndpoint> CreateAsync(string baseUrl, string apiKey);
    Task<IEnumerable<LegalEntityEndpoint>> GetAsync();
    Task<LegalEntityEndpoint> GetAsync(int id);
    Task<bool> UpdateAsync(int id, string baseUrl, string apiKey);
    Task<bool> DisableAsync(int id);
    Task<bool> EnableAsync(int id);
    Task<bool> ResetAsync(int id);
  }
}
