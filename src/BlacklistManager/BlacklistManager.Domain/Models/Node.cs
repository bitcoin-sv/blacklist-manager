// Copyright (c) 2020 Bitcoin Association

using Common;
using System;

namespace BlacklistManager.Domain.Models
{
  public class Node : DomainModelBase
  {
    public Node(string host, Int32 port, string username, string password, string remarks) 
      : this(int.MinValue, host, port, username, password, remarks, (int)NodeStatus.Connected, null, null)
    {
    }

    public Node(Int32 nodeid, string host, Int32 port, string username, string password, string remarks, Int32 nodestatus, String lasterror, DateTime? lasterrorat) 
      : base(nodeid) 
    {
      Host = host;
      Port = port;
      Username = username;
      Password = password;

      Status = (NodeStatus)nodestatus;
      LastError = lasterror;
      LastErrorAt = lasterrorat;
      Remarks = remarks;
    }

    public string Host { get; private set; }
    public int Port { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }

    public string Remarks { get; private set; }


    public NodeStatus Status { get; private set; }
    public string LastError { get; private set; }
    public DateTime? LastErrorAt { get; private set; }

    public bool HasErrors() => LastErrorAt != null || !string.IsNullOrEmpty(LastError);

    public string ToExternalId()
    {
      return Host + ":" + Port.ToString();
    }

    public override string ToString()
    {
      return ToExternalId();
    }

    public static (string host, int port) SplitHostAndPort(string hostAndPort)
    {
      if (string.IsNullOrEmpty(hostAndPort))
      {
        throw new BadRequestException($"'{nameof(hostAndPort)} must not be empty");
      }

      var split = hostAndPort.Split(':');
      if (split.Length != 2)
      {
        throw new BadRequestException($"'{nameof(hostAndPort)} must be separated by exactly one ':'");
      }

      return (split[0], int.Parse(split[1]));

    }

    public void SetError(Exception httpException)
    {
      LastError = httpException.Message;
      LastErrorAt = DateTime.UtcNow;
    }

    public void ClearError()
    {
      LastError = null;
      LastErrorAt = null;
    }

    public Node Clone()
    {
      return new Node((int)Id, Host, Port, Username, Password, Remarks, (int)Status, LastError, LastErrorAt);
    }
  }
}
