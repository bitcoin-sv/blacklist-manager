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
  public class TrustListRepositoryPostgres : ITrustListRepository
  {
    private readonly string _connectionString;

    public TrustListRepositoryPostgres(IConfiguration configuration)
    {
      _connectionString = configuration["BlacklistManagerConnectionStrings:DBConnectionString"];
    }

    public async Task<TrustListItem> CreatePublicKeyAsync(string publicKey, bool trusted, string remarks)
    {

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string insertOrUpdate = @"
INSERT INTO TrustList
(publicKey, trusted, createdAt, updateAt, remarks)
VALUES (@publicKey, @trusted, @createdAt, @updateAt, @remarks)
ON CONFLICT (publickey) DO NOTHING
RETURNING *;
";
      var now = DateTime.UtcNow;

      var trustItems = (await connection.QueryAsync<TrustListItem>(insertOrUpdate,
        new
        {
          publicKey = publicKey.ToLower(),
          trusted,
          createdAt = now,
          updateAt = now,
          remarks,
        },
        transaction
      )).SingleOrDefault();
      await transaction.CommitAsync();

      return trustItems;
    }

    public async Task<bool> UpdatePublicKeyAsync(string publicKey, bool trusted, string remarks, string replacedBy)
    {

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string update = @"
UPDATE TrustList 
SET trusted=@trusted, remarks=@remarks, updateAt=@updateAt, replacedBy = @replacedBy
WHERE publicKey = @publicKey; ";

      var now = DateTime.UtcNow;

      int recordAffected = await connection.ExecuteAsync(update,
        new
        {
          publicKey = publicKey.ToLower(),
          trusted,
          updateAt = now,
          remarks,
          replacedBy
        },
        transaction
      );
      await transaction.CommitAsync();

      return recordAffected > 0;
    }

    public async Task<IEnumerable<TrustListItem>> GetPublicKeysAsync()
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmd = "SELECT * from TrustList ORDER by publicKey";

      return await connection.QueryAsync<TrustListItem>(cmd, null, transaction);
    }

    public async Task<TrustListItem> GetPublicKeyAsync(string publicKey)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmd = "SELECT * from TrustList WHERE publicKey = @publickey ";
      return (await connection.QueryAsync<TrustListItem>(cmd,
        new
        {
          publicKey = publicKey.ToLower()
        },
        transaction
      )).FirstOrDefault();
    }

    public async Task<int> DeletePublicKeyAsync(string publicKey)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmd = "DELETE FROM TrustList WHERE publicKey = @publickey";

      var result = await connection.ExecuteAsync(cmd,
        new
        {
          publicKey = publicKey.ToLower()
        },
        transaction
      );
      await transaction.CommitAsync();
      return result;
    }


    public async Task<bool> IsPublicKeyTrustedAsync(string publicKey)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmdText =
      "SELECT 1 from TrustList WHERE publicKey=@publickey AND trusted = true";
      var result = (await connection.QueryAsync<int>(
        cmdText,
        new
        {
          publicKey = publicKey.ToLower(),
        },
        transaction)).Any();

      return result;
    }

    public async Task<List<TrustListItem>> GetTrustListChainAsync(string publicKey)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = @"
WITH RECURSIVE trustListChain AS
(
	SELECT t.publicKey, t.trusted, t.replacedBy 
	FROM trustList t 
	WHERE t.publicKey = @publicKey
	
	UNION ALL 
	
	SELECT t1.publicKey, t1.trusted, t1.replacedBy 
	FROM trustList t1
	INNER JOIN trustListChain tc ON t1.replacedBy = tc.publicKey
) 
SELECT * FROM trustListChain;
";

      var result = (await connection.QueryAsync<TrustListItem>(cmdText, new { publicKey = publicKey.ToLower() })).ToList();

      return result;
    }
  }
}