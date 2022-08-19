// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Actions
{
  public class DelegatedKeys : IDelegatedKeys
  {
    private readonly IDelegatedKeyRepositoryPostgres _delegatedKeysRepository;
    private readonly ILogger<BlackListManagerLogger> _logger;
    readonly AppSettings _appSettings;

    public DelegatedKeys(IDelegatedKeyRepositoryPostgres delegatedKeysRepository,
                         ILogger<BlackListManagerLogger> logger,
                         IOptions<AppSettings> appSettings)
    {
      _delegatedKeysRepository = delegatedKeysRepository ?? throw new ArgumentNullException(nameof(delegatedKeysRepository));
      _appSettings = appSettings.Value ?? throw new ArgumentNullException(nameof(appSettings));
      _logger = logger;
    }

    public Task ActivateDelegatedKeyAsync(int delegatedKeyId)
    {
      return _delegatedKeysRepository.ActivateDelegatedKeyAsync(delegatedKeyId, DateTime.UtcNow);
    }

    public async Task<int> GetDelegatedKeyIdAsync(string publicKey)
    {
      return await _delegatedKeysRepository.GetDelegatedKeyIdAsync(publicKey);
    }

    public async Task<IEnumerable<DelegatedKey>> GetDelegatedKeysAsync(int? id)
    {
      return await _delegatedKeysRepository.GetDelegatedKeysAsync(id);
    }

    public async Task<IEnumerable<DelegatingKey>> GetDelegatingKeysAsync(int? id)
    {
      return await _delegatedKeysRepository.GetDelegatingKeysAsync(id);
    }

    public async Task<int>InsertDelegatedKeyAsync(byte[] privateKey, string delegatedKey, bool delegationRequired, bool isActive)
    {
      var delegatedKeyId = await _delegatedKeysRepository.InsertDelegatedKeyAsync(privateKey, delegatedKey, delegationRequired, isActive);

      if (isActive)
      {
        await ActivateDelegatedKeyAsync(delegatedKeyId);
      }
      return delegatedKeyId;
    }

    public async Task<int> InsertDelegatingKeyAsync(string publicKeyAddress, string publicKey, string delegatedKeyJSON, DateTime createdAt, int delegatedKeyId)
    {
      return await _delegatedKeysRepository.InsertDelegatingKeyAsync(publicKeyAddress, publicKey, delegatedKeyJSON, createdAt, delegatedKeyId);
    }

    public async Task MarkDelegatingKeyValidatedAsync(int delegatingKey, string signedDelegatedKeyJSON)
    {
      await _delegatedKeysRepository.MarkDelegatingKeyValidatedAsync(delegatingKey, signedDelegatedKeyJSON, DateTime.UtcNow);
    }

    public async Task<ActiveDelegatingKey> GetActiveKeyForSigningAsync()
    {
      var activeDelegatedKey = (await _delegatedKeysRepository.GetDelegatedKeysAsync(null)).SingleOrDefault(x => x.IsActive);
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

      var delegatingKeys = await _delegatedKeysRepository.GetValidDelegatingKeysAsync(activeDelegatedKey.DelegatedKeyId);

      return new ActiveDelegatingKey 
      { 
        DelegatedPublicKey = activeDelegatedKey.PublicKey,
        DelegatedPrivateKey = activeDelegatedKey.PrivateKey,
        DelegationRequired = true,
        SignedDelegatedKeyJSON = delegatingKeys.Select(x => x.SignedDelegatedKeyJSON).ToArray()
      };
    }

    public async Task CreateInitialSignerKeyAsync(Network network, bool requireActivation)
    {
      if ((await GetDelegatedKeysAsync(null)).Any())
      {
        // First key is already present in database
        return;
      }

      _logger.LogInformation("Delegatedkey table does not contain any keys for signing documents. Will insert first key, that needs to be activated.");
      var firstKey = new Key();
      var encrypted = EncryptionTools.Encrypt(firstKey.ToString(network), _appSettings.EncryptionKey);
      await InsertDelegatedKeyAsync(encrypted, firstKey.PubKey.ToHex(), requireActivation, !requireActivation);
      _logger.LogInformation("Key inserted successfully");
    }
  }
}
