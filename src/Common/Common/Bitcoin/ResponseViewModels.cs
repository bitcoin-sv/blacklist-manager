// Copyright (c) 2020 Bitcoin Association

using NBitcoin;
using System;
using System.Collections.Generic;

namespace Common.Bitcoin
{
  public enum SendTransactionErrorCode
  {
    AlreadyKnown = 1000,
    Evicted = 1001,
    MissingInputs = 1002
  }

  public partial class GetBlock
  {
    public List<TransactionItem> Tx { get; set; }

    public string Hash { get; set; }

    public long Confirmations { get; set; }

    public long Size { get; set; }

    public long Height { get; set; }

    public long Version { get; set; }

    public string VersionHex { get; set; }

    public string Merkleroot { get; set; }

    public long NumTx { get; set; }

    public long Time { get; set; }

    public long MedianTime { get; set; }

    public long Nonce { get; set; }

    public string Bits { get; set; }

    public double Difficulty { get; set; }

    public string Chainwork { get; set; }

    public string PreviousBlockHash { get; set; }

    public string NextBlockHash { get; set; }
  }

  [Serializable]
  public class TransactionItem
  {
    public string Blockhash { get; set; }
    public long? BlockHeight { get; set; }
    public long? Time { get; set; }
    public string TxId { get; set; }
    public List<VInItem> VIn { get; set; }
    public List<VOutItem> Vout { get; set; }
  }

  [Serializable]
  public class VInItem
  {
    public string Coinbase { get; set; }
    public ScriptSigItem ScriptSig { get; set; }
    public string TxId { get; set; }
    public long VOut { get; set; }

  }

  [Serializable]
  public class ScriptSigItem
  {
    public string Asm { get; set; }
    public string Hex { get; set; }
  }

  [Serializable]
  public class VOutItem
  {
    public long n { get; set; }
    public string ScriptPubKeyHash { get; set; }
    public decimal Value { get; set; }
    public ScriptPubKey ScriptPubKey { get; set; }
  }

  [Serializable]
  public partial class ScriptPubKey
  {
    public string Asm { get; set; }
    public string Hex { get; set; }
    public long ReqSigs { get; set; }
    public string Type { get; set; }
    public string[] Addresses { get; set; }
  }

  [Serializable]
  public partial class BlockHeaderItem
  {
    public uint256 Hash { get; set; }

    //public long Confirmations { get; set; }

    public long Height { get; set; }

    //public long Version { get; set; }

    //public string VersionHex { get; set; }

    //public string Merkleroot { get; set; }

    //public long NumTx { get; set; }

    public long Time { get; set; }

    //public long Mediantime { get; set; }

    //public long Nonce { get; set; }

    //public string Bits { get; set; }

    //public double Difficulty { get; set; }

    //public string Chainwork { get; set; }

    public string Previousblockhash { get; set; }

    public long GetObjSize()
    {
      int objSize = 200;//24 bytes for overhead + 2*8 bytes for long + 32bytes for uint256 + 128 for Previousblockhash as string
      return objSize;
    }
  }

  [Serializable]
  public class GetChainTipsItem
  {
    public long Height { get; set; }

    public string Hash { get; set; }

    public long BranchLen { get; set; }

    public string Status { get; set; }
  }

  public class SendRawTransactionsResult
  {
    public string TxId { get; set; }

    public int? SubmittedAtHeight { get; set; }

    public int? ErrorCode { get; set; }

    public string ErrorDescription { get; set; }

    public int? ErrorAtHeight { get; set; }
  }
}
