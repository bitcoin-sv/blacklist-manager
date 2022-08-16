// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.BitcoinRpc;
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
    readonly INodes nodes;
    readonly ILegalEndpoints legalEndpoints;
    readonly IDelegatedKeys delegatedKeys;
    readonly ITrustListRepository trustListRepository;
    readonly IBitcoindFactory bitcoindFactory;
    readonly ILegalEntityFactory legalEntityFactory;
    readonly AppSettings appSettings;

    public HomeController(ILegalEndpoints legalEndpoints,
                          INodes nodes,
                          IDelegatedKeys delegatedKeys,
                          ITrustListRepository trustListRepository,
                          IBitcoindFactory bitcoindFactory,
                          IOptions<AppSettings> options,
                          ILegalEntityFactory legalEntityFactory)
    {
      this.nodes = nodes;
      this.legalEndpoints = legalEndpoints;
      this.delegatedKeys = delegatedKeys;
      this.trustListRepository = trustListRepository;
      this.bitcoindFactory = bitcoindFactory;
      this.legalEntityFactory = legalEntityFactory;
      this.appSettings = options.Value;
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetAppStatusAsync()
    {
      #region Bitcoind checks
      List<CheckMessageViewModel> checkMessages = new List<CheckMessageViewModel>();
      var allNodes = nodes.GetNodes();
      if (allNodes.Any())
      {
        foreach (var node in allNodes)
        {
          var bitcoind = bitcoindFactory.Create(node.Host, node.Port, node.Username, node.Password);
          string blockHash = null;
          try
          {
            blockHash = await bitcoind.GetBestBlockHashAsync();
          }
          catch (RpcException ex)
          {
            checkMessages.Add(CheckMessageViewModel.SetBitcoindError($"{node.Host}:{node.Port}", ex.GetBaseException().Message));
          }
        }
      }
      else
      {
        checkMessages.Add(CheckMessageViewModel.SetBMError(null, "Database does not contain information about nodes."));
      }
      #endregion

      ActiveDelegatingKey activeKey = null;
      try
      {
        activeKey = await delegatedKeys.GetActiveKeyForSigningAsync();
        if (activeKey == null)
        {
          checkMessages.Add(CheckMessageViewModel.SetBMError(null, "There are no active keys present for signing documents."));
        }
      }
      catch (Exception)
      {
        checkMessages.Add(CheckMessageViewModel.SetBMError(null, "Error while checking for active keys."));
      }

      #region LegalEntityEndpoints checks
      var allLegalEndpoints = await legalEndpoints.GetLegalEntitiyEndpointsAsync();
      if (allLegalEndpoints.Any())
      {
        foreach (var legalEntity in allLegalEndpoints)
        {
          var endpoint = legalEntityFactory.Create(legalEntity.BaseUrl, legalEntity.APIKey);
          try
          {
            var publicKey = await endpoint.GetPublicKeyAsync();

            if (!trustListRepository.IsPublicKeyTrusted(publicKey))
            {
              checkMessages.Add(CheckMessageViewModel.SetBMError(null, $"Public key '{publicKey}' used to sign the test payload is not trusted."));
            }
          }
          catch (Exception ex)
          {
            checkMessages.Add(CheckMessageViewModel.SetAlertManagerError(legalEntity.BaseUrl, ex.GetBaseException().Message));
          }

          if (activeKey != null)
          {
            try
            {
              CheckMinedBlocksRequestViewModel checkMinedBlocks = new CheckMinedBlocksRequestViewModel
              {
                DelegatedKeys = activeKey.SignedDelegatedKeyJSON
              };
              var checkMinedBlocksJSON = JsonSerializer.Serialize(checkMinedBlocks, SerializerOptions.SerializeOptions);
              var wifKey = Encryption.Decrypt(activeKey.DelegatedPrivateKey, appSettings.EncryptionKey);
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
                  checkMessages.Add(CheckMessageViewModel.SetAlertManagerWarning(legalEntity.BaseUrl, $"No blocks have been mined with {pkString}."));
                }
                else
                {
                  checkMessages.Add(CheckMessageViewModel.SetAlertManagerInfo(legalEntity.BaseUrl, $"{keyResponse.NumberOfMinedBlocks} blocks of last {keyResponse.NumberOfBlocksToCheck} have been mined with {pkString}."));
                }
              }
            }
            catch (Exception ex)
            {
              checkMessages.Add(CheckMessageViewModel.SetAlertManagerError(legalEntity.BaseUrl, ex.GetBaseException().Message));
            }
          }
        }
      }
      else
      {
        checkMessages.Add(CheckMessageViewModel.SetBMError(null,  "Database does not contain information about legal entity endpoints."));
      }
      #endregion

      var statusResult = new StatusViewModel
      {
        CheckMessages = checkMessages.ToArray(),
        AppSettings = appSettings
      };
      return Content(HelperTools.JSONSerializeNewtonsoft(statusResult, true), MediaTypeNames.Application.Json, Encoding.UTF8);
    }
  }
}