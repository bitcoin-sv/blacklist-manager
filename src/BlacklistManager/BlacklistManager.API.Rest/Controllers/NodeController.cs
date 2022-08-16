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
using Microsoft.Extensions.Logging;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class NodeController : ControllerBase
  {

    private readonly ILogger<BlackListManagerLogger> logger;
    private readonly IDomainAction domainAction;


    public NodeController(
      ILogger<BlackListManagerLogger> logger,
      IDomainAction domainAction
      )
    {
      this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
      this.domainAction = domainAction ?? throw new ArgumentNullException(nameof(domainAction));

    }

    [HttpPost]
    public async Task<ActionResult<NodeViewModelGet>> PostAsync(NodeViewModelCreate data) 
    {
      var created = await domainAction.CreateNodeAsync(data.ToDomainObject());
      if (created == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Node '{data.Id}' already exists"; 
        return Conflict(problemDetail);
      }

      return CreatedAtAction(nameof(Get), 
        new { id = created.ToExternalId() },
        new NodeViewModelGet(created));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> PutAsync(string id, NodeViewModelCreate data)
    {
      if (!string.IsNullOrEmpty(data.Id) && data.Id.ToLower() != id.ToLower())
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = "The public id does not match the one from message body";
        return BadRequest(problemDetail);
      }

      if (! await domainAction.UpdateNodeAsync(data.ToDomainObject()))
      {
        return NotFound();
      }

      return NoContent();
    }

    [HttpDelete("{id}")]
    public IActionResult DeleteNode(string id)
    {
      domainAction.DeleteNode(id);
      return NoContent();
    }


    [HttpGet("{id}")]
    public ActionResult<NodeViewModelGet> Get(string id)
    {
      var result = domainAction.GetNode(id);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new NodeViewModelGet(result));
    }

    [HttpGet]
    public ActionResult<IEnumerable<NodeViewModelGet>> Get()
    {
      var result = domainAction.GetNodes();
      return Ok(result.Select(x => new NodeViewModelGet(x)));
    }
  }
}