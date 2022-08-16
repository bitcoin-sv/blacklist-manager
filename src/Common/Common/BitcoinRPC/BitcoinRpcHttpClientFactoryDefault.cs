// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;

namespace Common.BitcoinRpc
{
  public class BitcoinRpcHttpClientFactoryDefault : IBitcoinRpcHttpClientFactory
  {
    readonly IHttpClientFactory factory;
    public BitcoinRpcHttpClientFactoryDefault(IHttpClientFactory defaultFactory)
    {
      this.factory = defaultFactory ?? throw new ArgumentNullException(nameof(defaultFactory));

    }

    public HttpClient CreateClient(string clientName)
    {
      return factory.CreateClient(clientName);
    }
  }
}
