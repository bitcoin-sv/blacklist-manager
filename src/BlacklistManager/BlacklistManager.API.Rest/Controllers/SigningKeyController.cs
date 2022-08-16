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
using BlacklistManager.Domain.Models;
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
    readonly ILogger<BlackListManagerLogger> logger;
    readonly IDelegatedKeys delegatedKeys;
    readonly Network BitcoinNetwork;
    readonly string encryptionKey;

    public SigningKeyController(ILogger<BlackListManagerLogger> logger,
                                IDelegatedKeys delegatedKeys,
                                IOptions<AppSettings> options)
    {
      this.logger = logger;
      this.delegatedKeys = delegatedKeys;
      BitcoinNetwork = Network.GetNetwork(options.Value.BitcoinNetwork);
      encryptionKey = options.Value.EncryptionKey;
    }

    [HttpPost()]
    public async Task<ActionResult> ImportSigningKeyAsync(SignerKeyViewModelCreate signerKey)
    {
      var key = Key.Parse(signerKey.PrivateKey, BitcoinNetwork);
      var encrypted = Encryption.Encrypt(signerKey.PrivateKey, encryptionKey);
      var id = await delegatedKeys.InsertDelegatedKeyAsync(encrypted, key.PubKey.ToHex(), signerKey.DelegationRequired, !signerKey.DelegationRequired);

      if (id == 0)
      {
        return Conflict();
      }

      return Ok(id);
    }

    [HttpGet()]
    public async Task<ActionResult<IEnumerable<SignerKeyViewModelGet>>> GetSignerKeysAsync(int? signerId)
    {
      var domainDelegatedKeys = await delegatedKeys.GetDelegatedKeysAsync(signerId);
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
          logger.LogError($"{problemDetails.Detail}. {ex}");
          return BadRequest(problemDetails);
        }

      }
      if (!string.IsNullOrEmpty(minerKey.PublicKeyAddress))
      {
        try
        {
          _ = new BitcoinPubKeyAddress(minerKey.PublicKeyAddress, BitcoinNetwork);
        }
        catch 
        {
          // Let's try it with segwit addresses
          try
          {
            _ = new BitcoinWitPubKeyAddress(minerKey.PublicKeyAddress, BitcoinNetwork);
          }
          catch
          {
            try
            {
              _ = new BitcoinScriptAddress(minerKey.PublicKeyAddress, BitcoinNetwork);
            }
            catch (Exception ex)
            {
              problemDetails.Detail = "Invalid 'publicKeyAddress'";
              logger.LogError($"{problemDetails.Detail}. {ex}");
              return BadRequest(problemDetails);
            }
          }
        }
      }

      var delegatedKey = (await delegatedKeys.GetDelegatedKeysAsync(signerId)).SingleOrDefault();
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

      var id = await delegatedKeys.InsertDelegatingKeyAsync(minerKey.PublicKeyAddress, minerKey.PublicKey, delegatedKeyJSON, createdAt, signerId);

      if (id == 0)
      {
        return Conflict();

      }
      var domainDelegatingKeys = await delegatedKeys.GetDelegatingKeysAsync(id);

      return Ok(new MinerKeyViewModelGet (domainDelegatingKeys.Single()));
    }

    [HttpPut("{signerId}/minerKey")]
    public async Task<ActionResult> PutAsync(int signerId, MinerKeyViewModelUpdate minerKeyUpdate)
    {
      var problemDetails = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);

      var delegatingKey = (await delegatedKeys.GetDelegatingKeysAsync(minerKeyUpdate.Id)).SingleOrDefault();
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
        signatureVerified = SignatureTools.VerifyBitcoinSignature(jsonEnvelope.Payload, minerKeyUpdate.Signature, null, out var pubKey, delegatingKey.PublicKeyAddress, BitcoinNetwork);
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
      
      var jsonString = JsonSerializer.Serialize(jsonEnvelope, SerializerOptions.SerializeOptions);
      if (minerKeyUpdate.ActivateKey)
      {
        await delegatedKeys.ActivateDelegatedKeyAsync(delegatingKey.DelegatedKeyId);
      }
      await delegatedKeys.MarkDelegatingKeyValidatedAsync(delegatingKey.DelegatingKeyId, jsonString);

      return Ok("minerKey was successfully validated.");
    }


    [HttpGet("minerKey")]
    public async Task<ActionResult<IEnumerable<MinerKeyViewModelGet>>> GetMinerKeysAsync(int? minerId)
    {
      var domainDelegatingKeys = await delegatedKeys.GetDelegatingKeysAsync(minerId);
      if (!domainDelegatingKeys.Any())
      {
        return NotFound();
      }
      var viewKeys = domainDelegatingKeys.Select(x => new MinerKeyViewModelGet(x));

      return Ok(viewKeys);
    }
  }
}