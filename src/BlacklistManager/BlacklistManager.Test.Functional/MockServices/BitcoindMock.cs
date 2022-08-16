// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class BitcoindMock : IBitcoind
  {
    private readonly string nodeId;
    private readonly ILogger logger;
    private readonly SortedSet<string> doNotTraceMethods;
    private readonly AutoResetEvent propagationSyncWaitForPropagation = new AutoResetEvent(false);
    private readonly int? cancellOnPropagationIndex = null;
    private readonly BitcoindCallList callList = null;
    private readonly SortedSet<string> disconnectedNodes = null;
    private readonly CancellationToken cancellationToken;


    public event Action<string> OnError;

    public BitcoindMock(
      string nodeId, 
      CancellationToken cancellationToken, 
      ILogger logger, 
      BitcoindCallList callList,
      AutoResetEvent propagationSyncWaitForPropagation,
      int? cancellOnPropagationIndex,
      SortedSet<string> doNotTraceMethods,
      SortedSet<string> disconnectedNodes)
    {
      this.nodeId = nodeId;
      this.cancellationToken = cancellationToken;
      this.logger = logger;
      this.callList = callList;
      this.propagationSyncWaitForPropagation = propagationSyncWaitForPropagation;
      this.cancellOnPropagationIndex = cancellOnPropagationIndex;
      this.doNotTraceMethods = doNotTraceMethods;
      this.disconnectedNodes = disconnectedNodes;
    }

    public void ThrowIfDisconnected()
    {
      if (disconnectedNodes.Contains(nodeId))
      {
        OnError?.Invoke(nodeId);

        // use HttpRequestException, since this is what is handled by FundPropagator
        throw new HttpRequestException($"Node '{nodeId}' can not be reached (simulating error)"); 
      }
    }

    private void IfRequestedWaitOnPropagationCancelation()
    {
      if (cancellOnPropagationIndex.HasValue && callList?.AllPropagationCalls.Count == cancellOnPropagationIndex.Value)
      {
        propagationSyncWaitForPropagation.Set();
        logger.LogDebug("Waiting until cancellationToken.IsCancellationRequested");
        Utils.WaitUntil(() => cancellationToken.IsCancellationRequested);
      }
    }

    public async Task<IEnumerable<Fund>> AddToConsensusBlacklistAsync(IEnumerable<Fund> funds)
    {
      ThrowIfDisconnected();
      if (!doNotTraceMethods.Contains(BitcoindCallList.Methods.AddToConsensus))
      {
        callList?.AddToConsensusCall(nodeId, funds);
      }
      IfRequestedWaitOnPropagationCancelation();
      return await Task.FromResult(Enumerable.Empty<Fund>());
    }

    public async Task<IEnumerable<Fund>> AddToPolicyBlacklistAsync(IEnumerable<Fund> funds)
    {
      ThrowIfDisconnected();
      if (!doNotTraceMethods.Contains(BitcoindCallList.Methods.AddToPolicy))
      {
        callList?.AddToPolicyCall(nodeId, funds);
      }
      IfRequestedWaitOnPropagationCancelation();
      return await Task.FromResult(Enumerable.Empty<Fund>());
    }
    
    public async Task<IEnumerable<Fund>> RemoveFromPolicyBlacklistAsync(IEnumerable<Fund> funds)
    {
      ThrowIfDisconnected();
      callList?.RemoveFromPolicyCall(nodeId, funds);
      IfRequestedWaitOnPropagationCancelation();
      return await Task.FromResult(Enumerable.Empty<Fund>());
    }

    public async Task<ClearAllBlacklistsResult> ClearBlacklistsAsync(bool removeAllEntries, int? expirationHeightDelta = null)
    {
      ThrowIfDisconnected();
      if (!doNotTraceMethods.Contains(BitcoindCallList.Methods.ClearAllBlacklists))
      {
        callList?.ClearAllBlacklistsCall(nodeId);
      }
      return await Task.FromResult(new ClearAllBlacklistsResult());
    }

    public Task<long> GetBlockCountAsync()
    {
      ThrowIfDisconnected();
      if (!doNotTraceMethods.Contains(BitcoindCallList.Methods.GetBlockCount))
      {
        callList?.GetBlockCountCall(nodeId);
      }

      return Task.FromResult(1000L);
    }

    public Task<long> TestNodeConnectionAsync()
    {
      ThrowIfDisconnected();
      if (!doNotTraceMethods.Contains(BitcoindCallList.Methods.GetBlockCount))
      {
        callList?.GetBlockCountCall(nodeId);
      }

      return Task.FromResult(1000L);
    }

    public Task<string> GetBestBlockHashAsync()
    {
      return Task.FromResult("000000000000000003ab604cf99c47ffe2a81c530788773281873b6890274fe1");
    }
  }
}
