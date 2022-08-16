// Copyright (c) 2020 Bitcoin Association

using Common;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class SignedPayloadViewModel
  {
    public SignedPayloadViewModel() { }

    public SignedPayloadViewModel(JsonEnvelope jsonEnvelope) 
    {
      Payload = jsonEnvelope.Payload;
      Signature = jsonEnvelope.Signature;
      PublicKey = jsonEnvelope.PublicKey;
      Encoding = jsonEnvelope.Encoding;
      Mimetype = jsonEnvelope.Mimetype;
      SignatureType = jsonEnvelope.SignatureType;
    }

    [Required]
    [JsonPropertyName("payload")]
    public string Payload { get; set; }

    [Required]
    [JsonPropertyName("signature")]
    public string Signature { get; set; }

    [Required]
    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; }

    [Required]
    [JsonPropertyName("encoding")]
    public string Encoding { get; set; }

    [Required]
    [JsonPropertyName("mimetype")]
    [RegularExpression("application/json")]
    public string Mimetype { get; set; }

    [JsonPropertyName("signatureType")]
    public string SignatureType { get; set; }

    public JsonEnvelope ToJsonEnvelope()
    {
      return new JsonEnvelope
      {
        Payload = Payload,
        Signature = Signature,
        PublicKey = PublicKey,
        Encoding = Encoding,
        Mimetype = Mimetype,
        SignatureType = SignatureType
      };
    }
  }
}
