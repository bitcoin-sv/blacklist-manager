// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.SmartEnums;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Actions
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

      if (!(await trustListRepository.IsPublicKeyTrustedAsync(consensusActivation.PublicKey)))
      {
        errors.Add($"Public key '{consensusActivation.PublicKey}' used to sign the consensus activation is not trusted.");
      }
      var jsonEnvelope = JsonEnvelope.ToObject(consensusActivation.SignedConsensusActivationJson);
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
      var co = cos.SingleOrDefault();
      if (co is null)
      {
        errors.Add($"Court order with hash {consensusActivation.CourtOrderHash} does not exist.");
      }
      else
      {
        if (co.DocumentType == DocumentType.UnfreezeOrder)
        {
          cos = await courtOrderRepository.GetCourtOrdersAsync(co.FreezeCourtOrderHash, false);
          var cor = cos.SingleOrDefault();
          if (cor is null)
          {
            errors.Add($"Freeze court order with hash {co.FreezeCourtOrderHash} does not exist.");

          }
          else if (consensusActivation.EnforceAtHeight <= cor.EnforceAtHeight)
          {
            errors.Add($"EnforceAtHeight for an unfreeze order must be greater than associated freeze order’s enforceAtHeight: {consensusActivation.EnforceAtHeight}, {cor.EnforceAtHeight}");
          }
        }

        if (co.DocumentType == DocumentType.ConfiscationOrder && consensusActivation.ConfiscationTimelockedTxs.Count == 0)
        {
          errors.Add("Consensus activation for court order doesn't contain any chained transactions.");
        }
      }
      return !errors.Any();
    }
  }
}
