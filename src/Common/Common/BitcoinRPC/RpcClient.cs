// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpc.Responses;
using Common.BitcoinRPC;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Common.BitcoinRpc
{
  public interface IGetBlockHeader
  {
    public Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null);
    public Task<string> GetBlockHashAsync(long height, CancellationToken? token = null);

    public Task<string> GetBestBlockHashAsync(CancellationToken? token = null);
    public async Task<RpcGetBlockHeader> GetBlockHeaderAsync(long height, CancellationToken? token = null)
    {
      return await GetBlockHeaderAsync(await GetBlockHashAsync(height));
    }
  }

  public class RpcClient : IGetBlockHeader
  {
    private readonly Uri Address;
    private readonly NetworkCredential Credentials;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);
    public int NumOfRetries { get; set; } = 50;

    public HttpClient HttpClient { get; private set; }

    public RpcClient(IBitcoinRpcHttpClientFactory rpcHttpClientFactory, Uri address, NetworkCredential credentials)
    {
      Address = address;
      Credentials = credentials;
      // rpcHttpClientFactory will be null when called from Indexer functional tests
      if (rpcHttpClientFactory != null)
      {
        HttpClient = rpcHttpClientFactory.CreateClient(Address.Host);
      }
    }

    public async Task<Responses.RpcFrozenFunds> AddToPolicyBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      var eah = funds.Funds.Where(f => f.EnforceAtHeight != null);
      if (eah.Any())
      {
        throw new Exception("AddToPolicyBlacklist can not have EnforceAtHeight elements");
      }
      return await RequestAsync<Responses.RpcFrozenFunds>(token, "addToPolicyBlacklist", funds);
    }

    public async Task<Responses.RpcFrozenFunds> AddToConsensusBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      var eah = funds.Funds.Where(f => !f.EnforceAtHeight.Any());
      if (eah.Any())
      {
        throw new Exception("addToConsensusBlacklist must have EnforceAtHeight elements for each fund");
      }
      return await RequestAsync<Responses.RpcFrozenFunds>(token, "addToConsensusBlacklist", funds);
    }

    public async Task<Responses.RpcFrozenFunds> RemoveFromPolicyBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      var eah = funds.Funds.Where(f => f.EnforceAtHeight != null);
      if (eah.Any())
      {
        throw new Exception("removeFromPolicyBlacklist can not have EnforceAtHeight elements");
      }
      return await RequestAsync<Responses.RpcFrozenFunds>(token, "removeFromPolicyBlacklist", funds);
    }

    public async Task<Responses.RPCClearAllBlackLists> ClearBlacklistsAsync(Requests.RpcClearBlacklist clear, CancellationToken? token = null)
    {
      return await RequestAsync<Responses.RPCClearAllBlackLists>(token, "clearBlacklists", clear);
    }

    public async Task<long> GetBlockCountAsync(CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<long>(token, "getblockcount", null);
    }

    public async Task<RpcGetBlockWithTxIds> GetBlockWithTxIdsAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<RpcGetBlockWithTxIds>(token, "getblock", new object[] { blockHash, 1 });

    }

    public async Task<RpcGetBlock> GetBlockAsync(string blockHash, int verbosity, CancellationToken? token = null)
    {
      if (verbosity == 0 || verbosity == 1)
      {
        throw new Exception("GetBlockAsync method does not accept verbosity level 0, 1.");
      }
      return await RequestWithRetryAsync<RpcGetBlock>(token, "getblock", new object[] { blockHash, verbosity });
    }

    public async Task<RPCBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<RPCBitcoinStreamReader>(token, "getblock", new object[] { blockHash, 0 });
    }

    public async Task<string> GetBlockHashAsync(long height, CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<string>(token, "getblockhash", new object[] { height });
    }

    public async Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<RpcGetBlockHeader>(token, "getblockheader", new object[] { blockHash, true });
    }

    public async Task<string> GetBlockHeaderAsHexAsync(string blockHash, CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<string>(token, "getblockheader", new object[] { blockHash, false });
    }

    public async Task<RpcGetRawTransaction> GetRawTransactionAsync(string txId, CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<RpcGetRawTransaction>(token, "getrawtransaction", new object[] { txId, true });
    }

    public async Task<string> GetRawTransactionAsHexAsync(string txId, CancellationToken? token = null)
    {
      return await RequestWithRetryAsync<string>(token, "getrawtransaction", new object[] { txId, false });
    }

    public async Task<string> GetBestBlockHashAsync(CancellationToken? token = null)
    {
      return await RequestAsync<string>(token, "getbestblockhash", null);
    }

    private async Task<T> RequestAsync<T>(CancellationToken? token, string method, params object[] parameters)
    {
      var rpcResponse = await MakeRequestAsync<T>(token, new RpcRequest(1, method, parameters));
      return rpcResponse.Result;
    }

    private const int WaitBetweenRetriesMs = 100;

    private async Task<T> RequestWithRetryAsync<T>(CancellationToken? token, string method, params object[] parameters)
    {
      int retriesLeft = NumOfRetries;
      do
      {
        try
        {
          retriesLeft--;
          RpcResponse<T> rpcResponse;
          if (method == "getblock" && (int)parameters[1] == 0)
          {
            object response = await MakeRequestReturnStreamAsync(token, new RpcRequest(1, method, parameters));
            return (T)response;
          }
          else
          {
            rpcResponse = await MakeRequestAsync<T>(token, new RpcRequest(1, method, parameters));
          }
          return rpcResponse.Result;
        }
        catch (TaskCanceledException ex)
        {
          throw new RpcException($"rpc call to {method} has been canceled", ex);
        }
        catch (Exception ex)
        {
          if (retriesLeft == 0)
          {
            throw new RpcException($"Failed after {NumOfRetries} retries. Last error: {ex.Message}", ex);
          }
        }
        if (token.HasValue)
        {
          await Task.Delay(WaitBetweenRetriesMs, token.Value);
        }
        else
        {
          await Task.Delay(WaitBetweenRetriesMs);
        }

      } while (retriesLeft > 0);

      // Should not happen since we exit when retriesLeft == 0
      throw new Exception("Internal error RequestAsyncWithRetry  reached the end");
    }

    private async Task<RpcResponse<T>> MakeRequestAsync<T>(CancellationToken? token, RpcRequest rpcRequest)
    {
      using var httpResponse = await MakeHttpRequestAsync(token, rpcRequest, false);
      return await GetRpcResponseAsync<T>(httpResponse);
    }

    private async Task<RPCBitcoinStreamReader> MakeRequestReturnStreamAsync(CancellationToken? token, RpcRequest rpcRequest)
    {
      var httpResponse = await MakeHttpRequestAsync(token, rpcRequest, true);
      return await GetRpcResponseAsStreamAsync(httpResponse, token);
    }

    private HttpRequestMessage CreateRequestMessage(string json)
    {
      var reqMessage = new HttpRequestMessage(HttpMethod.Post, Address.AbsoluteUri);

      var byteArray = Encoding.ASCII.GetBytes($"{Credentials.UserName}:{Credentials.Password}");
      reqMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
      reqMessage.Content = new StringContent(json, new UTF8Encoding(false), "application/json-rpc");
      return reqMessage;
    }

    private async Task<HttpResponseMessage> MakeHttpRequestAsync(CancellationToken? token, RpcRequest rpcRequest, bool readOnlyHeader)
    {
      var reqMessage = CreateRequestMessage(rpcRequest.GetJSON());
      using var cts = new CancellationTokenSource(RequestTimeout);
      using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token ?? CancellationToken.None);

      var completionOption = readOnlyHeader ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead;
      var httpResponse = await HttpClient.SendAsync(reqMessage, completionOption, cts2.Token);
      if (!httpResponse.IsSuccessStatusCode)
      {
        var response = await GetRpcResponseAsync<string>(httpResponse);
        throw new RpcException(response.Error.code, $"Error calling bitcoin RPC ({Address.AbsoluteUri}). {response.Error.message}");
      }
      return httpResponse;
    }

    private async Task<RpcResponse<T>> GetRpcResponseAsync<T>(HttpResponseMessage responseMessage)
    {
      string json = await responseMessage.Content.ReadAsStringAsync();

      if (string.IsNullOrEmpty(json))
      {
        return new RpcResponse<T>
        {
          Error = new RpcError
          {
            code = (int)responseMessage.StatusCode,
            message = responseMessage.ReasonPhrase
          }
        };
      }
      try
      {
        return JsonSerializer.Deserialize<RpcResponse<T>>(json);
      }
      catch (JsonException ex)
      {
        // Unable to parse error, so we return status code.
        int jsonSubpart = json.Length > 100 ? 100 : json.Length;
        throw new RpcException($"Error when executing bitcoin RPC method ({Address.AbsoluteUri}). JSON deserialization failed. Response code {responseMessage.StatusCode} was returned, exception message was '{ex.Message}'. JSON payload part {json.Substring(0, jsonSubpart)}", null);
      }
    }

    static readonly char[] validChars = new char[] { ':', '"', ' ', '\n', '\r', '\t' };

    private async Task ReadUntilAsync(char characterToFind, StreamReader streamReader, CancellationToken? token)
    {
      char[] character = new char[1];
      do
      {
        token?.ThrowIfCancellationRequested();

        await streamReader.ReadBlockAsync(character, 0, 1);
        if (!validChars.Contains(character[0]))
        {
          throw new RpcException($"Error when executing bitcoin RPC method ({Address.AbsoluteUri}). RPC response contains invalid JSON.", null);
        }
      } while (!streamReader.EndOfStream && character[0] != characterToFind);
    }

    /// <summary>
    /// This method bypasses JSON wrapper so it can stream the value part of the "result" field in JSON response which can contain huge amount of HEX encoded data, that JSON 
    /// parsers are unable to deserialize, and pass it out as a Stream so that NBitcoin can directly use the stream when creating instances
    /// </summary>
    private async Task<RPCBitcoinStreamReader> GetRpcResponseAsStreamAsync(HttpResponseMessage responseMessage, CancellationToken? token)
    {
      var responseStream = await responseMessage.Content.ReadAsStreamAsync();
      var strReader = new StreamReader(responseStream);
      // Bucket to hold data that is present between quotation marks, used to find field names
      StringBuilder bucket = new StringBuilder();
      do
      {
        token?.ThrowIfCancellationRequested();

        char[] charFromStream = new char[1];
        await strReader.ReadBlockAsync(charFromStream, 0, 1);

        // Once we find " we clear the content of the bucket to start storing a new value
        if (charFromStream[0] == '"')
        {
          // We found field name "result" now we just check if it looks like this "result":" before we return the stream with the position
          // on the first char after the "
          if (bucket.ToString().ToLower() == "result")
          {
            await ReadUntilAsync(':', strReader, token);
            await ReadUntilAsync('\"', strReader, token);

            return new RPCBitcoinStreamReader(strReader, token);
          }
          bucket.Clear();
        }
        else
        {
          bucket.Append(charFromStream);
        }

      } while (!strReader.EndOfStream);

      throw new RpcException($"Error when executing bitcoin RPC method ({Address.AbsoluteUri}). RPC response contains invalid JSON.", null);
    }
  }
}
