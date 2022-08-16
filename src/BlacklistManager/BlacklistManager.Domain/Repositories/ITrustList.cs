// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using BlacklistManager.Domain.Models;

namespace BlacklistManager.Domain.Repositories
{

  public interface ITrustListRepository
  {
    public bool IsPublicKeyTrusted(string publicKey);
    
    /// <summary>
    /// Returns null if key already exists
    /// </summary>
    TrustListItem CreatePublicKey(string publicKey, bool trusted, string remarks);

    /// <summary>
    /// Updates existing entry, returns false if it does not exists
    /// </summary>
    bool UpdatePublicKey(string publicKey, bool trusted, string remarks);

    IEnumerable<TrustListItem> GetPublicKeys();

    TrustListItem GetPublicKey(string publicKey);

    int DeletePublicKey(string publicKey);

  }

  
}
