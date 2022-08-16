// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.ExternalServices
{
  public interface IBitcoind
  {
    Task<IEnumerable<Fund>> AddToPolicyBlacklistAsync(IEnumerable<Fund> funds);
    Task<IEnumerable<Fund>> AddToConsensusBlacklistAsync(IEnumerable<Fund> funds);
    Task<IEnumerable<Fund>> RemoveFromPolicyBlacklistAsync(IEnumerable<Fund> funds);
    Task<ClearAllBlacklistsResult> ClearBlacklistsAsync(bool removeAllEntries, int? expirationHeightDelta = null);
    public Task<long> GetBlockCountAsync();
    public Task<long> TestNodeConnectionAsync();
    public Task<string> GetBestBlockHashAsync();
  }
}
