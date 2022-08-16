// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;
using BlacklistManager.Domain.Models;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class NodeViewModelGet // Used when returning data
  {
    public NodeViewModelGet()
    { }

    public NodeViewModelGet(Node domain)
    {
      Id = domain.ToExternalId();
      Username = domain.Username;
      //Password = domain.Password;
      Remarks = domain.Remarks;
      Status= domain.Status;
      LastError = domain.LastError;
      LastErrorAt = domain.LastErrorAt;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; } // Host + port

    [JsonPropertyName("username")]
    public string Username { get; set; }
    
    // For security reason, we never return password
    //[JsonPropertyName("password")]
    //public string Password { get; set; }

    [JsonPropertyName("remarks")]
    public string Remarks { get; set; }

    [JsonPropertyName("status")]
    public NodeStatus Status { get; set; }

    [JsonPropertyName("lastError")]    
    public string LastError { get; set; }

    [JsonPropertyName("lastErrorAt")]
    public DateTime? LastErrorAt { get; set; }
  }
}
