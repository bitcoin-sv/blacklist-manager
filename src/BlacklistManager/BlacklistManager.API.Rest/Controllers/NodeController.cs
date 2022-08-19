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
using Microsoft.Extensions.Logging;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class NodeController : ControllerBase
  {
    private readonly INodes _nodes;

    public NodeController(
      ILogger<BlackListManagerLogger> logger,
      INodes nodes
      )
    {
      this._nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));

    }

    [HttpPost]
    public async Task<ActionResult<NodeViewModelGet>> PostAsync(NodeViewModelCreate data) 
    {
      var created = await _nodes.CreateNodeAsync(data.ToDomainObject());
      if (created == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Node '{data.Id}' already exists"; 
        return Conflict(problemDetail);
      }

      return CreatedAtAction(Consts.HttpMethodNameGET, 
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

      if (! await _nodes.UpdateNodeAsync(data.ToDomainObject()))
      {
        return NotFound();
      }

      return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteNodeAsync(string id)
    {
      await _nodes.DeleteNodeAsync(id);
      return NoContent();
    }


    [HttpGet("{id}")]
    public async Task<ActionResult<NodeViewModelGet>> GetAsync(string id)
    {
      var result = await _nodes.GetNodeAsync(id);
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new NodeViewModelGet(result));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NodeViewModelGet>>> GetAsync()
    {
      var result = await _nodes.GetNodesAsync();
      return Ok(result.Select(x => new NodeViewModelGet(x)));
    }
  }
}