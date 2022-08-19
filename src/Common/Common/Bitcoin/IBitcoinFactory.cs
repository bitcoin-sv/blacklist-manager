// Copyright (c) 2020 Bitcoin Association

namespace Common.Bitcoin
{
  public interface IBitcoinFactory
  {
    IBitcoinRpc Create(string host, int port, string username, string password);
    
    /// <summary>
    /// Creates IBitcoinRpc interface with settings from appsetings.json
    /// </summary>
    /// <returns></returns>
    IBitcoinRpc Create();
  }
}
