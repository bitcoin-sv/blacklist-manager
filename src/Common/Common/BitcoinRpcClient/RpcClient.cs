// Copyright (c) 2020 Bitcoin Association

using Common.BitcoinRpcClient.Responses;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Common.BitcoinRpcClient
{
  public class RpcClient : IRpcClient
  {
    private readonly Uri _address;
    private readonly NetworkCredential _credentials;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public int NumOfRetries { get; set; } = 3;

    private HttpClient HttpClient { get; init; }

    public RpcClient(HttpClient httpClient)
    {
      HttpClient = httpClient;
    }

    public RpcClient(IHttpClientFactory httpClientFactory, Uri address, NetworkCredential credentials)
    {
      _address = address;
      _credentials = credentials;
      // rpcHttpClientFactory will be null when called from Indexer functional tests
      if (httpClientFactory != null)
      {
        HttpClient = httpClientFactory.CreateClient(_address.Host);
        var byteArray = Encoding.ASCII.GetBytes($"{_credentials.UserName}:{_credentials.Password}");
        HttpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
      }
    }

    public Task<RpcFrozenFunds> AddToPolicyBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      var eah = funds.Funds.Where(f => f.EnforceAtHeight != null);
      if (eah.Any())
      {
        throw new InvalidOperationException("AddToPolicyBlacklist can not have EnforceAtHeight elements");
      }
      return RequestAsync<RpcFrozenFunds>(token, "addToPolicyBlacklist", funds);
    }

    public Task<RpcFrozenFunds> AddToConsensusBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      var eah = funds.Funds.Where(f => !f.EnforceAtHeight.Any());
      if (eah.Any())
      {
        throw new ArgumentException("addToConsensusBlacklist must have EnforceAtHeight elements for each fund");
      }
      return RequestAsync<RpcFrozenFunds>(token, "addToConsensusBlacklist", funds);
    }

    public Task<RpcFrozenFunds> RemoveFromPolicyBlacklistAsync(Requests.RpcFrozenFunds funds, CancellationToken? token = null)
    {
      var eah = funds.Funds.Where(f => f.EnforceAtHeight != null);
      if (eah.Any())
      {
        throw new InvalidOperationException("removeFromPolicyBlacklist can not have EnforceAtHeight elements");
      }
      return RequestAsync<RpcFrozenFunds>(token, "removeFromPolicyBlacklist", funds);
    }

    public Task<RPCClearAllBlackLists> ClearBlacklistsAsync(Requests.RpcClearBlacklist clear, CancellationToken? token = null)
    {
      return RequestAsync<RPCClearAllBlackLists>(token, "clearBlacklists", clear);
    }

    public Task<long> GetBlockCountAsync(CancellationToken? token = null)
    {
      return RequestWithRetryAsync<long>(token, "getblockcount", null);
    }

    public Task<RpcGetBlockWithTxIds> GetBlockWithTxIdsAsync(string blockHash, CancellationToken? token = null)
    {
      return RequestWithRetryAsync<RpcGetBlockWithTxIds>(token, "getblock", new object[] { blockHash, 1 });

    }

    public Task<RpcGetBlock> GetBlockAsync(string blockHash, int verbosity, CancellationToken? token = null)
    {
      if (verbosity == 0 || verbosity == 1)
      {
        throw new InvalidOperationException("GetBlockAsync method does not accept verbosity level 0, 1.");
      }
      return RequestWithRetryAsync<RpcGetBlock>(token, "getblock", new object[] { blockHash, verbosity });
    }

    public Task<RPCBitcoinStreamReader> GetBlockAsStreamAsync(string blockHash, CancellationToken? token = null)
    {
      return RequestWithRetryAsync<RPCBitcoinStreamReader>(token, "getblock", new object[] { blockHash, 0 });
    }

    public Task<string> GetBlockHashAsync(long height, CancellationToken? token = null)
    {
      return RequestWithRetryAsync<string>(token, "getblockhash", new object[] { height });
    }

    public Task<RpcGetBlockHeader> GetBlockHeaderAsync(string blockHash, CancellationToken? token = null)
    {
      return RequestWithRetryAsync<RpcGetBlockHeader>(token, "getblockheader", new object[] { blockHash, true });
    }

    public Task<string> GetBlockHeaderAsHexAsync(string blockHash, CancellationToken? token = null)
    {
      return RequestWithRetryAsync<string>(token, "getblockheader", new object[] { blockHash, false });
    }

    public Task<RpcGetRawTransaction> GetRawTransactionAsync(string txId, CancellationToken? token = null)
    {
      return RequestWithRetryAsync<RpcGetRawTransaction>(token, "getrawtransaction", new object[] { txId, true });
    }

    public Task<string> GetRawTransactionAsHexAsync(string txId, CancellationToken? token = null)
    {
      return RequestWithRetryAsync<string>(token, "getrawtransaction", new object[] { txId, false });
    }

    public Task<string> GetBestBlockHashAsync(CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "getbestblockhash", null);
    }

    public Task<RpcBlockChainInfo> GetBlockChainInfoAsync(CancellationToken? token = null)
    {
      return RequestWithRetryAsync<RpcBlockChainInfo>(token, "getblockchaininfo", null);
    }

    public async Task<string> DumpPrivKeyAsync(string address)
    {
      return await RequestWithRetryAsync<string>(null, "dumpprivkey", address);
    }

    public async Task<string> GetNewAddressAsync()
    {
      return await RequestWithRetryAsync<string>(null, "getnewaddress", null);
    }

    public async Task<RpcSendRawTransactions> SendRawTransactionsAsync(
      (string transaction, bool allowhighfees, bool dontCheckFees, bool listUnconfirmedAncestors)[] transactions, CancellationToken? token = null)
    {

      var t = transactions.Select(
        tx => new RpcSendTransactionsRequestOne
        {
          Hex = tx.transaction,
          AllowHighFees = tx.allowhighfees,
          DontCheckFee = tx.dontCheckFees,
          ListUnconfirmedAncestors = tx.listUnconfirmedAncestors
        }).Cast<object>().ToArray();

      object param1 = t; // cast to object so that it is not interpreted as multiple arguments
      var rpcResponse = await MakeRequestAsync<RpcSendRawTransactions>(token, new RpcRequest(1, "sendrawtransactions", param1));
      return rpcResponse.Result;
    }

    public Task StopAsync(CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "stop", null);
    }

    public Task<string> SendToAddressAsync(string address, double amount, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "sendtoaddress",
        address,
        amount.ToString(CultureInfo.InvariantCulture)
      );
    }

    public async Task<byte[]> GetRawTransactionAsBytesAsync(string txId, CancellationToken? token = null)
    {
      return HelperTools.HexStringToByteArray(await RequestAsync<string>(token, "getrawtransaction", txId, false));
    }

    public Task<string[]> GenerateAsync(int n, CancellationToken? token = null)
    {
      return RequestAsync<string[]>(token, "generate", n);
    }

    public Task<string> SubmitBlockAsync(byte[] block, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "submitblock", HelperTools.ByteToHexString(block));
    }

    public Task<RpcQueryBlacklist> QueryBlacklistAsync(CancellationToken? token = null)
    {
      return RequestAsync<RpcQueryBlacklist>(token, "queryBlacklist", null);
    }

    public Task<RpcGetChainTips[]> GetChainTipsAsync(CancellationToken? token = null)
    {
      return RequestAsync<RpcGetChainTips[]>(token, "getchaintips", null);
    }

    public Task<RpcConfiscationResult> AddToConfiscationTxIdWhitelistAsync(Requests.RpcConfiscation confiscationData, CancellationToken? token = null)
    {
      return RequestAsync<RpcConfiscationResult>(token, "addToConfiscationTxidWhitelist", confiscationData);
    }

    public Task<RpcQueryConfWhitelist> QueryConfiscationTxidWhitelistAsync(CancellationToken? token = null)
    {
      return RequestAsync<RpcQueryConfWhitelist>(token, "queryConfiscationTxidWhitelist", null);
    }

    public Task<string[]> GetRawMempoolAsync(CancellationToken? token = null)
    {
      return RequestAsync<string[]>(token, "getrawmempool", null);
    }

    public Task<string> AddNodeAsync(string host, int port, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "addnode", new object[] { $"{host}:{port}", "onetry" });
    }

    public Task<string> DisconnectNodeAsync(string host, int port, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "disconnectnode", new object[] { $"{host}:{port}" });
    }

    public Task<RpcGetPeerInfo[]> GetPeerInfoAsync(CancellationToken? token = null)
    {
      return RequestAsync<RpcGetPeerInfo[]>(token, "getpeerinfo", null);
    }

    public Task AddNodeAsync(string nodeAddress, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "addnode", new object[] { nodeAddress, "add" });
    }

    public Task<string> GetNewAddressAsync(CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "getnewaddress", new object[] { });
    }

    public Task<string> DumpPrivKeyAsync(string address, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "dumpprivkey", new object[] { address });
    }

    public Task<string> SignMessageAsync(string address, string message, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "signmessage", new object[] { address, message });
    }

    public Task<string[]> GenerateToAddressAsync(int n, string address, CancellationToken? token = null)
    {
      return RequestAsync<string[]>(token, "generatetoaddress", new object[] { n, address });
    }

    public Task<string> SendRawTransactionAsync(string txHex, CancellationToken? token = null)
    {
      return RequestAsync<string>(token, "sendrawtransaction", new object[] { txHex });
    }

    #region Private methods
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
          if (token.Value.IsCancellationRequested)
          {
            break;
          }
          await Task.Delay(WaitBetweenRetriesMs, token.Value);
        }
        else
        {
          await Task.Delay(WaitBetweenRetriesMs);
        }

      } while (retriesLeft > 0);

      // Should not happen since we exit when retriesLeft == 0
      throw new BadRequestException("Internal error RequestAsyncWithRetry reached the end");
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
      var reqMessage = new HttpRequestMessage(HttpMethod.Post, _address?.AbsoluteUri);

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
        throw new RpcException(response.Error.code, $"Error calling bitcoin RPC ({HttpClient.BaseAddress.AbsoluteUri}). {response.Error.message}");
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
        var address = _address?.AbsoluteUri ?? HttpClient.BaseAddress.AbsoluteUri;
        // Unable to parse error, so we return status code.
        int jsonSubpart = json.Length > 100 ? 100 : json.Length;
        throw new RpcException($"Error when executing bitcoin RPC method ({address}). JSON deserialization failed. Response code {responseMessage.StatusCode} was returned, exception message was '{ex.Message}'. JSON payload part {json.Substring(0, jsonSubpart)}", null);
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
          throw new RpcException($"Error when executing bitcoin RPC method ({_address.AbsoluteUri}). RPC response contains invalid JSON.", null);
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

      throw new RpcException($"Error when executing bitcoin RPC method ({_address.AbsoluteUri}). RPC response contains invalid JSON.", null);
    }
    #endregion
  }
}
