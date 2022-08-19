// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.Repositories;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlacklistManager.API.Rest.Controllers
{

  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class TrustListController : ControllerBase
  {
    readonly ITrustListRepository _trustListRepository;
    readonly IBackgroundJobs _backgroundJobs;
    readonly ICourtOrderRepository _courtOrderRepository;

    public TrustListController(ITrustListRepository trustListRepository,
                               IBackgroundJobs backgroundJobs,
                               ICourtOrderRepository courtOrderRepository)
    {
      _trustListRepository = trustListRepository ?? throw new ArgumentNullException(nameof(trustListRepository));
      _backgroundJobs = backgroundJobs ?? throw new ArgumentNullException(nameof(backgroundJobs));
      _courtOrderRepository = courtOrderRepository ?? throw new ArgumentNullException(nameof(courtOrderRepository));
    }

    [HttpPost]
    public async Task<ActionResult<TrustListItemViewModelGet>> PostAsync(TrustListItemViewModelCreate data)
    {
      _backgroundJobs.CheckForOfflineMode();
      var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);

      var created = await _trustListRepository.CreatePublicKeyAsync(data.Id, data.Trusted ?? false, data.Remarks);

      if (created == null)
      {
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Public key with id '{data.Id}' already exists";
        return Conflict(problemDetail);
      }

      return CreatedAtAction(
        Consts.HttpMethodNameGET,
        new { publicKey = data.Id },
        new TrustListItemViewModelGet(created));
    }

    [HttpPut("{publicKey}")]
    public async Task<IActionResult> PutAsync(string publicKey, TrustListItemViewModelPut data)
    {
      _backgroundJobs.CheckForOfflineMode();
      var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);

      if (!string.IsNullOrEmpty(data.Id) && data.Id != publicKey)
      {
        problemDetail.Title = "The public key specified in URL does not match the one from message body";
        return BadRequest(problemDetail);
      }

      var existingKey = await _trustListRepository.GetPublicKeyAsync(publicKey);
      if (existingKey == null)
      {
        return NotFound();
      }

      if (data.ReplacedBy != null)
      {
        if (await _courtOrderRepository.GetNumberOfSignedDocumentsAsync(existingKey.ReplacedBy) > 0)
        {
          problemDetail.Title = "Public key cannot be replaced by another key, because the current replacement key already has associated documents.";
          return BadRequest(problemDetail);
        }
        existingKey = await _trustListRepository.GetPublicKeyAsync(data.ReplacedBy);
        if (existingKey == null)
        {
          problemDetail.Title = $"Public key {data.ReplacedBy} does not exist.";
          return BadRequest(problemDetail);
        }

        var keyChain = await _trustListRepository.GetTrustListChainAsync(publicKey);
        if (keyChain.Any(x => x.PublicKey == data.ReplacedBy))
        {
          problemDetail.Title = $"Public key {data.ReplacedBy} is already part of key chain. Key looping is not allowed.";
          return BadRequest(problemDetail);
        }
      }
      await _trustListRepository.UpdatePublicKeyAsync(data.Id, data.Trusted ?? false, data.Remarks, data.ReplacedBy);
      return NoContent();
    }

    [HttpGet("{publicKey}")]
    public async Task<ActionResult<TrustListItemViewModelGet>> GetAsync(string publicKey)
    {
      var result = await _trustListRepository.GetPublicKeyAsync(publicKey);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new TrustListItemViewModelGet(result));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TrustListItemViewModelGet>>> GetAsync()
    {
      var result = await _trustListRepository.GetPublicKeysAsync();
      return Ok(result.Select(x => new TrustListItemViewModelGet(x)));
    }
  }
}