// Copyright (c) 2020 Bitcoin Association

using System.Net.Http;

namespace Common.BitcoinRpc
{
  public interface IBitcoinRpcHttpClientFactory
  {
    HttpClient CreateClient(string clientName);
  }
}
