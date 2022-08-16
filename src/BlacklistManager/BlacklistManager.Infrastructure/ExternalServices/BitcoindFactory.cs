// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using Common.BitcoinRpc;
using System;
using System.Threading;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class BitcoindFactory : IBitcoindFactory
  {
    readonly IBitcoinRpcHttpClientFactory bitcoinRpcHttpClientFactory;
    public BitcoindFactory(IBitcoinRpcHttpClientFactory bitcoinRpcHttpClientFactory)
    {
      this.bitcoinRpcHttpClientFactory = bitcoinRpcHttpClientFactory ?? throw new ArgumentNullException(nameof(bitcoinRpcHttpClientFactory));

    }

    public IBitcoind Create(string host, int port, string username, string password)
    {
      return Create(host, port, username, password, new CancellationTokenSource().Token);
    }

    public IBitcoind Create(string host, int port, string username, string password, CancellationToken cancellationToken)
    {
      return new BitcoindRPC(bitcoinRpcHttpClientFactory, host, port, username, password, cancellationToken);
    }
  }
}
