// Copyright (c) 2020 Bitcoin Association

namespace Common.BitcoinRpcClient
{
  public static class RpcConsts
  {
    public static class ChainTipStatus
    {
      public const string Invalid = "invalid";
      public const string HeadersOnly = "headers-only";
      public const string ValidHeaders = "valid-headers";
      public const string ValidFork = "valid-fork";
      public const string Active = "active";
    }
  }
}
