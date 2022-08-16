// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Repositories;
using Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public class CourtOrders : ICourtOrders
  {
    private readonly ICourtOrderRepository courtOrderRepository;
    private readonly INodeRepository nodeRepository;
    private readonly IBackgroundJobs backgroundJobs;
    private readonly IFundPropagatorFactory fundPropagatorFactory;
    private readonly ILegalEntityFactory legalEntityServiceFactory;
    private readonly ILogger logger;
    private readonly IConsensusActivationValidatorFactory consensusActivationValidatorFactory;

    public CourtOrders(
      ICourtOrderRepository courtOrderRepository,
      INodeRepository nodeRepository,
      IBackgroundJobs backgroundJobs,
      IFundPropagatorFactory fundPropagatorFactory,
      ILegalEntityFactory legalEntityFactory,
      ILoggerFactory logger,
      IConsensusActivationValidatorFactory consensusActivationValidatorFactory)
    {
      this.courtOrderRepository = courtOrderRepository ?? throw new ArgumentNullException(nameof(courtOrderRepository));
      this.nodeRepository = nodeRepository ?? throw new ArgumentNullException(nameof(nodeRepository));
      this.backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
      this.fundPropagatorFactory = fundPropagatorFactory ?? throw new ArgumentNullException(nameof(fundPropagatorFactory));
      this.legalEntityServiceFactory = legalEntityFactory ?? throw new ArgumentNullException(nameof(legalEntityFactory));
      this.logger = logger.CreateLogger(LogCategories.Domain) ?? throw new ArgumentNullException(nameof(logger));
      this.consensusActivationValidatorFactory = consensusActivationValidatorFactory ?? throw new ArgumentNullException(nameof(consensusActivationValidatorFactory));
    }

    public async Task<bool> ImportCourtOrderAsync(CourtOrder courtOrder, string signedCourtOrder, int? legalEntityEndpointId, bool onSuccessStartBackgroundJobs = true)
    {
      if (!courtOrder.IsValid)
      {
        throw new BadRequestException($"Court order is invalid. The following errors were detected: {string.Join(",", courtOrder.ValidationMessages)}");
      }
      bool inserted = await courtOrderRepository.InsertCourtOrderAsync(courtOrder, signedCourtOrder, legalEntityEndpointId);
      if (inserted)
      {
        logger.LogInformation(LogEvents.CourtOrderStatusChanged, $"Court order '{courtOrder.CourtOrderHash}' imported");
        if (onSuccessStartBackgroundJobs)
        {
          await backgroundJobs.StartProcessCourtOrdersAsync();
          if (legalEntityEndpointId.HasValue) // if court order was automatically downloaded from legal entity endpoint
          {
            await backgroundJobs.StartProcessCourtOrderAcceptancesAsync();
            await backgroundJobs.StartProcessConsensusActivationAsync();
          }
        }
      }
      else
      {
        logger.LogInformation(LogEvents.CourtOrderStatusChanged, $"Court order '{courtOrder.CourtOrderHash}' already imported");
        return false;
      }
      return true;
    }

    public async Task<CourtOrderActivationResult> ActivateCourtOrdersAsync(CancellationToken cancellationToken)
    {
      try
      {
        var result = new CourtOrderActivationResult();
        var courtOrdersToActivate = await courtOrderRepository.GetCourtOrdersToActivateAsync();
        if (courtOrdersToActivate.Any())
        {
          logger.LogDebug($"Starting activation for {courtOrdersToActivate.Count()} court orders");
          bool activated;
          foreach (var courtOrderHash in courtOrdersToActivate)
          {
            if (cancellationToken.IsCancellationRequested)
            {
              logger.LogDebug("Activation of court orders canceled");
              break;
            }
            activated = await ActivateCourtOrderAsync(courtOrderHash);
            if (activated)
            {
              result.AddActivated(courtOrderHash);
            }
          }
          logger.LogInformation($"Activation of court orders ended. {result.ActivatedCourtOrders.Count()}/{courtOrdersToActivate.Count()} court orders activated successfully");
        }
        return result;
      }
      catch (Exception ex)
      {
        logger.LogError(LogEvents.CourtOrderActivation, ex, $"Activation of court orders aborted with exception");
        return new CourtOrderActivationResult(internalError: true);
      }
    }

    public async Task<bool> ActivateCourtOrderAsync(string courtOrderHash)
    {
      try
      {
        var cos = await courtOrderRepository.GetCourtOrdersAsync(courtOrderHash, false);
        if (cos.Any())
        {
          await ActivateCourtOrderAsync(cos.First());
          return true;
        }
        return false;
      }
      catch (Exception ex)
      {
        logger.LogError(LogEvents.CourtOrderActivation, ex, $"Activation of court order '{courtOrderHash}' aborted with exception");
        return false;
      }
    }

    public async Task ActivateCourtOrderAsync(CourtOrder courtOrder)
    {
      await SetCourtOrderStatusAsync(courtOrder, courtOrder.GetActiveStatus(), null);
    }

    /// <summary>
    /// Update nodes with latest funds states
    /// </summary>    
    public async Task<FundPropagationResult> PropagateFundsStateAsync(CancellationToken cancellationToken)
    {
      try
      {
        var result = new FundPropagationResult();
        var fundStateToPropagate = await courtOrderRepository.GetFundStateToPropagateAsync();
        if (fundStateToPropagate.Any())
        {
          logger.LogDebug($"Starting propagation for {fundStateToPropagate.Count()} fund states");
          var nodes = nodeRepository.GetNodes();
          result = await PropagateFundStateAsync(fundStateToPropagate, nodes, cancellationToken);
          PersistPropagationResult(result);
          logger.LogInformation(LogEvents.FundStatePropagation, $"Propagation of fund states ended. {result.PropagatedFunds.Count()}/{fundStateToPropagate.Count()} fund states propagated successfully");
        }
        else
        {
          logger.LogInformation(LogEvents.FundStatePropagation, $"No fund states to propagate");
        }
        return result;
      }
      catch (Exception ex)
      {
        logger.LogError(LogEvents.FundStatePropagation, ex, $"Propagation of fund states aborted with exception");
        return new FundPropagationResult(internalError: true);
      }
    }

    private void PersistPropagationResult(FundPropagationResult propagationResult)
    {
      if (propagationResult.PropagatedFunds.Any())
      {
        courtOrderRepository.InsertFundStateNode(propagationResult.PropagatedFunds);
      }

      // Update state for failed and recovered nodes:
      foreach (var node in propagationResult.NodesWithError.Concat(propagationResult.RecoveredNodes))
      {
        nodeRepository.UpdateNodeError(node);
      }
      logger.LogDebug("Result for propagation of fund states persisted to database");
    }

    private async Task<FundPropagationResult> PropagateFundStateAsync(
      IEnumerable<FundStateToPropagate> fundStatesToPropagate,
      IEnumerable<Node> nodes,
      CancellationToken cancellationToken)
    {
      var propagator = fundPropagatorFactory.Create(nodes, cancellationToken);
      var result = await propagator.PropagateAsync(fundStatesToPropagate);
      return result;
    }

    private async Task SetCourtOrderStatusConsensusAsync(string courtOrderHash, CourtOrderType courtOrderType, int enforceAtHeight)
    {
      var status = CourtOrderStatus.FreezeConsensus;
      if (courtOrderType == CourtOrderType.Unfreeze)
      {
        status = CourtOrderStatus.UnfreezeConsensus;
      }
      await SetCourtOrderStatusAsync(courtOrderHash, status, enforceAtHeight);
    }

    public async Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus status, int? enforceAtHeight)
    {
      var cos = await courtOrderRepository.GetCourtOrdersAsync(courtOrderHash, false);
      if (cos.Any())
      {
        await SetCourtOrderStatusAsync(cos.First(), status, enforceAtHeight);
      }
      else
      {
        throw new BadRequestException($"Court order '{courtOrderHash}' not found in database");
      }
    }

    private async Task SetCourtOrderStatusAsync(CourtOrder courtOrder, CourtOrderStatus newStatus, int? enforceAtHeight)
    {
      if (courtOrder.Status != newStatus)
      {
        if (!courtOrder.IsStatusChangeValid(newStatus))
        {
          throw new BadRequestException($"Can not change court order '{courtOrder.CourtOrderHash}' status from '{courtOrder.Status}' to  '{newStatus}'");
        }
        await courtOrderRepository.SetCourtOrderStatusAsync(courtOrder.CourtOrderHash, newStatus, enforceAtHeight);
        logger.LogInformation(LogEvents.CourtOrderStatusChanged, $"Court order '{courtOrder.CourtOrderHash}' status changed to '{newStatus}'");
        await backgroundJobs.StartPropagateFundsStatesAsync();
      }
    }

    /// <summary>
    /// Process/fetch consensus activations and report if any pending consensus activations remain
    /// </summary>
    public async Task<ProcessConsensusActivationResult> ProcessConsensusActivationsAsync(CancellationToken cancellationToken)
    {
      try
      {
        var result = new ProcessConsensusActivationResult();
        
        var pendingConsensusActivations = await courtOrderRepository.GetPendingConsensusActivationsAsync();
        
        if (pendingConsensusActivations.Any())
        {
          logger.LogDebug($"Starting processing of {pendingConsensusActivations.Count()} consensus activations");

          foreach (var pda in pendingConsensusActivations)
          {
            try
            {
              // download
              var legalEntityService = legalEntityServiceFactory.Create(pda.LegalEntityEndpointUrl, pda.LegalEntityEndpointApiKey);
              var consensusActivation = await legalEntityService.GetConsensusActivationAsync(pda.CourtOrderHash);

              if (consensusActivation == null)
              {
                result.SetConsensusActivationsPending();
                logger.LogInformation($"No consensus activation yet for '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}'");
                continue;
              }

              // validate
              var caValidator = consensusActivationValidatorFactory.Create(consensusActivation, pda.CourtOrderHash);
              bool isCaValid = await caValidator.IsValidAsync();

              // process
              if (isCaValid)
              {
                await SetCourtOrderStatusConsensusAsync(pda.CourtOrderHash, (CourtOrderType)pda.CourtOrderTypeId, consensusActivation.EnforceAtHeight);
              }

              // insert into database
              await courtOrderRepository.InsertConsensusActivationAsync(consensusActivation, pda.InternalCourtOrderId, pda.LegalEntityEndpointId, isCaValid);

              // edge case remark: only first consensus activation is processed in case of many consensusActivations for same courtOrder (signed) from different legal entities endpoints connected to same blacklist manager

              if (isCaValid)
              {
                result.Processed++;
                logger.LogInformation(
                  $"Processed consensus activation '{consensusActivation.Hash}' for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}'");
              }
              else
              {
                result.SetConsensusActivationsPending();
                logger.LogWarning(
                  $"Consensus activation '{consensusActivation.Hash}' for court order '{pda.CourtOrderHash}'" +
                  $"and '{pda.LegalEntityEndpointUrl}' is invalid: {string.Join(",", caValidator.Errors)}");
              }
            }
            catch (HttpRequestException ex)
            {
              result.SetConsensusActivationsPending();
              await courtOrderRepository.UpdateLegalEntityEndpointErrorAsync(pda.LegalEntityEndpointId, "Error requesting consensus activation");
              logger.LogError(LogEvents.ConsensusActivation, ex,
                $"Requesting of consensus activation for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}' aborted with exception");
            }
            catch (ValidationException ex)
            {
              result.SetConsensusActivationsPending();
              await courtOrderRepository.UpdateLegalEntityEndpointErrorAsync(pda.LegalEntityEndpointId, "Received invalid consensus activation");
              logger.LogError(LogEvents.ConsensusActivation, ex,
                $"Consensus activation for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}' is invalid");
            }
            catch (Exception ex)
            {
              result.SetConsensusActivationsPending();
              result.SetInternalError();
              await courtOrderRepository.UpdateLegalEntityEndpointErrorAsync(pda.LegalEntityEndpointId, "Error processing consensus activation");
              logger.LogError(LogEvents.ConsensusActivation, ex,
                $"Processing of consensus activation for court order '{pda.CourtOrderHash}' and '{pda.LegalEntityEndpointUrl}' aborted with exception");
            }
          }

          logger.LogInformation($"Processing of consensus activations ended. {result.Processed}/{pendingConsensusActivations.Count()} processed successfully");
        }
        return result;
      }
      catch (Exception ex)
      {
        logger.LogError(LogEvents.ConsensusActivation, ex, $"Processing of consensus activations aborted with exception");
        return new ProcessConsensusActivationResult(internalError: true);
      }
    }

    public async Task<CheckReferencedCourtOrderResult> CheckReferencedCourtOrderAsync(CourtOrder courtOrder)
    {
      if (!string.IsNullOrEmpty(courtOrder.FreezeCourtOrderHash))
      {
        var freezeCourtOrders = await courtOrderRepository.GetCourtOrdersAsync(courtOrder.FreezeCourtOrderHash, true);
        if (freezeCourtOrders.Any())
        {
          if (freezeCourtOrders.Count() != 1)
          {
            throw new BadRequestException($"Court order '{courtOrder.CourtOrderHash}' references {freezeCourtOrders.Count()} freeze court orders '{courtOrder.FreezeCourtOrderHash}'");
          }
          var freezeCourtOrder = freezeCourtOrders.First();
          var unreferenceFunds = courtOrder.Funds.Except(freezeCourtOrder.Funds, new FundEqualityComparerByTxOut()).ToList();
          return CheckReferencedCourtOrderResult.CreateUnreferencedFundsResult(unreferenceFunds);
        }
        return CheckReferencedCourtOrderResult.CreateNoReferencedCourtOrderResult();
      }
      return CheckReferencedCourtOrderResult.CreatePassResult();
    }
  }
}
