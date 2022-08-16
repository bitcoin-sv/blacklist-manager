// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class LegalEntityFactory : ILegalEntityFactory
  {
    private IRestClient restClient;
    public string BaseURL { get => restClient.BaseURL; set => restClient.BaseURL = value; }
    readonly IBlacklistHttpClientFactory blacklistHttpClientFactory;

    public LegalEntityFactory(IBlacklistHttpClientFactory blacklistHttpClientFactory)
    {
      this.blacklistHttpClientFactory = blacklistHttpClientFactory;
    }

    public ILegalEntity Create(string baseUrl, string apiKey)
    {
      restClient = new RestClient(baseUrl, apiKey, blacklistHttpClientFactory);
      return new LegalEntity(restClient);
    }
  }
}
