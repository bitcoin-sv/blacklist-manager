// Copyright (c) 2020 Bitcoin Association

using System.Threading;

namespace BlacklistManager.Domain.ExternalServices
{
  public interface IBitcoindFactory
  {
    IBitcoind Create(string host, int port, string username, string password);
    IBitcoind Create(string host, int port, string username, string password, CancellationToken cancellationToken);
  }
}
