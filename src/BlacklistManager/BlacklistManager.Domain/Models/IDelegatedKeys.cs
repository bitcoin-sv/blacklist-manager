// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public interface IDelegatedKeys
  {
    public Task<IEnumerable<DelegatedKey>> GetDelegatedKeysAsync(int? id);

    public Task<IEnumerable<DelegatingKey>> GetDelegatingKeysAsync(int? id);

    public Task<int> InsertDelegatedKeyAsync(byte[] privateKey, string delegatedKey, bool delegationRequired, bool isActive);

    public Task<int> InsertDelegatingKeyAsync(string publicKeyAddress, string publicKey, string delegatedKeyJSON, DateTime createdAt, int delegatedKeyId);

    public Task ActivateDelegatedKeyAsync(int delegatedKeyId);

    public Task<int> GetDelegatedKeyIdAsync(string publicKey);

    public Task MarkDelegatingKeyValidatedAsync(int delegatingKey, string signedDelegatedKeyJSON);

    public Task<ActiveDelegatingKey> GetActiveKeyForSigningAsync();
  }
}
