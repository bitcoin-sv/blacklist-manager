// Copyright (c) 2020 Bitcoin Association

using System.Net.Http;

namespace BlacklistManager.Domain.ExternalServices
{
  public interface IBlacklistHttpClientFactory
  {
    HttpClient CreateClient();
  }
}
