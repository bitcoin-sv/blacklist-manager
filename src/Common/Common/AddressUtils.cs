// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using NBitcoin;
using NBitcoin.DataEncoders;

namespace Common
{
  public static class AddressUtils
  {
    private static readonly Dictionary<BitcoinNetwork, NBitcoin.Network> networkMap = new Dictionary<BitcoinNetwork, Network>
    {
      {BitcoinNetwork.Mainnet, NBitcoin.Network.Main},
      {BitcoinNetwork.Testnet, NBitcoin.Network.TestNet},
      {BitcoinNetwork.STN, NBitcoin.Network.TestNet},
      {BitcoinNetwork.Regtest, NBitcoin.Network.RegTest}
    };

    public static bool ValidateAddress(string address, BitcoinNetwork network, string blockChainType, out string errorMessage)
    {
      try
      {
        // No other API, we have to catch()

        errorMessage = string.Empty;

        BitcoinAddress bitcoinAddress = BitcoinAddress.Create(address, networkMap[network]);
        byte[] scriptPubKeyBytes = bitcoinAddress.ScriptPubKey.ToBytes();
        bool isScriptType = bitcoinAddress.ScriptPubKey.IsScriptType(ScriptType.Witness);

        if (isScriptType)
        {
          return blockChainType == Consts.BlockChainType.BitcoinCore;
        }

        return true;
      }
      catch (Exception ex)
      {
        errorMessage = ex.Message;
        return false;
      }

    }

    /// <summary>
    /// Given the address, returns scriptPubKey for paying to this address
    /// </summary>
    public static byte[] GetScriptPubKey(string address, BitcoinNetwork network)
    {
      return BitcoinAddress.Create(address, networkMap[network]).ScriptPubKey.ToBytes();
    }

    /// <summary>
    /// Given the address, return "script hash" (sha256 of the script) used for searching for this output
    /// </summary>
    /// <param name="address"></param>
    /// <param name="network"></param>
    /// <returns></returns>
    public static string GetScriptHashHex(string address, BitcoinNetwork network)
    {
      return GetScriptHashHex(GetScriptPubKey(address, network));
    }

    public static string GetScriptHashHex(byte[] bytes)
    {
      var hash = SHA256.Create().ComputeHash(bytes);
      Array.Reverse(hash);
      return Encoders.Hex.EncodeData(hash);
    }

  }
}
