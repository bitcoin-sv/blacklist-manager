// Copyright (c) 2020 Bitcoin Association

using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.Text;
using System.Text.Json;

namespace Common
{
  public static class SignatureTools
  {
    /// <summary>
    ///  Calculates double SHA256 hash of payload and returns it as hex string
    /// </summary>
    public static string GetSigDoubleHash(string payload, string encodingName)
    {
      var sigHash = GetSigHash(payload, encodingName);
      byte[] doubleHash = Hashes.SHA256(sigHash);

      Array.Reverse(doubleHash);

      return Encoders.Hex.EncodeData(doubleHash);
    }

    public static byte[] GetSigHash(string payload, string encodingName) // throws an exception with friendly message if encoding is not found
    {
      if (string.IsNullOrEmpty(payload))
      {
        throw new BadRequestException("'payload' parameter cannot be null or empty.");
      }

      byte[] bytes;
      if (encodingName == "base64")
      {
        // treat as binary
        bytes = Convert.FromBase64String(payload);
      }
      else
      {
        Encoding encoding;
        try
        {
          encoding = Encoding.GetEncoding(encodingName);
        }
        catch (ArgumentException ex)
        {
          throw new BadRequestException($"Unsupported JSonEnvelope encoding :{encodingName} ", ex);
        }

        // treat as string
        bytes = encoding.GetBytes(payload);
      }
      return Hashes.SHA256(bytes);
    }

    public static bool VerifyJsonEnvelope(string jsonString)
    {
      var envelope = JsonSerializer.Deserialize<JsonEnvelope>(jsonString, SerializerOptions.SerializeOptions);
      return VerifyJsonEnvelope(envelope);
    }

    /// <summary>
    /// This method verifies signature against hash of payload instead of payload itself
    /// </summary>
    public static bool VerifyCourtOrderJsonEnvelope(JsonEnvelope envelope)
    {
      var hashEnvelope = new JsonEnvelope
      {
        Encoding = envelope.Encoding,
        Mimetype = envelope.Mimetype,
        PublicKey = envelope.PublicKey,
        Signature = envelope.Signature,
        SignatureType = envelope.SignatureType,
        Payload = new uint256(SignatureTools.GetSigHash(envelope.Payload, "UTF-8")).ToString()
      };
      return VerifyJsonEnvelope(hashEnvelope);
    }

    public static bool VerifyJsonEnvelope(JsonEnvelope envelope)
    {
      if (envelope == null)
      {
        throw new BadRequestException("JsonEnvelope must not be null");
      }

      if (string.IsNullOrEmpty(envelope.Payload))
      {
        throw new BadRequestException("JsonEnvelope must contain non-empty 'payload'");
      }

      if (string.IsNullOrEmpty(envelope.PublicKey))
      {
        throw new BadRequestException("JsonEnvelope must contain non-empty 'publicKey'");
      }

      if (string.IsNullOrEmpty(envelope.Signature))
      {
        throw new BadRequestException("JsonEnvelope must contain non-empty 'signature'");
      }

      if (!string.IsNullOrEmpty(envelope.SignatureType))
      {
        if (!Consts.JsonSignatureType.IsValid(envelope.SignatureType))
        {
          throw new BadRequestException($"'signatureType' contains unsupported value '{envelope.SignatureType}'");
        }
        if (Consts.JsonSignatureType.BitcoinMessage == envelope.SignatureType)
        {
          return VerifyBitcoinSignature(envelope.Payload, HelperTools.ConvertFromHexToBase64(envelope.Signature), envelope.PublicKey, out _);
        }
      }

      var signature = ECDSASignature.FromDER(Encoders.Hex.DecodeData(envelope.Signature));
      var pubKey = new PubKey(envelope.PublicKey);

      return pubKey.Verify(new uint256(GetSigHash(envelope.Payload, envelope.Encoding)), signature);
    }

    // Maps base68 private key prefixes to network https://en.bitcoin.it/wiki/List_of_address_prefixes
    private static Dictionary<string, Network> prefixToNetwork = new Dictionary<string, Network>
    {
      {"5", Network.Main}, // Private key (WIF, uncompressed pubkey)
      {"K", Network.Main}, // Private key (WIF, compressed pubkey)
      {"L", Network.Main}, // Private key (WIF, compressed pubkey)
      {"xprv", Network.Main}, // BIP32 private key

      {"9", Network.TestNet}, // Testnet Private key (WIF, uncompressed pubkey)
      {"c", Network.TestNet},  // Testnet Private key (WIF, compressed pubkey)
      {"tprv", Network.TestNet}, // Testnet BIP32 private key
    };

    public static string SignHash(string hashHex, string privateKeyWif)
    {
      if (!prefixToNetwork.TryGetValue(privateKeyWif.Substring(0, 1), out Network network))
      {
        throw new BadRequestException("Unknown private key format");
      }

      var key = Key.Parse(privateKeyWif, network);

      var signature = key.SignMessage(hashHex);

      return HelperTools.ConvertFromBase64ToHex(signature);      
    }

    public static string CreateSignature(string payload, string encoding, string mimetype, string privateKeyWif)
    {
      var sigHash = new uint256(GetSigHash(payload, encoding));

      if (!prefixToNetwork.TryGetValue(privateKeyWif.Substring(0, 1), out Network network))
      {
        throw new BadRequestException("Unknown private key format");
      }

      var key = Key.Parse(privateKeyWif, network);

      var signature = key.Sign(sigHash);

      var envelope = new JsonEnvelope
      {
        Payload = payload,
        Encoding = encoding,
        Mimetype = mimetype,
        PublicKey = key.PubKey.ToHex(),
        Signature = Encoders.Hex.EncodeData(signature.ToDER()),
        SignatureType = Consts.JsonSignatureType.Bitcoin
      };

      return HelperTools.JSONSerializeNewtonsoft(envelope, true);
    }

    public static string CreateJSonSignature(string json, string privateKeyWif)
    {
      return CreateSignature(json, Encoding.UTF8.BodyName.ToUpper(), MediaTypeNames.Application.Json, privateKeyWif);
    }

    //Method is marked as internal to prevent anyone outside Tests to accidentally to use it, because it creates random new key each time it's called
    internal static string CreateJSONWithBitcoinSignature(string json, string privateKeyWif, Network network, bool hashPayload = false)
    {
      var key = Key.Parse(privateKeyWif, network);
      string messageSignature;
      if (hashPayload)
      {
        messageSignature = key.SignMessage(new uint256(GetSigHash(json, Encoding.UTF8.BodyName.ToUpper())).ToString());
      }
      else
      {
        messageSignature = key.SignMessage(json);
      }

      var envelope = new JsonEnvelope
      {
        Payload = json,
        Encoding = Encoding.UTF8.BodyName.ToUpper(),
        Mimetype = MediaTypeNames.Application.Json,
        Signature = HelperTools.ConvertFromBase64ToHex(messageSignature),
        PublicKey = key.PubKey.ToHex(),
        SignatureType = Consts.JsonSignatureType.BitcoinMessage
      };

      return JsonSerializer.Serialize(envelope, SerializerOptions.SerializeOptions);
    }

    public static bool VerifyBitcoinSignature(string jsonPayload, string signature, string publicKey, out string pubKeyHex, string address = null, Network network = null)
    {
      pubKeyHex = null;
      PubKey pubKey;
      try
      {
        pubKey = PubKey.RecoverFromMessage(jsonPayload, signature);
      }
      catch
      {
        // Signature is invalid
        return false;
      }

      if (address != null)
      {
        if (network == null)
        {
          throw new BadRequestException("Network is not set.");
        }
        if (pubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString() != address &&
            pubKey.GetAddress(ScriptPubKeyType.Segwit, network).ToString() != address &&
            pubKey.GetAddress(ScriptPubKeyType.SegwitP2SH, network).ToString() != address)
        {
          return false;
        }

        pubKeyHex = pubKey.ToHex();
        return true;
      }
      return !string.IsNullOrEmpty(publicKey) && pubKey.ToHex() == publicKey;
    }
  }

}
