// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
  public class LegalEntityEndpointController : ControllerBase
  {
    private readonly ILegalEndpoints _legalEndpoints;

    public LegalEntityEndpointController(
      ILegalEndpoints legalEndpoints)
    {
      _legalEndpoints = legalEndpoints ?? throw new ArgumentNullException(nameof(legalEndpoints));
    }

    [HttpPost]
    public async Task<ActionResult<LegalEntityEndpointViewModelGet>> PostAsync(LegalEntityEndpointViewModelCreate data)
    {
      var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
      if (data.BaseUrl.EndsWith("/"))
      {
        problemDetail.Title = "The 'baseUrl' parameter must not end with /";
        return BadRequest(problemDetail);
      }
      var created = await _legalEndpoints.CreateAsync(data.BaseUrl, data.APIKey);

      if (created == null)
      {
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Legal entity endpoint with baseUrl '{data.BaseUrl}' already exists";
        return Conflict(problemDetail);
      }

      return CreatedAtAction(
        Consts.HttpMethodNameGET,
        new { id = created.LegalEntityEndpointId },
        new LegalEntityEndpointViewModelGet(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutAsync([FromRoute] int id, LegalEntityEndpointViewModelPut data)
    {
      if (!await _legalEndpoints.UpdateAsync(id, data.BaseUrl, data.APIKey))
      {
        return NotFound();
      }
      return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LegalEntityEndpointViewModelGet>> GetAsync(int id)
    {
      var result = await _legalEndpoints.GetAsync(id);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new LegalEntityEndpointViewModelGet(result));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LegalEntityEndpointViewModelGet>>> GetAsync()
    {
      var result = await _legalEndpoints.GetAsync();
      return Ok(result.Select(x => new LegalEntityEndpointViewModelGet(x)));
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> DisableAsync(int id)
    {
      await _legalEndpoints.DisableAsync(id);
      return NoContent();
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> EnableAsync(int id)
    {
      await _legalEndpoints.EnableAsync(id);
      return NoContent();
    }

    [HttpPost("{id}/reset")]
    public async Task<IActionResult> ResetAsync(int id)
    {
      await _legalEndpoints.ResetAsync(id);
      return NoContent();
    }
  }
}
