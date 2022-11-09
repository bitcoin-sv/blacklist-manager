﻿// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using Microsoft.Extensions.Logging;
using Common.BitcoinRpcClient;
using Common;
using System.Net.Http;

namespace BlacklistManager.Test.Functional
{
  public class BitcoindProcess : IDisposable
  {
    const string defaultParams =
      "-regtest -logtimemicros -excessiveblocksize=100000000000 -maxstackmemoryusageconsensus=1000000000 -genesisactivationheight=1 -debug -debugexclude=libevent -debugexclude=tor";

    Process process;
    
    /// <summary>
    /// A IRpcClient that can be used to access this node 
    /// </summary>
    public IRpcClient RpcClient { get; private set; }

    ILogger<BitcoindProcess> logger;

    public int P2Port { get; private set; }
    public int RpcPort { get; private set; }
    public string RpcUser { get; private set; }
    public string RpcPassword { get; private set; }
    public string Host { get; private set; }


    public BitcoindProcess(string bitcoindFullPath, string dataDirRoot, int nodeIndex, string hostIp, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, BitcoindProcess[] nodesToConnect = null) :
      this(hostIp, bitcoindFullPath, Path.Combine(dataDirRoot, "node" + nodeIndex),
        18444 + nodeIndex,
        18332 + nodeIndex,
        loggerFactory,
        httpClientFactory,
        nodesToConnect: nodesToConnect)
    {

    }

    /// <summary>
    /// Deletes node data directory (if exists) and start new instance of bitcoind
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "Constructor doesn't support async methods")]
    public BitcoindProcess(string hostIp, string bitcoindFullPath, string dataDir, int p2pPort, int rpcPort, ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory, bool emptyDataDir = true, BitcoindProcess[] nodesToConnect = null)
    {
      this.Host = hostIp;
      this.P2Port = p2pPort;
      this.RpcPort = rpcPort;
      this.RpcUser = "user";
      this.RpcPassword = "password";
      this.logger = loggerFactory.CreateLogger<BitcoindProcess>();

      if (!ArePortsAvailable(p2pPort, rpcPort))
      {
        throw new Exception(
          "Can not start a new instance of bitcoind. Specified ports are already in use. There might be an old version of bitcoind still running. Terminate it manually and try again-");
      }

      if (emptyDataDir)
      {
        if (Directory.Exists(dataDir))
        {
          var regtest = Path.Combine(dataDir, "regtest");
          if (Directory.Exists(regtest))
          {
            logger.LogInformation($"Old regtest directory exists. Removing it: {regtest}");
            Directory.Delete(regtest, true);
          }
        }
        else
        {
          Directory.CreateDirectory(dataDir);
        }
      }
      else
      {
        if (!Directory.Exists(dataDir))
        {
          throw new Exception("Data directory does not exists. Can not start new instance of bitcoind");
        }
      }


      // use StartupInfo.ArgumentList instead of StartupInfo.Arguments to avoid problems with spaces in data dir
      var argumentList = new List<string>(defaultParams.Split(" ").ToList());
      argumentList.Add($"-port={p2pPort}");
      argumentList.Add($"-rpcport={rpcPort}");
      argumentList.Add($"-datadir={dataDir}");
      argumentList.Add($"-rpcuser={RpcUser}");
      argumentList.Add($"-rpcpassword={RpcPassword}");

      if (nodesToConnect != null)
      {
        foreach(var node in nodesToConnect)
        {
          argumentList.Add($"-addnode={node.Host}:{node.P2Port}");
        }
      }

      logger.LogInformation($"Starting {bitcoindFullPath} {string.Join(" ",argumentList.ToArray())}");

      var localProcess = new Process();
      var startInfo = new ProcessStartInfo(bitcoindFullPath);
      foreach (var arg in argumentList)
      {
        startInfo.ArgumentList.Add(arg);
      }

      localProcess.StartInfo = startInfo;
      try
      {
        if (!localProcess.Start())
        {
          throw new Exception($"Can not invoke {bitcoindFullPath}");
        }

      }
      catch (Exception ex)
      {
        throw new Exception($"Can not invoke {bitcoindFullPath}. {ex.Message}", ex);
      }

      this.process = localProcess;
      string bestBlockhash = null;
      UriBuilder builder = new UriBuilder
      {
        Host = Host,
        Scheme = "http",
        Port = RpcPort
      };

      var rpcClient = new RpcClient(httpClientFactory, builder.Uri, new System.Net.NetworkCredential(RpcUser, RpcPassword));
      try
      {
        RetryUtils.ExecuteWithRetriesAsync(10, "Can not connect to test node", async () => { bestBlockhash = await rpcClient.GetBestBlockHashAsync(); }, 3000).GetAwaiter().GetResult();
      }
      catch (Exception e)
      {
        logger.LogError(e.Message);
        throw;
      }

      this.RpcClient = rpcClient;
      if (nodesToConnect is null && emptyDataDir)
      {
        var height = rpcClient.GetBlockHeaderAsync(bestBlockhash).Result.Height;
        if (height != 0)
        {
          throw new Exception(
            "The node that was just started does not have an empty chain. Can not proceed. Terminate the instance manually. ");
        }
      }

      logger.LogInformation($"Started bitcoind process pid={localProcess.Id } rpcPort={rpcPort}, p2pPort={P2Port}, dataDir={dataDir}");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "IDisposable doesn't implement DisposeAsync method")]
    public void Dispose()
    {
      if (process != null)
      {
        if (RpcClient != null)
        {
          // Note that bitcoind RPC "stop" call starts the shutdown it does not shutdown the process immediately
          try
          {
            RpcClient.StopAsync().GetAwaiter().GetResult();           
          } catch { }
        }
      }

      if (process != null)
      {
        if (RpcClient != null)
        {
          // if we requested stop, give it some time to shut down
          for (int i = 0; i < 10 && !process.HasExited; i++)
          {
            Thread.Sleep(100);
          }
        }

        if (!process.HasExited)
        {
          logger.LogError($"BitcoindProcess with pid={process.Id} did not stop. Will kill it.");
          process.Kill();
          if (process.WaitForExit(2000))
            logger.LogError($"BitcoindProcess with pid={process.Id} successfully killed.");
        }

        RpcClient = null;
        process.Dispose();
        process = null;
      }
    }


    bool ArePortsAvailable(params int[] ports)
    {
      var portLists = ports.ToList();
      IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
      var listeners = ipGlobalProperties.GetActiveTcpListeners();

      foreach (var listener in listeners)
      {
        if (portLists.Contains(listener.Port))
        {
          return false;
        }
      }

      return true;
    }
  }
}
