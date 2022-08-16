// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.Infrastructure.ExternalServices
{
  public class SignedPayload
  {
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

    public string Raw { get; set; }
  }
}
