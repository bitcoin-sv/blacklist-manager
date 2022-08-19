// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using Moq;
using System;

namespace BlacklistManager.Test.Unit.Mocks
{
  public class LegalEntityFactoryMock : ILegalEntityFactory
  {
    readonly ILegalEntity legalEntity;

    public LegalEntityFactoryMock(ILegalEntity legalEntity)
    {
      this.legalEntity = legalEntity;   
    }

    public string BaseURL { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public ILegalEntity Create(string baseUrl, string deltaLink, string apiKeyName, string apiKey, int? legalEntityClientId)
    {
      if (legalEntity == null)
      {
        return new Mock<ILegalEntity>().Object;
      }
      return legalEntity;
    }
  }
}
