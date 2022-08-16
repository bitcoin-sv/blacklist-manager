// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Test.Functional.Server
{
  public class BlacklistManagerServer : TestServerBase
  {
    public const string ApiCourtOrderUrl = "/api/v1/courtOrder";
    public const string ApiTxOutUrl = "/api/v1/txOut";
    public const string ApiConfigurationParamUrl = "/api/v1/config";
    public const string ApiTrustListUrl = "/api/v1/TrustList";
    public const string ApiNodeUrl = "/api/v1/node";
    public const string ApiLegalEntityEndpointUrl = "/api/v1/legalEntityEndpoint";
    public const string ApiSigningKeyEndpointUrl = "/api/v1/signingKey";

    public static class Get
    {
      public static string GetCourtOrder(string id, bool includeFunds)
      {
        var url = ApiCourtOrderUrl;
        if (!string.IsNullOrEmpty(id))
        {
          url += $"/{id}";
        }
        url += $"?includeFunds={includeFunds}";
        return url;
      }

      public static string GetTxOut(string id, long vout)
      {
        return $"{ApiTxOutUrl}/{id}/{vout}";
      }
    }

    public static class Post
    {
      public static string ProcessCourtOrder = $"{ApiCourtOrderUrl}";
    }
  }
}
