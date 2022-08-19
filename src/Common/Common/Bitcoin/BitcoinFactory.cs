// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient;
using Microsoft.Extensions.Logging;
using NBitcoin;
using System;
using System.Net.Http;
using System.Text;
using static Common.Consts;

namespace Common.Bitcoin
{
  public class BitcoinRpc {} // Used only for logger category

  public class BitcoinFactory : IBitcoinFactory
  {
    private readonly IRpcClient _rpcClient;
    private readonly ILogger<BitcoinRpc> _logger;
    private readonly string _blockChainType;
    private readonly Network _network;
    readonly IHttpClientFactory _httpClientFactory;

    public BitcoinFactory(string blockChainType, Network network, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
      _blockChainType = blockChainType;
      _logger = loggerFactory.CreateLogger<BitcoinRpc>();
      _network = network;
      _httpClientFactory = httpClientFactory;
    }

    public BitcoinFactory(IRpcClient rpcClient, ILoggerFactory loggerFactory, Network network, string blockChainType)
    {
      _rpcClient = rpcClient;
      _logger = loggerFactory.CreateLogger<BitcoinRpc>();
      _network = network;
      _blockChainType = blockChainType;
    }

    private IBitcoinRpc CreateIBitcoinRpc(IRpcClient rpcClient)
    {

      if (_blockChainType == BlockChainType.BitcoinSV)
        return new BitcoinRpcBSV(rpcClient, _logger, _network);
      else
        return new BitcoinRpcBTC(rpcClient, _logger, _network);

    }

    public IBitcoinRpc Create(string host, int port, string username, string password)
    {
      if (_httpClientFactory == null)
      {
        throw new ArgumentNullException(nameof(_httpClientFactory));
      }

      UriBuilder builder = new UriBuilder
      {
        Host = host,
        Scheme = "http",
        Port = port
      };

      var httpClient = _httpClientFactory.CreateClient(builder.Uri.ToString());
      httpClient.BaseAddress = builder.Uri;
      var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
      httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

      var rpcClient = new RpcClient(httpClient);
      return CreateIBitcoinRpc(rpcClient);
    }

    public IBitcoinRpc Create()
    {
      return CreateIBitcoinRpc(_rpcClient);
    }
  }
}
