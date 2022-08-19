// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [Authorize]
  public class ConfigController : ControllerBase
  {
    private readonly IConfigurationParamRepository _configParamRepository;

    public ConfigController(
      IConfigurationParamRepository configParamRepository)
    {
      this._configParamRepository = configParamRepository ?? throw new ArgumentNullException(nameof(configParamRepository));
    }

    [HttpPost]
    public async Task<ActionResult<ConfigurationParamViewModel>> PostAsync(ConfigurationParamViewModel data)
    {
      var created = await _configParamRepository.InsertAsync(new ConfigurationParam(data.Key, data.Value));

      if (created == null)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Status = (int)HttpStatusCode.Conflict;
        problemDetail.Title = $"Configuration with parameter key '{data.Key}' already exists";
        return Conflict(problemDetail);
      }

      return CreatedAtAction(
        Consts.HttpMethodNameGET,
        new { paramKey = data.Key},
        new ConfigurationParamViewModel(created));
    }

    [HttpPut("{paramKey}")]
    public async Task<IActionResult> PutAsync([FromRoute] string paramKey, ConfigurationParamViewModel data)
    {
      if (data.Key != paramKey)
      {
        var problemDetail = ProblemDetailsFactory.CreateProblemDetails(HttpContext, (int)HttpStatusCode.BadRequest);
        problemDetail.Title = "The param key specified in URL does not match the one from message body";
        return BadRequest(problemDetail);
      }

      if (!await _configParamRepository.UpdateAsync(new ConfigurationParam(data.Key, data.Value)))
      {
        return NotFound();
      }
      return NoContent();
    }

    [HttpGet("{paramKey}")]
    public async Task<ActionResult<ConfigurationParamViewModel>> GetAsync(string paramKey)
    {
      var result = await _configParamRepository.GetAsync(new ConfigurationParam(paramKey, null));
      if (result == null)
      {
        return NotFound();
      }

      return Ok(new ConfigurationParamViewModel(result));
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConfigurationParamViewModel>>> GetAsync()
    {
      var result = await _configParamRepository.GetAsync();
      return Ok(result.Select(x => new ConfigurationParamViewModel(x)));
    }

    [HttpDelete("{paramKey}")]
    public async Task<IActionResult> DeleteAsync(string paramKey)
    {
      await _configParamRepository.DeleteAsync(new ConfigurationParam(paramKey, null));
      return NoContent();
    }
  }
}
