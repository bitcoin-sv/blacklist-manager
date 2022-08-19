// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common
{
  public static class Consts
  {
    public const string ApiKeyHeaderName = "X-Api-Key"; 
    public const int EstimatedBlocksPerHour = 6;
    public const int EstimatedMinutesPerBlock = 10;
    public const string ConfiscationProtocolId = "63667478";
    public const string ConfiscationProtocolVersion = "01";
    public const string MinerIdProtocolPrefix = "ac1eed88";
    
    public const string HttpMethodNameGET = "Get";

    public static class BlockChainType
    {
      public const string BitcoinSV = "BSV";
      public const string BitcoinCash = "BCH";
      public const string BitcoinCore = "BTC";

      public static bool IsValid(string bcType)
      {
        return ValidTypes().Any(x => x == bcType);
      }

      public static IEnumerable<string> ValidTypes()
      {
        var type = typeof(BlockChainType);
        return type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(x => (string)x.GetValue(null));
      }
    }

    public static class JsonSignatureType
    {
      public const string BitcoinMessage = "bitcoinMessage";
      public const string Bitcoin = "bitcoin";

      public static bool IsValid(string signatureType)
      {
        return ValidTypes().Any(x => x == signatureType);
      }

      public static IEnumerable<string> ValidTypes()
      {
        var type = typeof(JsonSignatureType);
        return type.GetFields(BindingFlags.Public | BindingFlags.Static).Select(x => (string)x.GetValue(null));
      }
    }
  }
}
