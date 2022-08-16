// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.ExternalServices
{
  public interface ILegalEntityFactory
  {
    ILegalEntity Create(string baseUrl, string apiKey);

    public string BaseURL { get; set; }
  }
}
