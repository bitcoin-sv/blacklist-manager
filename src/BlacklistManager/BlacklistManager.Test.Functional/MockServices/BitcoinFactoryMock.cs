// Copyright (c) 2020 Bitcoin Association

using Common.Bitcoin;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class BitcoinFactoryMock : IBitcoinFactory
  {
    #region IBitcoindFactory

    private readonly ILogger logger;

    public BitcoinFactoryMock(ILoggerFactory logger)
    {
      this.logger = logger.CreateLogger(TestBase.LOG_CATEGORY);

      doNotTraceMethods.Add(BitcoindCallList.Methods.GetBlockCount);
      doNotTraceMethods.Add(BitcoindCallList.Methods.ClearAllBlacklists);
    }

    public IBitcoinRpc Create()
    {
      throw new NotImplementedException();
    }

    public IBitcoinRpc Create(string host, int port, string username, string password)
    {
      var bitcoindMock = new BitcoinRpcMock(
          host,
          this.logger,
          callList,
          propagationSyncWaitForPropagation,
          cancellOnPropagationIndex,
          doNotTraceMethods,
          disconnectedNodes);

      bitcoindMock.OnError += BitcoindMock_OnCallError;

      return bitcoindMock;
    }

    #endregion

    /// <summary>
    /// Calls to bitcoind for all nodes
    /// </summary>
    public BitcoindCallList callList = new BitcoindCallList();

    /// <summary>
    /// All bitcoind nodes
    /// </summary>
    //private Dictionary<string, BitcoindMock> bitcoindList = new Dictionary<string, BitcoindMock>();

    /// <summary>
    /// List of bitcoind calls we do not wont to trace
    /// </summary>
    private readonly SortedSet<string> doNotTraceMethods = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// Nodes that are not working
    /// </summary>
    private readonly SortedSet<string> disconnectedNodes = new SortedSet<string>(StringComparer.InvariantCultureIgnoreCase);

    /// <summary>
    /// True if any node reported exception
    /// </summary>
    private bool nodeErrorOccurred = false;

    private string errorObj;

    /// <summary>
    /// 
    /// </summary>
    public AutoResetEvent propagationSyncWaitForPropagation = new AutoResetEvent(false);

    /// <summary>
    /// Index of propagation that should be canceled (is per all nodes level)
    /// </summary>
    private int? cancellOnPropagationIndex = null;    

    private void BitcoindMock_OnCallError(string obj)
    {
      nodeErrorOccurred = true;
      errorObj = obj;
    }

    /// <summary>
    /// Clear all calls to bitcoind
    /// </summary>
    public void ClearCalls()
    {
      callList.ClearCalls();
    }

    /// <summary>
    /// Asserts that call lists equals to expected value and clears it so that new calls can be easily
    /// testes in next invocation of AssertEqualAndClear
    /// </summary>
    /// <param name="expected"></param>
    public void AssertEqualAndClear(params string[] expected)
    {
      callList.AssertEqualTo(expected);
      ClearCalls();
    }   

    public void Reset(string[] doNotTraceMethods = null)
    {
      ClearCalls();
      ReconnecNodes();
      nodeErrorOccurred = false;
      this.doNotTraceMethods.Clear();
      if (doNotTraceMethods != null)
      {
        foreach (var method in doNotTraceMethods)
        {
          this.doNotTraceMethods.Add(method);
        }
      }
      propagationSyncWaitForPropagation.Reset();
      cancellOnPropagationIndex = null;
    }

    public void DisconnectNode(string nodeId)
    {
      disconnectedNodes.Add(nodeId);
    }

    public void ReconnectNode(string nodeId)
    {
      disconnectedNodes.Remove(nodeId);
    }

    public void ReconnecNodes()
    {
      disconnectedNodes.Clear();
    }

    public void WaitUntilNodeException(string errorObj)
    {
      Utils.WaitUntil(() => nodeErrorOccurred == true && this.errorObj == errorObj);
      nodeErrorOccurred = false;
    }

    public void WaitUntilNodeException()
    {
      Utils.WaitUntil(() => nodeErrorOccurred == true);
      nodeErrorOccurred = false;
    }

    public void SetPropagationSync(int propagationIndex)
    {
      propagationSyncWaitForPropagation.Reset();
      cancellOnPropagationIndex = propagationIndex;
    }

  }
}
