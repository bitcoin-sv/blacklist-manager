// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.ExternalServices
{
  public interface IRestClient
  {
    public string BaseURL { get; set; }
    Task<string> RequestAsync(HttpMethod httpMethod, string apiMethod, string jsonRequest, bool throwExceptionOn404 = true, TimeSpan? requestTimeout = null);
  }
}
