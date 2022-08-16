// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;

namespace BlacklistManager.Infrastructure.Authentication
{
  public class ApiKey
  {
    public ApiKey(int id, string key, DateTime created, IReadOnlyCollection<string> roles)
    {
      Id = id;
      Key = key ?? throw new ArgumentNullException(nameof(key));
      Created = created;
      Roles = roles ?? throw new ArgumentNullException(nameof(roles));
    }

    public int Id { get; }
    public string Key { get; }
    public DateTime Created { get; }
    public IReadOnlyCollection<string> Roles { get; }
  }
}
