// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.API.Rest.Controllers
{

  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class TrustListController : ControllerBase
  {
    private readonly ILogger<BlackListManagerLogger> logger;
    private readonly ITrustListRepository trustList;

    public TrustListController(
      ILogger<BlackListManagerLogger> logger,
      ITrustListRepository trustList)
    {
      this.trustList = trustList ?? throw new ArgumentNullException(nameof(trustList));
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [HttpPost]
    public ActionResult<TrustListItemViewModelGet> Post(TrustListItemViewModelCreate data)
    {
      var created = trustList.CreatePublicKey(data.Id, data.Trusted ?? true, data.Remarks);

      if (created == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Public key with id '{data.Id}' already exists";
        return Conflict(problemDetail);
      }

      return CreatedAtAction(
        nameof(Get),
        new { publicKey = data.Id },
        new TrustListItemViewModelGet(created));
    }

    [HttpPut("{publicKey}")]
    public IActionResult Put(string publicKey, TrustListItemViewModelCreate data)
    {
      if (!string.IsNullOrEmpty(data.Id) && data.Id != publicKey)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = "The public key specified in URL does not match the one from message body";
        return BadRequest(problemDetail);
      }

      if (!trustList.UpdatePublicKey(data.Id, data.Trusted ?? true, data.Remarks))
      {
        return NotFound();
      }
      return NoContent();
    }

    [HttpGet("{publicKey}")]
    public ActionResult<TrustListItemViewModelGet> Get(string publicKey)
    {
      var result = trustList.GetPublicKey(publicKey);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new TrustListItemViewModelGet(result));
    }

    [HttpGet]
    public ActionResult<IEnumerable<TrustListItemViewModelGet>> Get()
    {
      var result = trustList.GetPublicKeys();
      return Ok(result.Select(x => new TrustListItemViewModelGet(x)));
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteNode(string id)
    {
      trustList.DeletePublicKey(id);
      return NoContent();
    }

  }
}