// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Common.SmartEnums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBitcoin;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class CourtOrderController : ControllerBase
  {
    readonly ICourtOrderRepository _courtOrderRepository;
    readonly ICourtOrders _courtOrders;
    readonly AppSettings _appSettings;
    readonly ILogger<CourtOrderController> _logger;
    readonly IBackgroundJobs _backgroundJobs;
    readonly IFundPropagator _fundPropagator;

    public CourtOrderController(
      ICourtOrderRepository courtOrderRepository,
      ICourtOrders courtOrders,
      IBackgroundJobs backgroundJobs,
      IFundPropagator fundPropagator,
      ILogger<CourtOrderController> logger,
      IOptions<AppSettings> options)
    {
      _courtOrders = courtOrders ?? throw new ArgumentNullException(nameof(courtOrders));
      _courtOrderRepository = courtOrderRepository ?? throw new ArgumentNullException(nameof(courtOrderRepository));
      _backgroundJobs = backgroundJobs;
      _fundPropagator = fundPropagator;
      _logger = logger;
      _appSettings = options.Value;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourtOrderQueryViewModel>>> GetAsync(bool includeFunds = true)
    {
      var result = await _courtOrderRepository.QueryCourtOrdersAsync(null, includeFunds);
      return Ok(result.Select(x => new CourtOrderQueryViewModel(x)));
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<CourtOrderQueryViewModel>> GetAsync(string id, bool includeFunds=true)
    {
      var result = await _courtOrderRepository.QueryCourtOrdersAsync(id, includeFunds);
      if (!result.Any())
      {
        return NotFound();
      }
      return Ok(new CourtOrderQueryViewModel(result.Single()));
    }

    [HttpPost]
    public async Task<IActionResult> ProcessCourtOrderAsync(SignedCourtOrderViewModel signedCourtOrder)
    {
      if (string.IsNullOrEmpty(signedCourtOrder.Payload))
      {
        return BadRequest("'signedCourtOrder.payload' cannot be null or empty.");
      }

      var problemDetails = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
      ProcessCourtOrderResult result;
      var coEnvelopeVM = JsonSerializer.Deserialize<ConfiscationEnvelopeViewModel>(signedCourtOrder.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);
      var coVM = JsonSerializer.Deserialize<CourtOrderViewModelCreate>(signedCourtOrder.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);
      var cancellationVM = JsonSerializer.Deserialize<CancelConfiscationOrderViewModel>(signedCourtOrder.Payload, SerializerOptions.SerializeOptionsNoPrettyPrint);

      var coHash = coEnvelopeVM?.ConfiscationTxDocument?.ConfiscationCourtOrderHash ?? coVM?.CourtOrderHash;
      try
      {
        if (coEnvelopeVM.DocumentType == DocumentType.ConfiscationEnvelope)
        {
          var domainConfiscation = coEnvelopeVM.ToDomainObject();
          result = await _courtOrders.ProcessSignedCourtOrderAsync(signedCourtOrder.ToDomainObject(), domainConfiscation);
        }
        else if (coEnvelopeVM.DocumentType == DocumentType.CancelConfiscationOrder)
        {
          result = await _courtOrders.CancelConfiscationOrderAsync(signedCourtOrder.ToDomainObject(), cancellationVM.ConfiscationOrderHash);
          if (result.Errors == null)
          {
            return NoContent();
          }
        }
        else
        {
          var domainOrder = coVM.ToDomainObject(
            SignatureTools.GetSigDoubleHash(signedCourtOrder.Payload, signedCourtOrder.Encoding), Network.GetNetwork(_appSettings.BitcoinNetwork));
          result = await _courtOrders.ProcessSignedCourtOrderAsync(signedCourtOrder.ToDomainObject(), domainOrder);
        }
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error while importing courOrder");
        problemDetails.Title = "Problem parsing Payload element.";
        problemDetails.Detail = ex.Message;
        return BadRequest(problemDetails);
      }

      if (result.AlreadyImported)
      {
        return Conflict(new ProcessCourtOrderViewModelResult(result));
      }
      if (result.Errors is not null && result.Errors.Any())
      {
        _logger.LogError("Error while importing courOrder, {error}", result.Errors.First());
        problemDetails.Title = "Problem verifying and importing court order.";
        problemDetails.Detail = result.Errors.First();
        return BadRequest(problemDetails);
      }

      var activationResult = await _courtOrders.ActivateCourtOrdersAsync(CancellationToken.None);
      if (!activationResult.WasSuccessful)
      {
        _logger.LogError("Error while activating court order for {coHash}", coHash);
        problemDetails.Title = "Error while activating court order";
        problemDetails.Detail = "Error while trying to activate court order, will retry in a background job.";
        await _backgroundJobs.StartProcessCourtOrdersAsync();
        return BadRequest(problemDetails);
      }

      var propagationResult = await _fundPropagator.PropagateFundsStateAsync(CancellationToken.None);
      if (!propagationResult.WasSuccessful)
      {
        _logger.LogError("Error while propagating funds for {coHash}", coHash);
        problemDetails.Title = "Error while propagating funds";
        problemDetails.Detail = "Error while trying to propagate funds, will retry in a background job.";
        await _backgroundJobs.StartPropagateFundsStatesAsync();
        return BadRequest(problemDetails);
      }

      // Court order successfully imported re-start services for further processing
      await _backgroundJobs.StartProcessCourtOrderAcceptancesAsync();

      return CreatedAtAction(
        Consts.HttpMethodNameGET,
        new { id = result.CourtOrderHash },
        new ProcessCourtOrderViewModelResult(result));
    }

    [HttpPost("retryFailed")]
    public async Task<ActionResult> RetryFailedOrdersAsync()
    {
      await _backgroundJobs.StartFailedCourtOrdersProcessingAsync();

      return Ok();
    }
  }
}