// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Actions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class LegalEntityEndpointController : ControllerBase
  {
    private readonly IDomainAction domainAction;

    public LegalEntityEndpointController(
      IDomainAction domainAction)
    {
      this.domainAction = domainAction ?? throw new ArgumentNullException(nameof(domainAction));
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
      var created = await domainAction.CreateLegalEntityEndpointAsync(data.BaseUrl, data.APIKey);

      if (created == null)
      {
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Legal entity endpoint with baseUrl '{data.BaseUrl}' already exists";
        return Conflict(problemDetail);
      }

      return CreatedAtAction(
        "Get",
        new { id = created.LegalEntityEndpointId },
        new LegalEntityEndpointViewModelGet(created));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> PutAsync([FromRoute] int id, LegalEntityEndpointViewModelPut data)
    {
      if (!await domainAction.UpdateLegalEntityEndpointAsync(id, data.BaseUrl, data.APIKey))
      {
        return NotFound();
      }
      return NoContent();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<LegalEntityEndpointViewModelGet>> GetAsync(int id)
    {
      var result = await domainAction.GetLegalEntityEndpointAsync(id);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new LegalEntityEndpointViewModelGet(result));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LegalEntityEndpointViewModelGet>>> GetAsync()
    {
      var result = await domainAction.GetLegalEntityEndpointAsync();
      return Ok(result.Select(x => new LegalEntityEndpointViewModelGet(x)));
    }

    [HttpPost("{id}/disable")]
    public async Task<IActionResult> DisableAsync(int id)
    {
      await domainAction.DisableLegalEntityEndpointAsync(id);
      return NoContent();
    }

    [HttpPost("{id}/enable")]
    public async Task<IActionResult> EnableAsync(int id)
    {
      await domainAction.EnableLegalEntityEndpointAsync(id);
      return NoContent();
    }

    [HttpPost("{id}/reset")]
    public async Task<IActionResult> ResetAsync(int id)
    {
      await domainAction.ResetLegalEntityEndpointAsync(id);
      return NoContent();
    }
  }
}
