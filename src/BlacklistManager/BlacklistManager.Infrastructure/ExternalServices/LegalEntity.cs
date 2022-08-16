// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.ExternalServiceViewModel;
using BlacklistManager.Domain.Models;
using Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class LegalEntity : ILegalEntity
  {
    private readonly IRestClient restClient;

    private const string API_BASE = "/api/v1";

    public string BaseUrl { get => restClient.BaseURL; set => restClient.BaseURL = value; }

    public LegalEntity(IRestClient restClient)
    {
      this.restClient = restClient;
    }    

    public async Task<ConsensusActivation> GetConsensusActivationAsync(string courtOrderHash)
    {
      var response =  await restClient.RequestAsync(HttpMethod.Get, $"{API_BASE}/courtOrder/{courtOrderHash}/consensusActivation", null, false);

      if (string.IsNullOrEmpty(response))
      {
        return null;
      }

      var signedPayloads = JsonSerializer.Deserialize<IEnumerable<SignedPayload>>(response);
      if (signedPayloads == null || signedPayloads.Count() > 1)
      {
        throw new BadRequestException($"Consensus activation deserialization for {courtOrderHash} was unsuccessful.");
      }
      
      // Consensus hasn't been created yet
      if (!signedPayloads.Any())
      {
        return null;
      }

      var signedPayload = signedPayloads.Single();
      var vc = new ValidationContext(signedPayload, null, null);
      ICollection<ValidationResult> vr = new List<ValidationResult>();
      var isSignedPayloadValid = Validator.TryValidateObject(signedPayload, vc, vr, true);
      if (!isSignedPayloadValid)
      {
        throw new ValidationException($"Consensus activation JsonEnvelope validation errors: {string.Join(",", vr.Select(e => e.ErrorMessage))}");
      }

      if (signedPayload.Payload == null)
      {
        throw new BadRequestException("Payload is missing in JSONEnvelope.");
      }
      var caViewModel = JsonSerializer.Deserialize<ConsensusActivationViewModel>(signedPayload.Payload);
      vc = new ValidationContext(caViewModel, null, null);
      vr = new List<ValidationResult>();
      var isCaViewModelValid = Validator.TryValidateObject(caViewModel, vc, vr, true);
      if (!isCaViewModelValid)
      {
        throw new ValidationException($"Consensus activation payload validation errors: {string.Join(",", vr.Select(e => e.ErrorMessage))}");
      }

      var caHash = SignatureTools.GetSigDoubleHash(signedPayload.Payload, signedPayload.Encoding);

      var consensusActivation = new ConsensusActivation(
        JsonSerializer.Serialize(signedPayload),
        caViewModel.CourtOrderHash,
        caViewModel.EnforceAtHeight.Value,
        signedPayload.PublicKey,
        caHash);

      int i = 0;
      foreach (var acceptanceItem in caViewModel.Acceptances)
      {
        signedPayload = JsonSerializer.Deserialize<SignedPayload>(acceptanceItem.SignedAcceptanceJson);
        if (signedPayload == null)
        {
          throw new BadRequestException($"Json envelope deserialization for signed acceptance was unsuccessful.");
        }
        if (signedPayload.Payload == null)
        { 
          throw new BadRequestException("Payload is missing in JSONEnvelope for CourtOrderAcceptance.");
        }
        var acceptanceViewModel = JsonSerializer.Deserialize<AcceptanceViewModel>(signedPayload.Payload);

        vc = new ValidationContext(acceptanceViewModel, null, null);
        vr = new List<ValidationResult>();
        var isAcceptanceViewModelValid = Validator.TryValidateObject(acceptanceViewModel, vc, vr, true);
        if (!isAcceptanceViewModelValid)
        {
          throw new ValidationException($"Consensus activation acceptance payload [{i}] validation errors: {string.Join(",", vr.Select(e => e.ErrorMessage))}");
        }

        consensusActivation.AddAcceptance(acceptanceItem.SignedAcceptanceJson, signedPayload.PublicKey, acceptanceViewModel.CourtOrderHash);
        i++;
      }

      return consensusActivation;
    }    

    public async Task<CourtOrdersViewModel> GetCourtOrdersAsync(bool ignoreAPIMethod)
    {
      string response;
      if (ignoreAPIMethod)
      {
        response = await restClient.RequestAsync(HttpMethod.Get, null, null, true);
      }
      else
      {
        response = await restClient.RequestAsync(HttpMethod.Get, $"{API_BASE}/courtOrder/", null, true);
      }
      if (response == null)
      {
        return null;
      }

      CourtOrdersViewModel coViewModel = null;
      if (!string.IsNullOrEmpty(response))
      {
        coViewModel = HelperTools.JSONDeserializeNewtonsoft<CourtOrdersViewModel>(response);
      }

      return coViewModel;
    }

    public async Task PostCourtOrderAcceptanceAsync(string courtOrderHash, string coAcceptanceJsonEnvelope)
    {
      await restClient.RequestAsync(HttpMethod.Post, $"{API_BASE}/courtOrder/{courtOrderHash}/acceptance", coAcceptanceJsonEnvelope, true);
    }

    public async Task<string> CheckMinedBlocksAsync(string requestPayload)
    {
      var blocks = await restClient.RequestAsync(HttpMethod.Post, $"{API_BASE}/checkMinedBlocks", requestPayload, true, TimeSpan.FromSeconds(15));

      return blocks;
    }

    public async Task<string> GetPublicKeyAsync()
    {
      var publicKey = await restClient.RequestAsync(HttpMethod.Get, $"{API_BASE}/getPublicKey", null, true, TimeSpan.FromSeconds(15));
      return publicKey;
    }
  }
}
