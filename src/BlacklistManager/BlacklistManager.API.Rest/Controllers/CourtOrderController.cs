// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Actions;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class CourtOrderController : ControllerBase
  {
    private readonly IDomainAction domainAction;
    private readonly IQueryAction queryAction;

    public CourtOrderController(
      IDomainAction domainAction,
      IQueryAction queryAction)
    {
      this.domainAction = domainAction ?? throw new ArgumentNullException(nameof(domainAction)); 
      this.queryAction = queryAction ?? throw new ArgumentNullException(nameof(queryAction));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CourtOrderQuery>>> GetAsync(bool includeFunds = true)
    {
      var result = await queryAction.QueryCourtOrdersAsync(null, includeFunds);
      return Ok(result);
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<CourtOrderQuery>> GetAsync(string id, bool includeFunds=true)
    {
      var result = await queryAction.QueryCourtOrdersAsync(id, includeFunds);
      if (!result.Any())
      {
        return NotFound();
      }
      return Ok(result.Single());
    }

    [HttpPost]
    public async Task<IActionResult> ProcessCourtOrderAsync(SignedCourtOrderViewModel signedCourtOrder)
    {
      if (string.IsNullOrEmpty(signedCourtOrder.Payload))
      {
        return BadRequest("'signedCourtOrder.payload' cannot be null or empty.");
      }
 
      Domain.Models.CourtOrder domainOrder;

      try
      {
        domainOrder = JsonSerializer
          .Deserialize<CourtOrderViewModelCreate>(signedCourtOrder.Payload, SerializerOptions.SerializeOptions)
          .ToDomainObject(
            SignatureTools.GetSigDoubleHash(signedCourtOrder.Payload, signedCourtOrder.Encoding));
      }
      catch (Exception ex)
      {
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetails.Title = $"Problem parsing Payload element.";
        problemDetails.Detail = ex.Message;
        return BadRequest(problemDetails);
      }

      var result = await domainAction.ProcessSignedCourtOrderAsync(
            signedCourtOrder.ToDomainObject(),
            domainOrder
          );

      if (result.AlreadyImported)
      {
        return Conflict(new ProcessCourtOrderViewModelResult(result));
      }

      return CreatedAtAction(
        "Get",
        new { id = result.CourtOrderHash },
        new ProcessCourtOrderViewModelResult(result));
    }
  }
}