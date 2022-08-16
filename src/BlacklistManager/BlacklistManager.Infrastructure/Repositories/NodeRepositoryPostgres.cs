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
  public class NodeRepositoryPostgres : INodeRepository
  {

    private readonly string connectionString;

    public NodeRepositoryPostgres(string connectionString)
    {
      this.connectionString = connectionString;
    }

    public Node CreateNode(Node node)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string insertOrUpdate =
            "INSERT INTO Node " +
            "  (host, port, username, password, nodestatus, remarks) " +
            "  VALUES (@host, @port, @username, @password, @nodestatus, @remarks)" +
            "  ON CONFLICT (host, port) DO NOTHING " +
            "  RETURNING *"
          ;

          var now = DateTime.UtcNow;

          var insertedNode = connection.Query<Node>(insertOrUpdate,
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
          ).SingleOrDefault();
          transaction.Commit();

          return insertedNode;
        }
      }
    }

    public bool UpdateNode(Node node)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string update =
          "UPDATE Node " +
          "  SET  username=@username, password=@password, remarks=@remarks " +
          "  WHERE host=@host AND port=@port";


          int recordAffected = connection.Execute(update,
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
          transaction.Commit();

          return recordAffected > 0;
        }
      }
    }

    public bool UpdateNodeError(Node node)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string update =
          "UPDATE Node " +
          "  SET  lastError=@lastError, lastErrorAt=@lastErrorAt " +
          "  WHERE nodeId=@nodeId";

          int recordAffected = connection.Execute(update,
            new
            {
              lastError = node.LastError,
              lastErrorAt = node.LastErrorAt,
              nodeId = node.Id
            },
            transaction
          );
          transaction.Commit();

          return recordAffected > 0;
        }
      }
    }

    public Node GetNode(string hostAndPort)
    {
      var (host, port) = Node.SplitHostAndPort(hostAndPort);

      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmd = "SELECT nodeId, host, port, username, password, remarks, nodeStatus, lastError, lastErrorAt  FROM Node WHERE host = @host AND  port = @port";
          return connection.Query<Node>(cmd,
            new
            {
              host = host.ToLower(),
              port
            },
            transaction
          ).FirstOrDefault();
        }
      }
    }

    public int DeleteNode(string hostAndPort)
    {
      var (host, port) = Node.SplitHostAndPort(hostAndPort);

      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmd =
"DELETE FROM fundstatenode WHERE nodeid=(SELECT nodeId FROM Node WHERE host = @host AND  port = @port); " +
"DELETE FROM Node WHERE host = @host AND  port = @port;";
          var result =  connection.Execute(cmd,
            new
            {
              host = host.ToLower(),
              port
            },
            transaction
          );
          transaction.Commit();
          return result;
        }
      }
    }

    public IEnumerable<Node> GetNodes()
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmdText =
            @"SELECT nodeId, host, port, username, password, remarks, nodeStatus, lastError, lastErrorAt FROM node ORDER by host, port";
          return connection.Query<Node>(cmdText, null, transaction);
        }
      }
    }
  }
}
