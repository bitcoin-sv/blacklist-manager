// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Common;

namespace BlacklistManager.API.Rest.ViewModels
{

  // NOTE: we use explicit JsonProperty naming, so that we do not have to configure special serializer for controller. 
  //       Alternative would be for controller to take JSon document as parameter
  public class SignedCourtOrderViewModel
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

    public JsonEnvelope ToDomainObject()
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
