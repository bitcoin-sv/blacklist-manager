// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServiceViewModel;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  public class ConsensusActivation
  {
    private readonly List<Acceptance> _acceptances;
    private readonly List<(string TxId, int? EnforceAtHeight, byte[] Body)> _confiscationTimelockedTxs;

    public ConsensusActivation(
      string signedConsensusActivationJson,
      string courtOrderHash,
      int enforceAtHeight,
      string publicKey,
      DateTime signedDate,
      string consensusActivationHash)
    {
      SignedConsensusActivationJson = signedConsensusActivationJson;
      CourtOrderHash = courtOrderHash;
      Hash = consensusActivationHash;
      EnforceAtHeight = enforceAtHeight;
      PublicKey = publicKey;
      SignedDate = signedDate;

      _acceptances = new List<Acceptance>();
      _confiscationTimelockedTxs = new List<(string TxId, int? EnforceAtHeight, byte[] Payload)>();
    }

    public string SignedConsensusActivationJson { get; private set; }
    public string CourtOrderHash { get; private set; }
    public string Hash { get; private set; }
    public int EnforceAtHeight { get; private set; }
    public string PublicKey { get; private set; }
    public DateTime SignedDate { get; private set; }
    public IReadOnlyCollection<Acceptance> Acceptances => _acceptances;
    public IReadOnlyCollection<(string TxId, int? EnforceAtHeight, byte[] Payload)> ConfiscationTimelockedTxs => _confiscationTimelockedTxs;

    public void AddAcceptance(string signedAcceptanceJson, string publicKey, string courtOrderHash)
    {
      _acceptances.Add(new Acceptance(signedAcceptanceJson, publicKey, courtOrderHash));
    }

    public class Acceptance
    {
      public Acceptance(string signedAcceptanceJson, string publicKey, string courtOrderHash)
      {
        SignedAcceptanceJson = signedAcceptanceJson;
        CourtOrderHash = courtOrderHash;
        PublicKey = publicKey;
      }

      public string SignedAcceptanceJson { get; private set; }
      public string CourtOrderHash { get; private set; }
      public string PublicKey { get; private set; }
    }

    public bool PrepareChainedTransactions(IList<ConfiscationTxViewModel> chainedHexTransactions, string network, out string error)
    {
      if (chainedHexTransactions == null || !chainedHexTransactions.Any())
      {
        error = null;
        return true;
      }

      var net = Network.GetNetwork(network);
      foreach (var txViewModel in chainedHexTransactions)
      {
        if (txViewModel == null)
        {
          error = $"Chained transactions list contains empty transaction payload";
          return false;
        }
        else if (Transaction.TryParse(txViewModel.Hex, net, out var tx))
        {
          if (!tx.LockTime.IsHeightLock)
          {
            error = $"Transaction {tx.GetHash()} is not height locked.";
            return false;
          }
          _confiscationTimelockedTxs.Add((tx.GetHash().ToString(), tx.LockTime.Height, tx.ToBytes()));
        }
        else
        {
          error = $"Unable to load transaction with hex {txViewModel.GetHexStartingPart()}";
          return false;
        }
      }

      error = "";
      return true;
    }
  }
}