// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Repositories
{
  public interface IDelegatedKeyRepositoryPostgres
  {
    public Task<IEnumerable<DelegatedKey>> GetDelegatedKeysAsync(int? delegatedKeyId);

    public Task<IEnumerable<DelegatingKey>> GetDelegatingKeysAsync(int? delegatingKeyId);

    public Task<int> InsertDelegatedKeyAsync(byte[] privateKey, string publicKey, bool delegationRequired, bool isActive);

    public Task<int> InsertDelegatingKeyAsync(string publicKeyAddress, string publicKey, string delegatedKeyJSON, DateTime createdAt, int delegatedKeyId);

    public Task<int> GetDelegatedKeyIdAsync(string publicKey);

    public Task ActivateDelegatedKeyAsync(int delegatedKeyId, DateTime activationDate);

    public Task MarkDelegatingKeyValidatedAsync(int delegatingKeyId, string signedDelegatedKeyJSON, DateTime validatedAt);

    public Task<IEnumerable<DelegatingKey>> GetValidDelegatingKeysAsync(int delegatedKeyId);
  }
}
