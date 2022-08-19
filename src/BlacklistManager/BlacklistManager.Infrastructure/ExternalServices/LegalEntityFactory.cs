// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using BlacklistManager.Domain.ExternalServices;
using Common;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class LegalEntityFactory : ILegalEntityFactory
  {
    public const string CLIENT_NAME = "LegalEntityFactoryClient";
    private IRestClient _restClient;
    public string BaseURL { get => _restClient.BaseURL; set => _restClient.BaseURL = value; }

    readonly IHttpClientFactory _httpClientFactory;
    readonly IOptions<AppSettings> _options;

    public LegalEntityFactory(IHttpClientFactory httpClientFactory, IOptions<AppSettings> options)
    {
      _httpClientFactory = httpClientFactory;
      _options = options;
    }

    public ILegalEntity Create(string baseUrl, string deltaLink, string apiKeyName, string apiKeyValue, int? legalEntityClientId)
    {
      var client = _httpClientFactory.CreateClient(CLIENT_NAME);
      _restClient = new RestClient(baseUrl, apiKeyName, apiKeyValue, client);
      return new LegalEntity(_restClient, deltaLink, _options.Value.BitcoinNetwork, legalEntityClientId);
    }
  }
}
