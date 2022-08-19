// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.Bitcoin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BlacklistManager.API.Rest.Controllers
{
  public class ValidationProblemDetails : ProblemDetails
  {
    public ICollection<string> Errors { get; set; }
  }

  [Route("api/v1")]
  [ApiController]
  public class HomeController : ControllerBase
  {
    readonly INodes _nodes;
    readonly ILegalEndpoints _legalEndpoints;
    readonly IDelegatedKeys _delegatedKeys;
    readonly ITrustListRepository _trustListRepository;
    readonly IBitcoinFactory _bitcoindFactory;
    readonly ILegalEntityFactory _legalEntityFactory;
    readonly ICourtOrderRepository _courtOrderRepository;
    readonly IBackgroundJobs _backgroundJobs;
    readonly AppSettings _appSettings;

    public HomeController(ILegalEndpoints legalEndpoints,
                          INodes nodes,
                          IDelegatedKeys delegatedKeys,
                          ITrustListRepository trustListRepository,
                          IBitcoinFactory bitcoindFactory,
                          IOptions<AppSettings> options,
                          ILegalEntityFactory legalEntityFactory,
                          ICourtOrderRepository courtOrderRepository,
                          IBackgroundJobs backgroundJobs)
    {
      _nodes = nodes;
      _legalEndpoints = legalEndpoints;
      _delegatedKeys = delegatedKeys;
      _trustListRepository = trustListRepository;
      _bitcoindFactory = bitcoindFactory;
      _legalEntityFactory = legalEntityFactory;
      _courtOrderRepository = courtOrderRepository;
      _backgroundJobs = backgroundJobs;
      _appSettings = options.Value;
    }

    [HttpGet("status")]
    public async Task<ActionResult<StatusViewModel>> GetAppStatusAsync()
    {
      var result = new StatusViewModel();
      result.OfflineModeInitiated = _backgroundJobs.OfflineMode;

      #region Bitcoind checks
      var allNodes = await _nodes.GetNodesAsync();
      if (allNodes.Any())
      {
        foreach (var node in allNodes)
        {
          var bitcoind = _bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
          try
          {
            _ = await bitcoind.GetBestBlockHashAsync();
          }
          catch (Exception ex)
          {
            result.AddCheckMessage(CheckMessageViewModel.SetBitcoindError($"{node.Host}:{node.Port}", ex.GetBaseException().Message));
          }
        }
      }
      else
      {
        result.AddCheckMessage(CheckMessageViewModel.SetBMError(null, "Database does not contain information about nodes."));
      }
      #endregion

      #region Delegating key checks
      ActiveDelegatingKey activeKey = null;
      try
      {
        activeKey = await _delegatedKeys.GetActiveKeyForSigningAsync();
        if (activeKey == null)
        {
          result.AddCheckMessage(CheckMessageViewModel.SetBMError(null, "There are no active keys present for signing documents."));
        }
      }
      catch (Exception)
      {
        result.AddCheckMessage(CheckMessageViewModel.SetBMError(null, "Error while checking for active keys."));
      }
      #endregion

      #region LegalEntityEndpoints checks
      var allLegalEndpoints = await _legalEndpoints.GetAsync();
      if (allLegalEndpoints.Any())
      {
        foreach (var legalEntity in allLegalEndpoints)
        {
          var endpoint = _legalEntityFactory.Create(legalEntity.BaseUrl, null, Common.Consts.ApiKeyHeaderName, legalEntity.APIKey, legalEntity.LegalEntityEndpointId);
          try
          {
            var publicKey = await endpoint.GetPublicKeyAsync();

            if (!await _trustListRepository.IsPublicKeyTrustedAsync(publicKey))
            {
              result.AddCheckMessage(CheckMessageViewModel.SetBMError(null, $"Public key '{publicKey}' used to sign the test payload is not trusted."));
            }
          }
          catch (Exception ex)
          {
            result.AddCheckMessage(CheckMessageViewModel.SetAlertManagerError(legalEntity.BaseUrl, ex.GetBaseException().Message));
          }

          if (activeKey != null)
          {
            try
            {
              CheckMinedBlocksRequestViewModel checkMinedBlocks = new CheckMinedBlocksRequestViewModel
              {
                DelegatedKeys = activeKey.SignedDelegatedKeyJSON
              };
              var checkMinedBlocksJSON = JsonSerializer.Serialize(checkMinedBlocks, SerializerOptions.SerializeOptionsNoPrettyPrint);
              var wifKey = EncryptionTools.Decrypt(activeKey.DelegatedPrivateKey, _appSettings.EncryptionKey);
              var response = await endpoint.CheckMinedBlocksAsync(SignatureTools.CreateJSonSignature(checkMinedBlocksJSON, wifKey));
              var blocks = JsonSerializer.Deserialize<CheckMinedBlocksResponseViewModel>(response, SerializerOptions.SerializeOptions);

              foreach (var keyResponse in blocks.MinedBlocks)
              {
                string pkString = null;
                if (!string.IsNullOrEmpty(keyResponse.PublicKey))
                {
                  pkString = $"'{keyResponse.PublicKey}' public key";
                }
                else if (!string.IsNullOrEmpty(keyResponse.PublicKeyAddress))
                {
                  pkString = $"'{keyResponse.PublicKeyAddress}' public key address";
                }

                if (keyResponse.NumberOfMinedBlocks == 0)
                {
                  result.AddCheckMessage(CheckMessageViewModel.SetAlertManagerWarning(legalEntity.BaseUrl, $"No blocks have been mined with {pkString}."));
                }
                else
                {
                  result.AddCheckMessage(CheckMessageViewModel.SetAlertManagerInfo(legalEntity.BaseUrl, $"{keyResponse.NumberOfMinedBlocks} blocks of last {keyResponse.NumberOfBlocksToCheck} have been mined with {pkString}."));
                }
              }

              var pendingConsensusActivations = (await _courtOrderRepository.GetPendingConsensusActivationsAsync(_appSettings.MaxRetryCount, _appSettings.ConsensusWaitDays)).Count();
              if (pendingConsensusActivations > 0)
              {
                result.AddCheckMessage(CheckMessageViewModel.SetAlertManagerInfo(legalEntity.BaseUrl, $"{pendingConsensusActivations} pending consensus activations."));
              }

              var strBuilder = new List<string>();
              strBuilder.Add($"{legalEntity.ProcessedOrdersCount} court orders successfully processed. Last successful contact at {legalEntity.LastContactedAt}");
              if (legalEntity.FailureCount > 0)
              {
                strBuilder.Add($"{legalEntity.FailureCount} failures occurred.");
                strBuilder.Add($"Last error at '{legalEntity.LastErrorAt}'. ErroMessage: '{legalEntity.LastError}'");
              }
              result.AddCheckMessage(CheckMessageViewModel.SetAlertManagerInfo(legalEntity.BaseUrl, strBuilder.ToArray()));

            }
            catch (Exception ex)
            {
              result.AddCheckMessage(CheckMessageViewModel.SetAlertManagerError(legalEntity.BaseUrl, ex.GetBaseException().Message));
            }
          }
        }
      }
      else
      {
        result.AddCheckMessage(CheckMessageViewModel.SetBMError(null, "Database does not contain information about legal entity endpoints."));
      }
      #endregion

      #region BackgroundJob status

      result.BackgroundJobStatuses = _backgroundJobs.BackgroundTasks.Tasks.Select(x => new BackgroundJobStatusViewModel
      {
        Name = x.Key,
        Status = x.Value.Status.ToString()
      }).ToArray();

      #endregion

      result.AppSettings = _appSettings;
      return Ok(result);
    }

    [HttpPost("offline")]
    [Authorize]
    public async Task<IActionResult> SetOfflineModeAsync(bool offlineMode)
    {
      await _backgroundJobs.SetOfflineModeAsync(offlineMode);
      return Ok();
    }
  }
}