// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using Common.BitcoinRpc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Common.BitcoinRpc.Requests;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class BitcoindRPC : IBitcoind
  {
    private readonly RpcClient client;
    private readonly CancellationToken cancellationToken;

    public BitcoindRPC(IBitcoinRpcHttpClientFactory rpcHttpClientFactory, string host, int port, string username, string password, CancellationToken cancellationToken)
    {
      this.cancellationToken = cancellationToken;
      
      client = new RpcClient(rpcHttpClientFactory, CreateAddress(host, port), new System.Net.NetworkCredential(username, password));
    }

    public async Task<IEnumerable<Fund>> AddToPolicyBlacklistAsync(IEnumerable<Fund> funds)
    {
      var resultInternal = await client.AddToPolicyBlacklistAsync(ToRpc(funds), cancellationToken);
      return FromRpc(resultInternal);
    }

    public async Task<IEnumerable<Fund>> AddToConsensusBlacklistAsync(IEnumerable<Fund> funds)
    {
      var resultInternal = await client.AddToConsensusBlacklistAsync(ToRpc(funds, true), cancellationToken);
      return FromRpc(resultInternal);
    }

    public async Task<IEnumerable<Fund>> RemoveFromPolicyBlacklistAsync(IEnumerable<Fund> funds)
    {
      var resultInternal = await client.RemoveFromPolicyBlacklistAsync(ToRpc(funds), cancellationToken);
      return FromRpc(resultInternal);
    }

    public async Task<ClearAllBlacklistsResult> ClearBlacklistsAsync(bool removeAllEntries, int? expirationHeightDelta  = null)
    {
      var resultInternal = await client.ClearBlacklistsAsync(new RpcClearBlacklist(removeAllEntries, expirationHeightDelta), cancellationToken);
      return FromRpc(resultInternal);
    }

    public async Task<long> GetBlockCountAsync()
    {
      return await client.GetBlockCountAsync(cancellationToken);
    }

    public async Task<long> TestNodeConnectionAsync()
    {
      client.RequestTimeout = TimeSpan.FromSeconds(10);
      client.NumOfRetries = 3;
      return await client.GetBlockCountAsync(cancellationToken);
    }

    public async Task<string> GetBestBlockHashAsync()
    {
      return await client.GetBestBlockHashAsync(cancellationToken);
    }

    private RpcFrozenFunds ToRpc(IEnumerable<Fund> funds, bool mapConsensusFields = false)
    {
      var frozenFundsInternal = new RpcFrozenFunds
      {
        Funds = funds
          .Select(f => new RpcFrozenFunds.RpcFund()
          {
            TxOut = new RpcFrozenFunds.RpcFund.RpcTxOut() { TxId = f.TxOut.TxId, Vout = f.TxOut.Vout },
            EnforceAtHeight = mapConsensusFields ? f.EnforceAtHeight.GetConsolidatedList() : null,
            PolicyExpiresWithConsensus = mapConsensusFields ? f.PolicyExpiresWithConsensus  : (bool?)null
          })
          .ToList()
      };

      return frozenFundsInternal;
    }

    private IEnumerable<Fund> FromRpc(Common.BitcoinRpc.Responses.RpcFrozenFunds frozenFunds)
    {
      return frozenFunds.Funds
        .Select(f => new Fund(new TxOut(f.TxOut.TxId, f.TxOut.Vout), f.Reason));
    }

    private ClearAllBlacklistsResult FromRpc(Common.BitcoinRpc.Responses.RPCClearAllBlackLists clearAllBlackListsResponse)
    {
      return new ClearAllBlacklistsResult
      {
        NumRemovedConsensus = clearAllBlackListsResponse.NumRemovedConsensus,
        NumRemovedPolicy = clearAllBlackListsResponse.NumRemovedPolicy
      };
    }

    private Uri CreateAddress(string host, int port)
    {
      UriBuilder builder = new UriBuilder
      {
        Host = host,
        Scheme = "http",
        Port = port
      };
      return builder.Uri;
    }
  }
}
