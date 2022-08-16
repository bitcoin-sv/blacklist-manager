// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Common
{
  public static class Consts
  {
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
