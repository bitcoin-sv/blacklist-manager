// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Repositories;
using Common;
using Common.SmartEnums;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public class ConsensusActivationValidator : IConsensusActivationValidator
  {
    private readonly ITrustListRepository trustListRepository;
    private readonly ICourtOrderRepository courtOrderRepository;
    private readonly string origCourtOrderHash;
    private readonly ConsensusActivation consensusActivation;
    private readonly List<string> errors = new List<string>();

    public ConsensusActivationValidator(
      ConsensusActivation ca,
      string origCourtOrderHash,
      ITrustListRepository trustListRepository,
      ICourtOrderRepository courtOrderRepository)
    {
      this.consensusActivation = ca;
      this.origCourtOrderHash = origCourtOrderHash;
      this.trustListRepository = trustListRepository;
      this.courtOrderRepository = courtOrderRepository;
    }

    public IEnumerable<string> Errors => errors;

    public async Task<bool> IsValidAsync()
    {
      if (origCourtOrderHash != consensusActivation.CourtOrderHash)
      {
        errors.Add($"Consensus activation has different court order hash then requested: '{consensusActivation.CourtOrderHash}','{origCourtOrderHash}'");
      }

      if (!trustListRepository.IsPublicKeyTrusted(consensusActivation.PublicKey))
      {
        errors.Add($"Public key '{consensusActivation.PublicKey}' used to sign the consensus activation is not trusted.");
      }
      var jsonEnvelope = HelperTools.JSONDeserializeNewtonsoft<JsonEnvelope>(consensusActivation.SignedConsensusActivationJson);
      if (!SignatureTools.VerifyCourtOrderJsonEnvelope(jsonEnvelope))
      {
        errors.Add("Digital signature applied to consensus activation is invalid");
      }

      // validate acceptances in consensus activation
      int i = 0;
      foreach (var acceptance in consensusActivation.Acceptances)
      {
        if (!SignatureTools.VerifyJsonEnvelope(acceptance.SignedAcceptanceJson))
        {
          errors.Add($"Digital signature applied to consensus activation acceptance [{i}] is invalid");
        }
        if (acceptance.CourtOrderHash != consensusActivation.CourtOrderHash)
        {
          errors.Add($"CourtOrderHash in consensus activation acceptance [{i}] does not match one in consensus activation");
        }
        i++;
      }

      // validate enforceAtHeight
      var cos = await courtOrderRepository.GetCourtOrdersAsync(consensusActivation.CourtOrderHash, false);
      var co = cos.FirstOrDefault();
      if (co?.DocumentType == DocumentType.UnfreezeOrder)
      {
        cos = await courtOrderRepository.GetCourtOrdersAsync(co?.FreezeCourtOrderHash, false);
        var cor = cos.FirstOrDefault();
        if (consensusActivation.EnforceAtHeight <= cor?.EnforceAtHeight)
        {
          errors.Add($"EnforceAtHeight for an unfreeze order must be greater than associated freeze order’s enforceAtHeight: {consensusActivation.EnforceAtHeight}, {cor.EnforceAtHeight}");
        }
      }

      return !errors.Any();
    }
  }
}
