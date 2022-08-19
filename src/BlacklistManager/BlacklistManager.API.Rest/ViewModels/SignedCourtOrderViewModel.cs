// Copyright (c) 2020 Bitcoin Association

using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Common;

namespace BlacklistManager.API.Rest.ViewModels
{

  // NOTE: we use explicit JsonProperty naming, so that we do not have to configure special serializer for controller. 
  //       Alternative would be for controller to take JSon document as parameter
  public class SignedCourtOrderViewModel
  {
    [Required]
    [JsonProperty("payload")]
    public string Payload { get; set; }

    [Required]
    [JsonProperty("signature")]
    public string Signature { get; set; }

    [Required]
    [JsonProperty("publicKey")]
    public string PublicKey { get; set; }

    [Required]
    [JsonProperty("encoding")]
    public string Encoding { get; set; }

    [Required]
    [JsonProperty("mimetype")]
    [RegularExpression("application/json")]
    public string Mimetype { get; set; }

    [JsonProperty("signatureType")]
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
