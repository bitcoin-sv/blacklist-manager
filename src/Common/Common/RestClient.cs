// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Common
{
  public class RestClient : IRestClient
  {
    private readonly string apiKeyName;
    private readonly string apiKeyValue;
    private readonly TimeSpan defaultRequestTimeout = TimeSpan.FromSeconds(100);

    public HttpClient HttpClient { get; private set; }

    public string BaseURL { get; set; }

    public RestClient(string baseUrl, string apiKeyName, string apiKeyValue, HttpClient httpClient)
    {
      this.BaseURL = baseUrl;
      this.apiKeyName = apiKeyName;
      this.apiKeyValue = apiKeyValue;
      HttpClient = httpClient;
    }


    public async Task<string> RequestAsync(HttpMethod httpMethod, string apiMethod, string jsonRequest, bool throwExceptionOn404 = true, TimeSpan? requestTimeout = null, CancellationToken cancellationToken = default)
    {
      var reqMessage = CreateRequestMessage(httpMethod, apiMethod, jsonRequest);
      using (var cts = new CancellationTokenSource(requestTimeout ?? defaultRequestTimeout))
      {
        var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
        var httpResponse = await HttpClient.SendAsync(reqMessage, linkedTokenSource.Token);
        var response = await httpResponse.Content.ReadAsStringAsync();

        if (!throwExceptionOn404 && httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
          return null;
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
          throw new HttpResponseException(httpResponse.StatusCode, $"Error calling {BaseURL + apiMethod}. Reason: {httpResponse.ReasonPhrase}");
        }
        return response;
      }
    }

    private HttpRequestMessage CreateRequestMessage(HttpMethod httpMethod, string apiMethod, string jsonRequest)
    {
      var reqMessage = new HttpRequestMessage(httpMethod, new Uri(BaseURL + apiMethod));
      reqMessage.Headers.Add(apiKeyName, apiKeyValue);
      if (jsonRequest != null)
      {
        reqMessage.Content = new StringContent(jsonRequest, new UTF8Encoding(false), "application/json");
      }
      return reqMessage;
    }
  }
}
