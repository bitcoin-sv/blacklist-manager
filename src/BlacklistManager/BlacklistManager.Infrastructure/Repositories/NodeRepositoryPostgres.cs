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
  public class NodeRepositoryPostgres : INodeRepository
  {

    private readonly string _connectionString;

    public NodeRepositoryPostgres(IConfiguration configuration)
    {
      _connectionString = configuration["BlacklistManagerConnectionStrings:DBConnectionString"];
    }

    public async Task<Node> CreateNodeAsync(Node node)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string insertOrUpdate =
        "INSERT INTO Node " +
        "  (host, port, username, password, nodestatus, remarks) " +
        "  VALUES (@host, @port, @username, @password, @nodestatus, @remarks)" +
        "  ON CONFLICT (host, port) DO NOTHING " +
        "  RETURNING *"
      ;

      var now = DateTime.UtcNow;

      var insertedNode = (await connection.QueryAsync<Node>(insertOrUpdate,
        new
        {
          host = node.Host.ToLower(),
          port = node.Port,
          username = node.Username,
          password = node.Password,
          nodestatus = node.Status,
          remarks = node.Remarks
        },
        transaction
      )).SingleOrDefault();
      await transaction.CommitAsync();

      return insertedNode;
    }

    public async Task<bool> UpdateNodeAsync(Node node)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string update =
      "UPDATE Node " +
      "  SET  username=@username, password=@password, remarks=@remarks " +
      "  WHERE host=@host AND port=@port";


      int recordAffected = await connection.ExecuteAsync(update,
        new
        {
          host = node.Host.ToLower(),
          port = node.Port,
          username = node.Username,
          password = node.Password,
            //nodestatus = node.Status, // NodeStatus is not present in ViewModel
            remarks = node.Remarks
        },
        transaction
      );
      await transaction.CommitAsync();

      return recordAffected > 0;
    }

    public async Task<bool> UpdateNodeErrorAsync(Node node)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string update =
      "UPDATE Node " +
      "  SET  lastError=@lastError, lastErrorAt=@lastErrorAt " +
      "  WHERE nodeId=@nodeId";

      int recordAffected = await connection.ExecuteAsync(update,
        new
        {
          lastError = node.LastError,
          lastErrorAt = node.LastErrorAt,
          nodeId = node.Id
        },
        transaction
      );
      await transaction.CommitAsync();

      return recordAffected > 0;
    }

    public async Task<Node> GetNodeAsync(string hostAndPort)
    {
      var (host, port) = Node.SplitHostAndPort(hostAndPort);

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmd = "SELECT nodeId, host, port, username, password, remarks, nodeStatus, lastError, lastErrorAt  FROM Node WHERE host = @host AND  port = @port";
      return (await connection.QueryAsync<Node>(cmd,
        new
        {
          host = host.ToLower(),
          port
        },
        transaction
      )).FirstOrDefault();
    }

    public async Task<int> DeleteNodeAsync(string hostAndPort)
    {
      var (host, port) = Node.SplitHostAndPort(hostAndPort);

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmd =
"DELETE FROM fundstatenode WHERE nodeid=(SELECT nodeId FROM Node WHERE host = @host AND  port = @port); " +
"DELETE FROM Node WHERE host = @host AND  port = @port;";
      var result = await connection.ExecuteAsync(cmd,
        new
        {
          host = host.ToLower(),
          port
        },
        transaction
      );
      await transaction.CommitAsync();
      return result;
    }

    public async Task<IEnumerable<Node>> GetNodesAsync()
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmdText =
        @"SELECT nodeId, host, port, username, password, remarks, nodeStatus, lastError, lastErrorAt FROM node ORDER by host, port";
      return await connection.QueryAsync<Node>(cmdText, null, transaction);
    }
  }
}
