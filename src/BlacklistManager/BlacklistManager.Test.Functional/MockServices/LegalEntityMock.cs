// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.ExternalServiceViewModel;
using Common;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class LegalEntityMock : ILegalEntity
  {
    private readonly CourtOrdersViewModel courtOrdersResponse;
    private readonly Dictionary<string, Domain.Models.ConsensusActivation> consensusActivationResponse;
    private readonly Dictionary<string, string> courtOrderAcceptanceRequest;

    public string BaseUrl { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public LegalEntityMock(
      Dictionary<string, Domain.Models.ConsensusActivation> consensusActivationResponse,
      Dictionary<string, string> courtOrderAcceptanceReceived,
      CourtOrdersViewModel courtOrderResponse)
    {
      this.consensusActivationResponse = consensusActivationResponse;
      this.courtOrderAcceptanceRequest = courtOrderAcceptanceReceived;
      this.courtOrdersResponse = courtOrderResponse;
    }

    public async Task<Domain.Models.ConsensusActivation> GetConsensusActivationAsync(string courtOrderHash)
    {
      if (courtOrderAcceptanceRequest.ContainsKey(courtOrderHash))
      {
        if (consensusActivationResponse.TryGetValue(courtOrderHash, out Domain.Models.ConsensusActivation ca))
        {
          return await Task.FromResult(ca);
        }
      }
      return null;
    }

    public async Task<CourtOrdersViewModel> GetCourtOrdersAsync(bool requestingDelta)
    {
      return await Task.FromResult(courtOrdersResponse);
    }

    public async Task PostCourtOrderAcceptanceAsync(string courtOrderHash, string coAcceptanceJsonEnvelope)
    {
      courtOrderAcceptanceRequest.Remove(courtOrderHash);
      courtOrderAcceptanceRequest.Add(courtOrderHash, coAcceptanceJsonEnvelope);
      await Task.CompletedTask;
    }

    public Task<string> CheckMinedBlocksAsync(string requestPayload)
    {
      throw new NotImplementedException();
    }

    public Task<string> GetPublicKeyAsync()
    {
      return Task.FromResult("0293ff7c31eaa93ce4701a462676c1e46dac745f6848097f57357d2a414b379a34");
    }
  }
}
