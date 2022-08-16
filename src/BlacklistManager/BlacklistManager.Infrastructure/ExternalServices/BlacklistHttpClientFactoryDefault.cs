// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using System;
using System.Net.Http;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class BlacklistHttpClientFactoryDefault: IBlacklistHttpClientFactory
  {
    public const string ClientName = "BlacklistManager.Service.Http.Client";
    readonly IHttpClientFactory factory;
    public BlacklistHttpClientFactoryDefault(IHttpClientFactory defaultFactory)
    {
      factory = defaultFactory ?? throw new ArgumentNullException(nameof(defaultFactory));
    }

    public HttpClient CreateClient()
    {
      return factory.CreateClient(ClientName);
    }
  }
}
