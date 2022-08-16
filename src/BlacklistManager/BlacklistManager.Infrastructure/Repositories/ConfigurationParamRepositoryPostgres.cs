﻿// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Dapper;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Repositories
{
  public class ConfigurationParamRepositoryPostgres : IConfigurationParamRepository
  {
    private readonly string connectionString;

    public ConfigurationParamRepositoryPostgres(string connectionString)
    {
      this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task DeleteAsync(ConfigurationParam configParam)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText = @"DELETE FROM configurationParam WHERE paramKey=@paramKey";
      await connection.ExecuteAsync(cmdText,
        new
        {
          paramKey = configParam.Key
        });
    }

    public async Task<IEnumerable<ConfigurationParam>> GetAsync()
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText = @"SELECT paramKey, paramValue FROM configurationParam";
      var all = await connection.QueryAsync<ConfigurationParam>(cmdText);
      return all.ToArray();
    }

    public async Task<ConfigurationParam> GetAsync(ConfigurationParam configParam)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText = @"SELECT paramKey, paramValue FROM configurationParam WHERE paramKey=@paramKey";
      var all = await connection.QueryAsync<ConfigurationParam>(cmdText,
        new
        {
          paramKey = configParam.Key
        });
      return all.SingleOrDefault();
    }

    public async Task<ConfigurationParam> InsertAsync(ConfigurationParam configParam)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string cmdText =
        @"INSERT INTO configurationParam (paramKey, paramValue) " +
        "VALUES (@paramKey, @paramValue) " +
        "ON CONFLICT (paramKey) DO NOTHING " +
        "RETURNING * ";
      var all = await connection.QueryAsync<ConfigurationParam>(cmdText,
        new
        {
          paramKey = configParam.Key,
          paramValue = configParam.Value
        });
      return all.SingleOrDefault();
    }

    public async Task<bool> UpdateAsync(ConfigurationParam configParam)
    {
      using var connection = new NpgsqlConnection(connectionString);
      RetryUtils.Exec(() => connection.Open());
      string update =
      "UPDATE configurationParam " +
      "SET paramValue=@paramValue " +
      "WHERE paramKey = @paramKey";

      int recordsAffected = await connection.ExecuteAsync(update,
        new
        {
          paramKey = configParam.Key,
          paramValue = configParam.Value
        });
      return recordsAffected > 0;
    }
  }
}
