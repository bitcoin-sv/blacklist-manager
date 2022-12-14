// Copyright (c) 2020 Bitcoin Association


using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static Common.BitcoinRpcClient.Requests.RpcFrozenFunds;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class BitcoindCallList
  {
    public static class Methods
    {
      public static readonly string AddToPolicy = "addToPolicy";
      public static readonly string AddToConsensus = "addToConsensus";
      public static readonly string RemoveFromPolicy = "removeFromPolicy";      
      public static readonly string ClearAllBlacklists = "clearAllBlacklists";
      public static readonly string GetBlockCount = "getBlockCount";
    }
    /// <summary>
    /// All calls to bitcoind ordered by time
    /// </summary>
    public List<Call> AllCalls = new List<Call>();
    public IReadOnlyList<Call> AddToPolicyCalls => AllCalls.Where(call => call.MethodName == Methods.AddToPolicy).ToList();
    public IReadOnlyList<Call> AddToConsensusCalls => AllCalls.Where(call => call.MethodName == Methods.AddToConsensus).ToList();
    public IReadOnlyList<Call> RemoveFromPolicyCalls => AllCalls.Where(call => call.MethodName == Methods.RemoveFromPolicy).ToList();
    /// <summary>
    /// All calls to bitcoind except calls for: clearAllBlacklists, GetBlockCount
    /// </summary>
    public IReadOnlyList<Call> AllPropagationCalls => AllCalls.Where(c => c.MethodName != Methods.ClearAllBlacklists).ToList();

    public void ClearCalls()
    {
      AllCalls.Clear();
    }
    
    public void AddToPolicyCall(string nodeId, Common.BitcoinRpcClient.Requests.RpcFrozenFunds funds)
    {
      AllCalls.Add(new Call(Methods.AddToPolicy, nodeId, funds));
    }

    public void AddToConsensusCall(string nodeId, Common.BitcoinRpcClient.Requests.RpcFrozenFunds funds)
    {
      AllCalls.Add(new Call(Methods.AddToConsensus, nodeId, funds));
    }

    public void RemoveFromPolicyCall(string nodeId, Common.BitcoinRpcClient.Requests.RpcFrozenFunds funds)
    {
      AllCalls.Add(new Call(Methods.RemoveFromPolicy, nodeId, funds));
    }    

    public void ClearAllBlacklistsCall(string nodeId)
    {
      AllCalls.Add(new Call(Methods.ClearAllBlacklists, nodeId, null));
    }

    public void GetBlockCountCall(string nodeId)
    {
      AllCalls.Add(new Call(Methods.GetBlockCount, nodeId, null));
    }


    public override string ToString()
    {
      var sb = new StringBuilder();
      foreach (var call in AllCalls)
      {
        sb.AppendLine(call.ToString());
      }

      return sb.ToString().Trim(); // trim ending newline
    }

    public void AssertEqualTo(params string[] expected)
    {
      Assert.AreEqual(string.Join(Environment.NewLine, expected), ToString());
    }

    public class Call
    {
      public Call(string methodName, string nodeId, Common.BitcoinRpcClient.Requests.RpcFrozenFunds funds)
      {
        MethodName = methodName;
        NodeId = nodeId;
        Funds = funds;
        if (funds != null)
        {
          TxIds = string.Join("/",
            Funds.Funds
              .OrderBy(f => f.TxOut.TxId)
              .Select(FundToStr));
        }
      }

      private string FundToStr(RpcFund fund)
      {
        string eah = string.Empty;
        string p = string.Empty;
        if (fund.EnforceAtHeight != null && fund.EnforceAtHeight.Any())
        {
          eah = string.Join(";",
            fund.EnforceAtHeight
              .OrderBy(e => e.Start)
              .Select(e => $"{e.Start},{e.Stop}"));
        }

        if (MethodName == Methods.AddToConsensus)
        {
          p = $"{fund.PolicyExpiresWithConsensus}";
        }
        return $"{fund.TxOut.TxId},{fund.TxOut.Vout}|{eah}|{p}";
      }

      public Common.BitcoinRpcClient.Requests.RpcFrozenFunds Funds { get; set; }
      public string TxIds;
      public string NodeId;
      public string MethodName;

      public override string ToString()
      {
        var result = NodeId + ":" + MethodName;
        if (TxIds != null)
        {
          result += "/" + TxIds;
        }
        return result;
      }
    }
  }
}
