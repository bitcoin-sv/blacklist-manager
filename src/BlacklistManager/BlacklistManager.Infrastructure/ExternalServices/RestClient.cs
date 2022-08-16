// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using Common;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class RestClient : IRestClient
  {
    private string baseUrl;
    private readonly string apiKey;

    public RestClient(string baseUrl, string apiKey, IBlacklistHttpClientFactory blacklistHttpClientFactory)
    {
      this.baseUrl = baseUrl;
      this.apiKey = apiKey;
      HttpClient = blacklistHttpClientFactory.CreateClient();
    }

    private TimeSpan defaultRequestTimeout = TimeSpan.FromSeconds(100);

    public HttpClient HttpClient { get; private set; }

    public string BaseURL { get => baseUrl; set => baseUrl = value; }

    public async Task<string> RequestAsync(HttpMethod httpMethod, string apiMethod, string jsonRequest, bool throwExceptionOn404 = true, TimeSpan? requestTimeout = null)
    {
      var reqMessage = CreateRequestMessage(httpMethod, apiMethod, jsonRequest);
      using (var cts = new CancellationTokenSource(requestTimeout ?? defaultRequestTimeout))
      {
        var httpResponse = await HttpClient.SendAsync(reqMessage, cts.Token);
        var response = await httpResponse.Content.ReadAsStringAsync();

        if (!throwExceptionOn404 && httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
          return null;
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
          throw new HttpResponseException(httpResponse.StatusCode, $"Error calling {baseUrl + apiMethod}. Reason: {httpResponse.ReasonPhrase}");
        }
        return response;
      }
    }

    private HttpRequestMessage CreateRequestMessage(HttpMethod httpMethod, string apiMethod, string jsonRequest)
    {
      var reqMessage = new HttpRequestMessage(httpMethod, new Uri(baseUrl + apiMethod));
      reqMessage.Headers.Add("X-Api-Key", apiKey);
      if (jsonRequest != null)
      {
        reqMessage.Content = new StringContent(jsonRequest, new UTF8Encoding(false), "application/json");
      }
      return reqMessage;
    }
  }
}
