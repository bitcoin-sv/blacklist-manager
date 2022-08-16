// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;

namespace BlacklistManager.Domain.Models
{
  public class ConsensusActivation
  {
    private readonly List<Acceptance> acceptances;

    public ConsensusActivation(
      string signedConsensusActivationJson,
      string courtOrderHash,
      int enforceAtHeight,
      string publicKey,
      string consensusActivationHash)
    {
      SignedConsensusActivationJson = signedConsensusActivationJson;
      CourtOrderHash = courtOrderHash;
      Hash = consensusActivationHash;
      EnforceAtHeight = enforceAtHeight;
      PublicKey = publicKey;

      acceptances = new List<Acceptance>();
    }

    public string SignedConsensusActivationJson { get; private set; }
    public string CourtOrderHash { get; private set; }
    public string Hash { get; private set; }
    public int EnforceAtHeight { get; private set; }
    public string PublicKey { get; private set; }
    public IReadOnlyCollection<Acceptance> Acceptances => acceptances;

    public void AddAcceptance(string signedAcceptanceJson, string publicKey, string courtOrderHash)
    {
      acceptances.Add(new Acceptance(signedAcceptanceJson, publicKey, courtOrderHash));
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
  }
}