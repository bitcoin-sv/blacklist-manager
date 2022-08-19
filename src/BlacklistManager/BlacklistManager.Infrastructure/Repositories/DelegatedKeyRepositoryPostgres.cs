// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Dapper;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Repositories
{
  public class DelegatedKeyRepositoryPostgres : IDelegatedKeyRepositoryPostgres
  {
    private readonly string _connectionString;

    public DelegatedKeyRepositoryPostgres(
      IConfiguration configuration)
    {
      _connectionString = configuration["BlacklistManagerConnectionStrings:DBConnectionString"];
    }

    public async Task<int> InsertDelegatedKeyAsync(byte[] privateKey, string publicKey, bool delegationRequired, bool isActive)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();
      
      string cmdText = @"
INSERT INTO DelegatedKey (privateKey, publicKey, delegationRequired, isActive, createdAt) 
VALUEs (@privateKey, @publicKey, @delegationRequired, @isActive, @createdAt) 
ON CONFLICT (privateKey) DO NOTHING
RETURNING delegatedKeyId;";
      var id = await transaction.Connection.ExecuteScalarAsync<int>(cmdText,
                                                         new
                                                         {
                                                           privateKey,
                                                           publicKey,
                                                           delegationRequired,
                                                           isActive,
                                                           createdAt = DateTime.UtcNow
                                                         });

      await transaction.CommitAsync();
      return id;
    }

    public async Task<int> InsertDelegatingKeyAsync(string publicKeyAddress, string publicKey, string delegatedKeyJSON, DateTime createdAt, int delegatedKeyId)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();

      string cmdText = @"
INSERT INTO DelegatingKey (publicKeyAddress, publicKey, dataToSign, createdAt, delegatedKeyId) 
VALUES (@publicKeyAddress, @publicKey, @dataToSign, @createdAt, @delegatedKeyId) 
ON CONFLICT (publicKey, publicKeyAddress) DO NOTHING
RETURNING delegatingKeyId;";
      var id = await transaction.Connection.ExecuteScalarAsync<int>(cmdText,
                                                         new
                                                         {
                                                           publicKeyAddress,
                                                           publicKey,
                                                           dataToSign = delegatedKeyJSON,
                                                           createdAt,
                                                           delegatedKeyId
                                                         });

      await transaction.CommitAsync();

      return id;
    }

    public async Task<IEnumerable<DelegatedKey>> GetDelegatedKeysAsync(int? delegatedKeyId)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();

      string cmdText = "SELECT * FROM DelegatedKey " +
                       "WHERE @delegatedKeyId IS NULL OR delegatedKeyId = @delegatedKeyId";
      return await transaction.Connection.QueryAsync<DelegatedKey>(cmdText, new { delegatedKeyId });
    }

    public async Task<IEnumerable<DelegatingKey>> GetDelegatingKeysAsync(int? delegatingKeyId)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();

      string cmdText = @"SELECT a.*, b.publicKey DelegatedPublicKey FROM DelegatingKey a 
                         INNER JOIN DelegatedKey b on a.delegatedkeyid = b.delegatedkeyid 
                         WHERE @delegatingKeyId IS NULL OR a.delegatingKeyId = @delegatingKeyId";
      return await transaction.Connection.QueryAsync<DelegatingKey>(cmdText, new { delegatingKeyId });
    }

    public async Task<int> GetDelegatedKeyIdAsync(string publicKey)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();

      string cmdText = "SELECT delegatedKeyId FROM DelegatedKey WHERE publicKey = @publicKey";
      return await transaction.Connection.ExecuteScalarAsync<int>(cmdText, new { publicKey });
    }

    public async Task ActivateDelegatedKeyAsync(int delegatedKeyId, DateTime activationDate)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();

      string cmdTextDeactivate = "UPDATE DelegatedKey SET isActive = false";
      await transaction.Connection.ExecuteAsync(cmdTextDeactivate);

      string cmdText = "UPDATE DelegatedKey SET isActive = true, activatedAt = @activationDate " +
                       "WHERE delegatedKeyId = @delegatedKeyId";

      await transaction.Connection.ExecuteAsync(cmdText, new { delegatedKeyId, activationDate });
      await transaction.CommitAsync();
    }

    public async Task MarkDelegatingKeyValidatedAsync(int delegatingKeyId, string signedDelegatedKeyJSON, DateTime validatedAt)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();

      string cmdText = @"UPDATE DelegatingKey SET validatedAt = @validatedAt, dataToSign = null, signedDelegatedKeyJson = @signedDelegatedKeyJSON 
                         WHERE delegatingKeyId = @delegatingKeyId";

      await transaction.Connection.ExecuteAsync(cmdText, new { delegatingKeyId, signedDelegatedKeyJSON, validatedAt });
      await transaction.CommitAsync();
    }

    public async Task<IEnumerable<DelegatingKey>> GetValidDelegatingKeysAsync(int delegatedKeyId)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();

      string cmdText = "SELECT * " +
                       "FROM delegatingkey a " +
                       "WHERE a.validatedat IS NOT NULL AND delegatedKeyId = @delegatedKeyId";

      var activeKeys = await transaction.Connection.QueryAsync<DelegatingKey>(cmdText, new { delegatedKeyId });
      return activeKeys;
    }
  }
}
