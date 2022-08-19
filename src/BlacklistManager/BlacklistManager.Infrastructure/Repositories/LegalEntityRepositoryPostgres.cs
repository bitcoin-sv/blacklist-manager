// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Dapper;
using Microsoft.Extensions.Configuration;

namespace BlacklistManager.Infrastructure.Repositories
{
  public class LegalEntityRepositoryPostgres : ILegalEntityRepository
  {
    private readonly string _connectionString;

    public LegalEntityRepositoryPostgres(
      IConfiguration configuration)
    {
      _connectionString = configuration["BlacklistManagerConnectionStrings:DBConnectionString"];
    }

    public async Task<IEnumerable<LegalEntityEndpoint>> GetAsync()
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();
      string cmd =
        @"
SELECT legalEntityEndpointId, 
       baseUrl, 
       apiKey, 
       createdAt, 
       validUntil, 
       lastContactedAt, 
       lastErrorAt, 
       lastError, 
       courtOrderSyncToken, 
       courtOrderAcceptanceSyncToken, 
       courtOrderDeltaLink,
       processedOrdersCount,
       failureCount
FROM LegalEntityEndpoint
WHERE validUntil IS NULL OR  validUntil > @validUntil";
      return await transaction.Connection.QueryAsync<LegalEntityEndpoint>(cmd, new { validUntil = DateTime.UtcNow }, transaction);
    }

    public async Task<LegalEntityEndpoint> GetAsync(int id)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmd =
        @"
SELECT legalEntityEndpointId, 
       baseUrl, 
       apiKey, 
       createdAt, 
       validUntil, 
       lastContactedAt, 
       lastErrorAt, 
       lastError, 
       courtOrderSyncToken, 
       courtOrderAcceptanceSyncToken, 
       courtOrderDeltaLink,
       processedOrdersCount,
       failureCount
FROM LegalEntityEndpoint 
WHERE legalEntityEndpointId=@id ";

      var all = await connection.QueryAsync<LegalEntityEndpoint>(cmd, new { id });
      return all.SingleOrDefault();
    }

    public async Task UpdateDeltaLinkAsync(int legalEntityEndpointId, DateTime? lastContactedAt, string deltaLink, int processedOrdersCount)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmd = @"
UPDATE LegalEntityEndpoint 
SET lastContactedAt=@lastContactedAt,
    courtOrderDeltaLink = @deltaLink,
    processedOrdersCount = processedOrdersCount + @processedOrdersCount
WHERE legalEntityEndpointId=@legalEntityEndpointId";

      await transaction.Connection.ExecuteAsync(cmd, new { legalEntityEndpointId, lastContactedAt, deltaLink, processedOrdersCount });
      await transaction.CommitAsync();
    }

    public async Task SetErrorAsync(int legalEntityEndpointId, string lastError, DateTime? lastErrorAt, bool increaseFailureCount)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      string cmd = $@"
UPDATE LegalEntityEndpoint 
SET lastErrorAt=@lastErrorAt,
    lastError=@lastError,
    failureCount = failureCount + @failureCount
WHERE legalEntityEndpointId=@legalEntityEndpointId";

      await transaction.Connection.ExecuteAsync(cmd, new { legalEntityEndpointId, lastError, lastErrorAt, failureCount = increaseFailureCount ? 1 : 0 });
      await transaction.CommitAsync();
    }

    public async Task<bool> UpdateStatusAsync(int id, bool enabled)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string valid = enabled ? "null" : "now() at time zone 'utc'";
      string cmdText = $"UPDATE legalEntityEndpoint SET validUntil={valid} WHERE legalEntityEndpointId=@id";
      int recordsAffected = await connection.ExecuteAsync(cmdText,
        new
        {
          id
        });
      return recordsAffected > 0;
    }

    public async Task<bool> ResetDeltaLinkAsync(int id)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = $"UPDATE legalEntityEndpoint SET courtorderdeltalink=null WHERE legalEntityEndpointId=@id";
      int recordsAffected = await connection.ExecuteAsync(cmdText,
        new
        {
          id
        });
      return recordsAffected > 0;
    }

    public async Task<bool> UpdateAsync(LegalEntityEndpoint legalEntityEndpoint)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string set = string.Empty;
      if (!string.IsNullOrEmpty(legalEntityEndpoint.BaseUrl))
      {
        set = "baseUrl=@baseUrl";
      }
      if (!string.IsNullOrEmpty(legalEntityEndpoint.APIKey))
      {
        if (set.Length > 0)
        {
          set += ",";
        }
        set += "apiKey=@apiKey";
      }

      string update = @$"
UPDATE legalEntityEndpoint
SET {set}
WHERE legalEntityEndpointId=@id";

      int recordsAffected = await connection.ExecuteAsync(update,
        new
        {
          id = legalEntityEndpoint.LegalEntityEndpointId,
          baseUrl = legalEntityEndpoint.BaseUrl.ToLower(),
          apiKey = legalEntityEndpoint.APIKey
        });
      return recordsAffected > 0;
    }

    public async Task<LegalEntityEndpoint> InsertAsync(LegalEntityEndpoint legalEntityEndpoint)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = @"
INSERT INTO legalEntityEndpoint (baseUrl, apiKey, createdAt)
VALUES (@baseUrl, @apiKey, now() at time zone 'utc')
ON CONFLICT (baseUrl) DO NOTHING
RETURNING legalEntityEndpointId, baseUrl, apiKey, createdAt ";
      var all = await connection.QueryAsync<LegalEntityEndpoint>(cmdText,
        new
        {
          baseUrl = legalEntityEndpoint.BaseUrl.ToLower(),
          apiKey = legalEntityEndpoint.APIKey
        });
      return all.SingleOrDefault();
    }
  }
}
