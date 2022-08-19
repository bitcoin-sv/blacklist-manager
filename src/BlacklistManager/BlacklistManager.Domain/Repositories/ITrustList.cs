// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;
using BlacklistManager.Domain.Models;

namespace BlacklistManager.Domain.Repositories
{

  public interface ITrustListRepository
  {
    Task<bool> IsPublicKeyTrustedAsync(string publicKey);
    
    /// <summary>
    /// Returns null if key already exists
    /// </summary>
    Task<TrustListItem> CreatePublicKeyAsync(string publicKey, bool trusted, string remarks);

    /// <summary>
    /// Updates existing entry, returns false if it does not exists
    /// </summary>
    Task<bool> UpdatePublicKeyAsync(string publicKey, bool trusted, string remarks, string replacedBy);

    Task<IEnumerable<TrustListItem>> GetPublicKeysAsync();

    Task<TrustListItem> GetPublicKeyAsync(string publicKey);

    Task<int> DeletePublicKeyAsync(string publicKey);

    public Task<List<TrustListItem>> GetTrustListChainAsync(string publicKey);
  }


}
