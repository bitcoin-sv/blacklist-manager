// Copyright (c) 2020 Bitcoin Association

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin.DataEncoders;

namespace Common.Test.Unit
{
  [TestClass]
  public class AddressUtilsTest
  {
    // https://whatsonchain.com/tx/c08060314aec970d7f36a2750681707e6535a726e4d7f959ade616c2e7ff0f9c
    private string P2PKHAddress = "13DGMH6qUT9eE46P2iUqPbW8tzwbr4yGvf";
    private string P2PKHScriptPubKey = "76a91418420e6b12b99938dbd9e310c277b894a5a2d98b88ac"; // script corresponding to the address above


    // https://whatsonchain.com/tx/ccfa6089cae9533bbceddbd9da3e7250bca15e08e2988eca7eded5656d10a8cc
    private string P2SHAddress = "3MLTuWjNzgfQidwyEZ7X6MjMa31XKZiqat"; 
    private string P2SHScriptPubKey = "a914d77f7827b637c4a45bb81553ad91ccddbff11ec587";

    private string Bench32Adddress = "bc1qar0srrr7xfkvy5l643lydnw9re59gtzzwf5mdq";

    [TestMethod]
    public void TestValidateAddress()
    {
      Assert.IsTrue(AddressUtils.ValidateAddress(P2PKHAddress, BitcoinNetwork.Mainnet, "BSV", out _));
      Assert.IsTrue(AddressUtils.ValidateAddress(P2SHAddress, BitcoinNetwork.Mainnet, "BSV", out _));
      Assert.IsTrue(AddressUtils.ValidateAddress(Bench32Adddress, BitcoinNetwork.Mainnet, "BTC", out _));

      // Test with invalid address:
      var sb = new StringBuilder(P2PKHAddress);
      sb[2] = 'a';
      
      var P2PKHAddressInvalid = sb.ToString();
      Assert.AreNotEqual(P2PKHAddress, P2PKHAddressInvalid);

      Assert.IsFalse(AddressUtils.ValidateAddress(P2PKHAddressInvalid, BitcoinNetwork.Mainnet, "BSV", out _));

      // Test with wrong network
      Assert.IsFalse(AddressUtils.ValidateAddress(P2PKHAddress, BitcoinNetwork.Testnet, "BSV", out _));
      Assert.IsFalse(AddressUtils.ValidateAddress(P2SHAddress, BitcoinNetwork.Testnet, "BSV", out _));
      Assert.IsFalse(AddressUtils.ValidateAddress(Bench32Adddress, BitcoinNetwork.Testnet, "BSV", out _));

      // Test with wrong blockchain
      Assert.IsFalse(AddressUtils.ValidateAddress(Bench32Adddress, BitcoinNetwork.Mainnet, "BSV", out _));
    }

    [TestMethod]
    public void TestGetScriptPubKey()
    {

      Assert.AreEqual(P2PKHScriptPubKey,
        Encoders.Hex.EncodeData(AddressUtils.GetScriptPubKey(P2PKHAddress, BitcoinNetwork.Mainnet)));
      
      Assert.AreEqual(P2SHScriptPubKey,
        Encoders.Hex.EncodeData(AddressUtils.GetScriptPubKey(P2SHAddress, BitcoinNetwork.Mainnet)));
    }

  }
}
  