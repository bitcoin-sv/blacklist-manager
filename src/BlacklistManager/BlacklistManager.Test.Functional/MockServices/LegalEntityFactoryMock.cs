// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.ExternalServiceViewModel;
using System;
using System.Collections.Generic;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class LegalEntityFactoryMock : ILegalEntityFactory
  {
    private CourtOrdersViewModel courtOrdersResponse;
    private readonly Dictionary<string, Domain.Models.ConsensusActivation> consensusActivationResponse;
    private readonly Dictionary<string, string> courtOrderAcceptanceRequest;

    public string BaseURL { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public LegalEntityFactoryMock()
    {
      consensusActivationResponse = new Dictionary<string, Domain.Models.ConsensusActivation>();
      courtOrderAcceptanceRequest = new Dictionary<string, string>();
    }

    public ILegalEntity Create(string baseUrl, string deltaLink, string apiKeyName, string apiKey, int? legalEntityClientId)
    {
      var legalEntityMock = new LegalEntityMock(
        consensusActivationResponse,
        courtOrderAcceptanceRequest,
        courtOrdersResponse);

      return legalEntityMock;
    }

    public void SetConsensusActivationResponse(Domain.Models.ConsensusActivation consensusActivation, string courtOrderHash)
    {
      consensusActivationResponse.Remove(courtOrderHash);
      consensusActivationResponse.Add(courtOrderHash, consensusActivation);
    }

    public void SetCourtOrderResponse(CourtOrdersViewModel cos)
    {
      courtOrdersResponse = cos;
    }
  }
}
