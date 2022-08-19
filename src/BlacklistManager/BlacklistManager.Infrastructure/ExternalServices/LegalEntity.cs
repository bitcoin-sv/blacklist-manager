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
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class LegalEntity : ILegalEntity
  {
    private readonly IRestClient _restClient;

    private const string API_BASE = "/api/v1";
    readonly string _network;

    public string BaseUrl { get => _restClient.BaseURL; set => _restClient.BaseURL = value; }
    public int? LegalEntityClientId { get; init; }
    public bool IsFinished { get; set; }
    public string DeltaLink { get; set; }

    public LegalEntity(IRestClient restClient, string deltaLink, string network, int? legalEntityClientId)
    {
      _restClient = restClient;
      _network = network;
      LegalEntityClientId = legalEntityClientId;
      DeltaLink = deltaLink;
    }

    public async Task<ConsensusActivation> GetConsensusActivationAsync(string courtOrderHash, CancellationToken cancellationToken)
    {
      var response = await _restClient.RequestAsync(HttpMethod.Get, $"{API_BASE}/courtOrder/{courtOrderHash}/consensusActivation", null, cancellationToken: cancellationToken);

      if (string.IsNullOrEmpty(response))
      {
        return null;
      }

      var signedPayloads = JsonSerializer.Deserialize<IEnumerable<SignedPayload>>(response, SerializerOptions.SerializeOptions);
      if (signedPayloads == null || signedPayloads.Count() > 1)
      {
        return null;
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
      var caViewModel = JsonSerializer.Deserialize<ConsensusActivationViewModel>(signedPayload.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);
      vc = new ValidationContext(caViewModel, null, null);
      vr = new List<ValidationResult>();
      var isCaViewModelValid = Validator.TryValidateObject(caViewModel, vc, vr, true);
      if (!isCaViewModelValid)
      {
        throw new ValidationException($"Consensus activation payload validation errors: {string.Join(",", vr.Select(e => e.ErrorMessage))}");
      }

      var caHash = SignatureTools.GetSigDoubleHash(signedPayload.Payload, signedPayload.Encoding);

      var consensusActivation = new ConsensusActivation(
        JsonSerializer.Serialize(signedPayload, SerializerOptions.SerializeOptions),
        caViewModel.CourtOrderHash,
        caViewModel.EnforceAtHeight.Value,
        signedPayload.PublicKey,
        caViewModel.SignedDate,
        caHash);

      if (!consensusActivation.PrepareChainedTransactions(caViewModel.ConfiscationTimelockedTxs, _network, out var error))
      {
        throw new ValidationException(error);
      }

      int i = 0;
      foreach (var acceptanceItem in caViewModel.Acceptances)
      {
        vc = new ValidationContext(acceptanceItem, null, null);
        vr = new List<ValidationResult>();
        var isAcceptanceValid = Validator.TryValidateObject(acceptanceItem, vc, vr, true);
        if (!isAcceptanceValid)
        {
          throw new ValidationException($"Court order acceptance validation errors: {string.Join(",", vr.Select(e => e.ErrorMessage))}");
        }
        signedPayload = JsonSerializer.Deserialize<SignedPayload>(acceptanceItem.SignedAcceptanceJson, SerializerOptions.SerializeOptions);
        if (signedPayload == null)
        {
          throw new BadRequestException($"Json envelope deserialization for signed acceptance was unsuccessful.");
        }
        if (signedPayload.Payload == null)
        {
          throw new BadRequestException("Payload is missing in JSONEnvelope for CourtOrderAcceptance.");
        }
        var acceptanceViewModel = JsonSerializer.Deserialize<AcceptanceViewModel>(signedPayload.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);

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

    public async Task<CourtOrdersViewModel> GetCourtOrdersAsync(bool useDeltaLink, CancellationToken cancellationToken)
    {
      string response;
      if (useDeltaLink)
      {
        if (string.IsNullOrEmpty(DeltaLink))
        {
          throw new InvalidOperationException("Delta link is null or empty, not possible to use delta link.");
        }
        var baseUrl = BaseUrl;
        _restClient.BaseURL = DeltaLink;
        response = await _restClient.RequestAsync(HttpMethod.Get, null, null, cancellationToken: cancellationToken);
        _restClient.BaseURL = baseUrl;
      }
      else
      {
        response = await _restClient.RequestAsync(HttpMethod.Get, $"{API_BASE}/courtOrder/", null, cancellationToken: cancellationToken);
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

    public async Task<SignedPayloadViewModel> GetCourtOrderByHashAsync(string courtOrderHash, CancellationToken cancellationToken)
    {
      var response = await _restClient.RequestAsync(HttpMethod.Get, $"{API_BASE}/courtOrder/{courtOrderHash}", null, cancellationToken: cancellationToken);

      return JsonSerializer.Deserialize<SignedPayloadViewModel>(response, SerializerOptions.SerializeOptions);
    }

    public async Task PostCourtOrderAcceptanceAsync(string courtOrderHash, string coAcceptanceJsonEnvelope, CancellationToken cancellationToken)
    {
      await _restClient.RequestAsync(HttpMethod.Post, $"{API_BASE}/courtOrder/{courtOrderHash}/acceptance", coAcceptanceJsonEnvelope, cancellationToken: cancellationToken);
    }

    public async Task<string> CheckMinedBlocksAsync(string requestPayload)
    {
      var blocks = await _restClient.RequestAsync(HttpMethod.Post, $"{API_BASE}/checkMinedBlocks", requestPayload, true, TimeSpan.FromSeconds(5));

      return blocks;
    }

    public async Task<string> GetPublicKeyAsync()
    {
      var publicKey = await _restClient.RequestAsync(HttpMethod.Get, $"{API_BASE}/getPublicKey", null, true, TimeSpan.FromSeconds(1));
      return publicKey;
    }
  }
}
