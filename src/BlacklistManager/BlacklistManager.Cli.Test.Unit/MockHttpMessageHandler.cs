// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlacklistManager.Cli.Test.Unit
{


  /// <summary>
  /// Used to provide mock implementation to HttpClient.
  /// Does not actually send any request, but checks if a request contains expected values
  /// </summary>
  public class MockHttpMessageHandler : HttpMessageHandler
  {
    public Func<HttpRequestMessage, HttpResponseMessage> ProcessRequest { get; set; }

    public int Invoked { get; private set; } = 0;


    public HttpMethod ExpectedRequestMethod { get; }
    public string ExpectedRequestData { get; }

    public string ExpectedUrl { get; private set; }
    public MockHttpMessageHandler(HttpMethod expectedRequestMethod, string expectedUrl, string expectedRequestData, Func<HttpRequestMessage, HttpResponseMessage> processRequest = null)
    {
      this.ProcessRequest = processRequest;
      this.ExpectedUrl = expectedUrl;
      this.ExpectedRequestMethod = expectedRequestMethod;
      this.ExpectedRequestData = expectedRequestData;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
      CancellationToken cancellationToken)
    {

      Invoked++;
      if (ExpectedRequestData == null)
      {
        Assert.AreEqual(null, request.Content);
      }
      else
      {
        Assert.AreEqual(ExpectedRequestData, await request.Content.ReadAsStringAsync());
      }

      Assert.AreEqual(ExpectedRequestMethod, request.Method);
      Assert.AreEqual(ExpectedUrl, request.RequestUri.ToString());

      if (ProcessRequest == null)
      {
        throw new ArgumentException($"'{nameof(ProcessRequest)}' has not been set'");
      }

      return ProcessRequest(request);
    }
  }

}
