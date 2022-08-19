// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using Common;
using Common.SmartEnums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class SigningKeyController : ControllerBase
  {
    readonly ILogger<BlackListManagerLogger> _logger;
    readonly IDelegatedKeys _delegatedKeys;
    readonly IBackgroundJobs _backgroundJobs;
    readonly Network _bitcoinNetwork;
    readonly string _encryptionKey;

    public SigningKeyController(ILogger<BlackListManagerLogger> logger,
                                IDelegatedKeys delegatedKeys,
                                IBackgroundJobs backgroundJobs,
                                IOptions<AppSettings> options)
    {
      _logger = logger;
      _delegatedKeys = delegatedKeys;
      _backgroundJobs = backgroundJobs;
      _bitcoinNetwork = Network.GetNetwork(options.Value.BitcoinNetwork);
      _encryptionKey = options.Value.EncryptionKey;
    }

    [HttpPost()]
    public async Task<ActionResult<SignerKeyViewModelGet>> ImportSigningKeyAsync(SignerKeyViewModelCreate signerKey)
    {
      _backgroundJobs.CheckForOfflineMode();
      var key = Key.Parse(signerKey.PrivateKey, _bitcoinNetwork);
      var encrypted = EncryptionTools.Encrypt(signerKey.PrivateKey, _encryptionKey);
      var id = await _delegatedKeys.InsertDelegatedKeyAsync(encrypted, key.PubKey.ToHex(), signerKey.DelegationRequired, !signerKey.DelegationRequired);

      if (id == 0)
      {
        return Conflict();
      }

      var domainDelegatedKey = (await _delegatedKeys.GetDelegatedKeysAsync(id)).Single();
      return Ok(new SignerKeyViewModelGet(domainDelegatedKey));
    }

    [HttpGet()]
    public async Task<ActionResult<IEnumerable<SignerKeyViewModelGet>>> GetSignerKeysAsync(int? signerId)
    {
      var domainDelegatedKeys = await _delegatedKeys.GetDelegatedKeysAsync(signerId);
      if (!domainDelegatedKeys.Any())
      {
        return NotFound();
      }
      var viewKeys = domainDelegatedKeys.Select(x => new SignerKeyViewModelGet(x));

      return Ok(viewKeys);
    }


    [HttpPost("{signerId}/minerKey")]
    public async Task<ActionResult> ImportMinerKeyAsync(int signerId, MinerKeyViewModelCreate minerKey)
    {
      _backgroundJobs.CheckForOfflineMode();
      var problemDetails = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);

      if (string.IsNullOrEmpty(minerKey.PublicKey) && string.IsNullOrEmpty(minerKey.PublicKeyAddress))
      {
        problemDetails.Detail = "'publicKey' or 'publicKeyAddress' must be set.";
        return BadRequest(problemDetails);
      }

      if (!string.IsNullOrEmpty(minerKey.PublicKey))
      {
        try
        { 
          _ = new PubKey(minerKey.PublicKey);
        }
        catch (Exception ex)
        {
          problemDetails.Detail = "Invalid 'publicKey'";
          _logger.LogError($"{problemDetails.Detail}. {ex}");
          return BadRequest(problemDetails);
        }

      }
      if (!string.IsNullOrEmpty(minerKey.PublicKeyAddress))
      {
        try
        {
          _ = new BitcoinPubKeyAddress(minerKey.PublicKeyAddress, _bitcoinNetwork);
        }
        catch 
        {
          // Let's try it with segwit addresses
          try
          {
            _ = new BitcoinWitPubKeyAddress(minerKey.PublicKeyAddress, _bitcoinNetwork);
          }
          catch
          {
            try
            {
              _ = new BitcoinScriptAddress(minerKey.PublicKeyAddress, _bitcoinNetwork);
            }
            catch (Exception ex)
            {
              problemDetails.Detail = "Invalid 'publicKeyAddress'";
              _logger.LogError($"{problemDetails.Detail}. {ex}");
              return BadRequest(problemDetails);
            }
          }
        }
      }

      var delegatedKey = (await _delegatedKeys.GetDelegatedKeysAsync(signerId)).SingleOrDefault();
      if (delegatedKey == null)
      {
        return NotFound();
      }

      if (!delegatedKey.DelegationRequired)
      {
        problemDetails.Detail = "Signer key has been imported with 'delegationRequired = false'.";
        return BadRequest(problemDetails);
      }

      var createdAt = DateTime.UtcNow;

      var delegatedKeyDocument = new DelegatedKeysDocumentViewModel
      {
        CreatedAt = createdAt,
        DelegatingPublicKey = minerKey.PublicKey,
        DelegatingPublicKeyAddress = minerKey.PublicKeyAddress,
        DelegatedPublicKeys = new string[] { delegatedKey.PublicKey },
        DocumentType = DocumentType.DelegatedKeys,
        Purpose = PurposeType.LegalEntityCommunication
      };
      var delegatedKeyJSON = JsonSerializer.Serialize(delegatedKeyDocument, SerializerOptions.SerializeOptionsNoPrettyPrint);

      var id = await _delegatedKeys.InsertDelegatingKeyAsync(minerKey.PublicKeyAddress, minerKey.PublicKey, delegatedKeyJSON, createdAt, signerId);

      if (id == 0)
      {
        return Conflict();

      }
      var domainDelegatingKeys = await _delegatedKeys.GetDelegatingKeysAsync(id);

      return Ok(new MinerKeyViewModelGet (domainDelegatingKeys.Single()));
    }

    [HttpPut("{signerId}/minerKey")]
    public async Task<ActionResult<MinerKeyViewModelGet>> PutAsync(int signerId, MinerKeyViewModelUpdate minerKeyUpdate)
    {
      _backgroundJobs.CheckForOfflineMode();
      var problemDetails = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);

      var delegatingKey = (await _delegatedKeys.GetDelegatingKeysAsync(minerKeyUpdate.Id)).SingleOrDefault();
      if (delegatingKey == null)
      {
        return NotFound();
      }

      if (delegatingKey.DelegatedKeyId != signerId)
      {
        problemDetails.Detail = $"'id' with value of '{minerKeyUpdate.Id}' for miner key is not related to signer key with id '{signerId}'";
        return BadRequest(problemDetails);
      }

      var jsonEnvelope = new JsonEnvelope
      {
        Encoding = Encoding.UTF8.BodyName.ToUpper(),
        Mimetype = MediaTypeNames.Application.Json,
        Payload = delegatingKey.DataToSign,
        PublicKey = delegatingKey.PublicKey,
        SignatureType = Consts.JsonSignatureType.Bitcoin
      };

      bool signatureVerified;
      if (!string.IsNullOrEmpty(delegatingKey.PublicKeyAddress))
      {
        if (HelperTools.TryConvertFromBase64ToHex(minerKeyUpdate.Signature, out var hexString))
        {
          jsonEnvelope.Signature = hexString;
        }
        else
        {
          problemDetails.Detail = "Signature is not in base64 format";
          return BadRequest(problemDetails);
        }
        signatureVerified = SignatureTools.VerifyBitcoinSignature(jsonEnvelope.Payload, minerKeyUpdate.Signature, null, out var pubKey, delegatingKey.PublicKeyAddress, _bitcoinNetwork);
        if (!signatureVerified)
        {
          problemDetails.Detail = "Signature is invalid.";
          return BadRequest(problemDetails);
        }
        jsonEnvelope.PublicKey = pubKey;
        jsonEnvelope.SignatureType = Consts.JsonSignatureType.BitcoinMessage;
      }
      else
      {
        jsonEnvelope.Signature = minerKeyUpdate.Signature;
        signatureVerified = SignatureTools.VerifyJsonEnvelope(jsonEnvelope);
        if (!signatureVerified)
        {
          jsonEnvelope.SignatureType = Consts.JsonSignatureType.BitcoinMessage;
          signatureVerified = SignatureTools.VerifyJsonEnvelope(jsonEnvelope);
          if (!signatureVerified)
          {
            problemDetails.Detail = "Signature is not valid.";
            return BadRequest(problemDetails);
          }
        }
      }
      
      var jsonString = jsonEnvelope.ToJson();
      if (minerKeyUpdate.ActivateKey)
      {
        await _delegatedKeys.ActivateDelegatedKeyAsync(delegatingKey.DelegatedKeyId);
      }
      await _delegatedKeys.MarkDelegatingKeyValidatedAsync(delegatingKey.DelegatingKeyId, jsonString);
      delegatingKey = (await _delegatedKeys.GetDelegatingKeysAsync(minerKeyUpdate.Id)).SingleOrDefault();

      return Ok(new MinerKeyViewModelGet(delegatingKey));
    }


    [HttpGet("minerKey")]
    public async Task<ActionResult<IEnumerable<MinerKeyViewModelGet>>> GetMinerKeysAsync(int? minerId)
    {
      var domainDelegatingKeys = await _delegatedKeys.GetDelegatingKeysAsync(minerId);
      if (!domainDelegatingKeys.Any())
      {
        return NotFound();
      }
      var viewKeys = domainDelegatingKeys.Select(x => new MinerKeyViewModelGet(x));

      return Ok(viewKeys);
    }
  }
}