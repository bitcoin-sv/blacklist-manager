// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServiceViewModel;
using BlacklistManager.Domain.Models;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.ExternalServices
{
  public interface ILegalEntity
  {
    public string BaseUrl { get; set; }
    /// <summary>
    /// Get consensus activation from legal entity endpoint.
    /// Returns null if legal entity endpoint returned 404, meaning there is no consensus yet
    /// </summary>
    /// <returns></returns>
    Task<ConsensusActivation> GetConsensusActivationAsync(string courtOrderHash);

    /// <summary>
    /// Gets court orders from legal entity endpoint
    /// </summary>
    /// <returns></returns>
    Task<CourtOrdersViewModel> GetCourtOrdersAsync(bool ignoreAPIMethod);

    /// <summary>
    /// Post court order acceptances to legal entity endpoints
    /// </summary>
    /// <returns></returns>
    Task PostCourtOrderAcceptanceAsync(string courtOrderHash, string coAcceptanceJsonEnvelope);

    /// <summary>
    /// Method to test if provided keys return any block hashes that were mined with those keys
    /// (used to check if blacklist manager has valid keys that are associated with blocks)
    /// </summary>
    /// <returns></returns>
    Task<string> CheckMinedBlocksAsync(string requestPayload);

    /// <summary>
    /// Method to test NT connection and if the key that NT uses to sign messages is trusted
    /// </summary>
    /// <returns></returns>
    public Task<string> GetPublicKeyAsync();
  }
}
