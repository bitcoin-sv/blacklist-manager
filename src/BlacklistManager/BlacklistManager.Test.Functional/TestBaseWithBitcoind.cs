// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Common.BitcoinRpcClient;
using BlacklistManager.Domain.Models;
using NBitcoin;
using System.Net.Http;

namespace BlacklistManager.Test.Functional
{
  /// <summary>
  /// Atribute to skip node start and registration
  /// </summary>
  public class SkipNodeStartAttribute : Attribute { }

  /// <summary>
  /// base class for functional tests that require bitcoind instance
  /// During test setup a new instance of bitcoind is setup, some blocks are generated and coins are collected,
  /// so that they can be used during test
  /// </summary>
  public class TestBaseWithBitcoind : TestBase
  {
    private string _bitcoindFullPath;
    private string _hostIp = "localhost";
    public TestContext TestContext { get; set; }

    protected List<BitcoindProcess> _bitcoindProcesses = new List<BitcoindProcess>();

    protected IRpcClient _rpcClient0;
    protected BitcoindProcess _node0;

    protected Queue<Coin> _availableCoins = new Queue<Coin>();

    // Private key and corresponding address used for testing
    protected const string FEE_ADDRESS = "miaWUnUREiJbJayUWDKZ2aWNCUwQHv3dit";

    public async Task TestInitializeAsync(bool setupChain = false)
    {
      await InitializeAsync(mockedServices: false);

      var bitcoindConfigKey = "BitcoindFullPath";
      _bitcoindFullPath = Configuration[bitcoindConfigKey];
      if (string.IsNullOrEmpty(_bitcoindFullPath))
      {
        throw new Exception($"Required parameter {bitcoindConfigKey} is missing from configuration");
      }

      var alternativeIp = Configuration["HostIp"];
      if (!string.IsNullOrEmpty(alternativeIp))
      {
        _hostIp = alternativeIp;
      }

      bool skipNodeStart = GetType().GetMethod(TestContext.TestName).GetCustomAttributes(true).Any(a => a.GetType() == typeof(SkipNodeStartAttribute));

      if (!skipNodeStart)
      {
        _node0 = await CreateAndStartNodeAsync(0);
        _rpcClient0 = _node0.RpcClient;
        if (setupChain)
        {
          await SetupChainAsync(_rpcClient0);
        }
      }
    }

    public async Task<BitcoindProcess> CreateAndStartNodeAsync(int index)
    {
      var bitcoind = StartBitcoind(index);

      var node = new Node(index, bitcoind.Host, bitcoind.RpcPort, bitcoind.RpcUser, bitcoind.RpcPassword, $"This is a mock node #{index}", (int)NodeStatus.Connected, null, null);

      await Nodes.CreateNodeAsync(node);
      return bitcoind;
    }

    void StopAllBitcoindProcesses()
    {
      if (_bitcoindProcesses.Any())
      {
        var totalCount = _bitcoindProcesses.Count;
        int sucesfullyStopped = 0;
        LoggerTest.LogInformation($"Shutting down {totalCount} bitcoind processes");

        foreach (var bitcoind in _bitcoindProcesses.ToArray())
        {
          var bitcoindDescription = bitcoind.Host + ":" + bitcoind.RpcPort;
          try
          {
            StopBitcoind(bitcoind);
            sucesfullyStopped++;
          }
          catch (Exception e)
          {
            LoggerTest.LogInformation($"Error while stopping bitcoind {bitcoindDescription}. This can occur if node has been explicitly stopped or if it crashed. Will proceed anyway. {e}");
          }

          LoggerTest.LogInformation($"Successfully stopped {sucesfullyStopped} out of {totalCount} bitcoind processes");

        }
        _bitcoindProcesses.Clear();
      }
    }

    public virtual void TestCleanup()
    {
      // StopAllBitcoindProcesses is called after the server is shutdown, so that we are sure that no
      // no background services (which could use bitcoind)  are still running
      StopAllBitcoindProcesses();
    }

    static readonly string commonTestPrefix = typeof(TestBaseWithBitcoind).Namespace + ".";
    static readonly int bitcoindInternalPathLength = "regtest/blocks/index/MANIFEST-00000".Length + 10;
    public BitcoindProcess StartBitcoind(int nodeIndex, BitcoindProcess[] nodesToConnect = null)
    {

      string testPerfix = TestContext.FullyQualifiedTestClassName;
      if (testPerfix.StartsWith(commonTestPrefix))
      {
        testPerfix = testPerfix.Substring(commonTestPrefix.Length);
      }

      var dataDirRoot = Path.Combine(TestContext.TestRunDirectory, "node" + nodeIndex, testPerfix, TestContext.TestName);
      if (Environment.OSVersion.Platform == PlatformID.Win32NT && dataDirRoot.Length + bitcoindInternalPathLength >= 260)
      {
        // LevelDB refuses to open file with path length  longer than 260 
        throw new Exception($"Length of data directory path is too long. This might cause problems when running bitcoind on Windows. Please run tests from directory with a short path. Data directory path: {dataDirRoot}");
      }

      var bitcoind = new BitcoindProcess(
        _bitcoindFullPath,
        dataDirRoot,
        nodeIndex, _hostIp, LoggerFactory,
        Server.Services.GetRequiredService<IHttpClientFactory>(), nodesToConnect);
      _bitcoindProcesses.Add(bitcoind);
      return bitcoind;
    }

    public void StopBitcoind(BitcoindProcess bitcoind)
    {
      if (!_bitcoindProcesses.Contains(bitcoind))
      {
        throw new Exception($"Can not stop a bitcoind that was not started by {nameof(StartBitcoind)} ");
      }

      bitcoind.Dispose();

    }

    static async Task<Coin> GetCoinAsync(IRpcClient rpcClient)
    {
      var txId = await rpcClient.SendToAddressAsync(TEST_ADDRESS, 0.1);
      var tx = NBitcoin.Transaction.Load(await rpcClient.GetRawTransactionAsBytesAsync(txId), Network.RegTest);
      int? foundIndex = null;
      for (int i = 0; i < tx.Outputs.Count; i++)
      {
        if (tx.Outputs[i].ScriptPubKey.GetDestinationAddress(Network.RegTest).ToString() == TEST_ADDRESS)
        {
          foundIndex = i;
          break;
        }
      }

      if (foundIndex == null)
      {
        throw new Exception("Unable to found a transaction output with required destination address");
      }

      return new Coin(tx, (uint)foundIndex.Value);
    }

    protected static async Task<Coin[]> GetCoinsAsync(IRpcClient rpcClient, int number)
    {
      var coins = new List<Coin>();
      for (int i = 0; i < number; i++)
      {
        coins.Add(await GetCoinAsync(rpcClient));
      }

      // Mine coins into  a block
      await rpcClient.GenerateAsync(1);

      return coins.ToArray();
    }


    /// <summary>
    /// Sets ups a new chain, get some coins and store them in availableCoins, so that they can be consumed by test
    /// </summary>
    /// <param name="rpcClient"></param>
    public async Task SetupChainAsync(IRpcClient rpcClient)
    {
      LoggerTest.LogInformation("Setting up test chain");
      await rpcClient.GenerateAsync(150);
      foreach (var coin in await GetCoinsAsync(rpcClient, 10))
      {
        _availableCoins.Enqueue(coin);
      }
    }

    public (string txHex, string txId) CreateNewTransaction(Coin coin, Money amount)
    {
      return CreateNewTransaction(coin, amount, TEST_ADDRESS);
    }

    public (string txHex, string txId) CreateNewTransaction(Coin coin, Money amount, string destinationAddress)
    {
      var address = BitcoinAddress.Create(destinationAddress, Network.RegTest);
      var tx = NBitcoin.Altcoins.BCash.Instance.Regtest.CreateTransaction();

      tx.Inputs.Add(new TxIn(coin.Outpoint));
      tx.Outputs.Add(coin.Amount - amount, address);

      var key = Key.Parse(TEST_PRIVATE_KEY_WIF, Network.RegTest);

      tx.Sign(key.GetBitcoinSecret(Network.RegTest), coin);

      return (tx.ToHex(), tx.GetHash().ToString());
    }
  }
}
