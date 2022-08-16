// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Repositories;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public class DelegatedKeys : IDelegatedKeys
  {
    private readonly IDelegatedKeyRepositoryPostgres delegatedKeysRepository;
    private readonly ILogger<BlackListManagerLogger> logger;

    public DelegatedKeys(IDelegatedKeyRepositoryPostgres delegatedKeysRepository,
                         ILogger<BlackListManagerLogger> logger)
    {
      this.delegatedKeysRepository = delegatedKeysRepository ?? throw new ArgumentNullException(nameof(delegatedKeysRepository));
      this.logger = logger;
    }

    public async Task ActivateDelegatedKeyAsync(int delegatedKeyId)
    {
      await delegatedKeysRepository.ActivateDelegatedKeyAsync(delegatedKeyId, DateTime.UtcNow);
    }

    public async Task<int> GetDelegatedKeyIdAsync(string publicKey)
    {
      return await delegatedKeysRepository.GetDelegatedKeyIdAsync(publicKey);
    }

    public async Task<IEnumerable<DelegatedKey>> GetDelegatedKeysAsync(int? id)
    {
      return await delegatedKeysRepository.GetDelegatedKeysAsync(id);
    }

    public async Task<IEnumerable<DelegatingKey>> GetDelegatingKeysAsync(int? id)
    {
      return await delegatedKeysRepository.GetDelegatingKeysAsync(id);
    }

    public async Task<int>InsertDelegatedKeyAsync(byte[] privateKey, string delegatedKey, bool delegationRequired, bool isActive)
    {
      return await delegatedKeysRepository.InsertDelegatedKeyAsync(privateKey, delegatedKey, delegationRequired, isActive);
    }

    public async Task<int> InsertDelegatingKeyAsync(string publicKeyAddress, string publicKey, string delegatedKeyJSON, DateTime createdAt, int delegatedKeyId)
    {
      return await delegatedKeysRepository.InsertDelegatingKeyAsync(publicKeyAddress, publicKey, delegatedKeyJSON, createdAt, delegatedKeyId);
    }

    public async Task MarkDelegatingKeyValidatedAsync(int delegatingKey, string signedDelegatedKeyJSON)
    {
      await delegatedKeysRepository.MarkDelegatingKeyValidatedAsync(delegatingKey, signedDelegatedKeyJSON, DateTime.UtcNow);
    }

    public async Task<ActiveDelegatingKey> GetActiveKeyForSigningAsync()
    {
      var activeDelegatedKey = (await delegatedKeysRepository.GetDelegatedKeysAsync(null)).SingleOrDefault(x => x.IsActive);
      if (activeDelegatedKey == null)
      {
        return null;
      }
      if (!activeDelegatedKey.DelegationRequired)
      {
        return new ActiveDelegatingKey
        {
          DelegatedPrivateKey = activeDelegatedKey.PrivateKey,
          DelegationRequired = false
        };
      }

      var delegatingKeys = await delegatedKeysRepository.GetValidDelegatingKeysAsync(activeDelegatedKey.DelegatedKeyId);

      return new ActiveDelegatingKey 
      { 
        DelegatedPublicKey = activeDelegatedKey.PublicKey,
        DelegatedPrivateKey = activeDelegatedKey.PrivateKey,
        DelegationRequired = true,
        SignedDelegatedKeyJSON = delegatingKeys.Select(x => x.SignedDelegatedKeyJSON).ToArray()
      };
    }
  }
}
