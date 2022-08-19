// Copyright (c) 2020 Bitcoin Association

// https://gist.github.com/nakov/f2a579eb9893b29338b11e063d6f80c2

namespace Common
{

  /// <summary>
  /// Json envelope as defined by "BRFC 6a7a2dec8b17 jsonEnvelope"
  /// C# property names are written in capital names. Use To correctly serialize/deserialize
  /// Use PropertyNamingPolicy = JsonNamingPolicy.CamelCase to correctly serialize/deserialize
  /// </summary>
  public class JsonEnvelope
  {

    /// <summary>
    ///  payload of data being sent
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Type of signature used (bitcoinmessage/bitcoin)
    /// </summary>
    public string SignatureType { get; set; }

    /// <summary>
    /// signature on payload(string)
    /// </summary>
    public string Signature { get; set; }

    /// <summary>
    ///  public key to verify signature
    /// </summary>
    public string PublicKey { get; set; }


    /// <summary>
    /// 	encoding of the payload data
    /// </summary>
    public string Encoding { get; set; }

    /// <summary>
    /// mimetype of the payload data
    /// </summary>
    public string Mimetype { get; set; }

    public static JsonEnvelope ToObject(string json)
    {
      return HelperTools.JSONDeserializeNewtonsoft<JsonEnvelope>(json);
    }

    public string ToJson()
    {
      return HelperTools.JSONSerializeNewtonsoft(this);
    }
  }
}
