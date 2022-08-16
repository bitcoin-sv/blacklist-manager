// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Dapper;
using Npgsql;

namespace BlacklistManager.Infrastructure.Repositories
{
  public class TrustListRepositoryPostgres : ITrustListRepository 
  {
    private readonly string connectionString;

    public TrustListRepositoryPostgres(string connectionString)
    {
      this.connectionString = connectionString;
    }

    public TrustListItem CreatePublicKey(string publicKey, bool trusted, string remarks)
    {

      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string insertOrUpdate =
          "INSERT INTO TrustList " +
          "  (publicKey, trusted, createdAt, updateAt, remarks) " +
          "  VALUES (@publicKey, @trusted, @createdAt, @updateAt, @remarks)" +
          "  ON CONFLICT (publickey) DO NOTHING " +
          "  RETURNING *"
          ;

          var now = DateTime.UtcNow;

          var trustItems = connection.Query<TrustListItem>(insertOrUpdate,
            new
            {
              publicKey = publicKey.ToLower(),
              trusted,
              createdAt = now,
              updateAt = now,
              remarks
            },
            transaction
          ).SingleOrDefault();
          transaction.Commit();

          return trustItems;
        }
      }
    }

    public bool UpdatePublicKey(string publicKey, bool trusted, string remarks)
    {

      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string update =
          "UPDATE TrustList " +
          "  SET trusted=@trusted, remarks=@remarks, updateAt=@updateAt " +
          "  WHERE publicKey = @publicKey";
          ;

          var now = DateTime.UtcNow;

          int recordAffected = connection.Execute(update,
            new
            {
              publicKey = publicKey.ToLower(),
              trusted,
              updateAt = now,
              remarks
            },
            transaction
          );
          transaction.Commit();

          return recordAffected > 0;
        }
      }
    }

    public IEnumerable<TrustListItem> GetPublicKeys()
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmd = "SELECT * from TrustList ORDER by publicKey";

          return connection.Query<TrustListItem>(cmd, null, transaction);
        }
      }
    }

    public TrustListItem GetPublicKey(string publicKey)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmd = "SELECT * from TrustList WHERE publicKey = @publickey ";
          return connection.Query<TrustListItem>(cmd,
            new
            {
              publicKey = publicKey.ToLower()
            },
            transaction
          ).FirstOrDefault();
        }
      }
    }

    public int DeletePublicKey(string publicKey)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmd = "DELETE FROM TrustList WHERE publicKey = @publickey";

          var result = connection.Execute(cmd,
            new
            {
              publicKey = publicKey.ToLower()
            },
            transaction
          );
          transaction.Commit();
          return result;
        }
      }
    }


    public bool IsPublicKeyTrusted(string publicKey)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmdText =
          "SELECT 1 from TrustList WHERE publicKey=@publickey AND trusted = true";
          var result = connection.Query<int>(
            cmdText,
            new
            {
              publicKey = publicKey.ToLower(),
            },
            transaction).Any();

          return result;
        }
      }

    }
  }
}