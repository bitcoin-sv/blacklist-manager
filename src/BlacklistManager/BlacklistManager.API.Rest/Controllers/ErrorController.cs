// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net;
using Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.API.Rest.Controllers
{
  [Route("api/v1/[controller]")]
  [ApiController]
  [AllowAnonymous]
  public class ErrorController : ControllerBase
  {
    private ILogger<BlackListManagerLogger> _logger;
    public ErrorController(ILogger<BlackListManagerLogger> logger)
    {
      _logger = logger;
    }

    private Exception ExtractAndLogException(out int statusCode)
    {
      var ex = HttpContext.Features.Get<IExceptionHandlerPathFeature>().Error;

      statusCode = (int)HttpStatusCode.InternalServerError;
      if (ex is HttpResponseException httpResponseException)
      {
        statusCode = httpResponseException.Status;
      }
      if (ex is Npgsql.PostgresException)
      {
        statusCode = (int)HttpStatusCode.BadRequest;
      }

      _logger.LogError("Exception during API execution. Status: {0}. Exception:{1}{2}",
                       statusCode,
                       Environment.NewLine,
                       ex.UnwrapExceptionAsString());

      return ex;
    }


    [Route("/error-local-development")]
    public IActionResult ErrorLocalDevelopment(
    [FromServices] IWebHostEnvironment webHostEnvironment)
    {
      if (webHostEnvironment.EnvironmentName != "Development")
      {
        throw new InvalidOperationException(
            "This shouldn't be invoked in non-development environments.");
      }

      var ex = ExtractAndLogException(out int statusCode);

      return Problem(statusCode: statusCode,
                     detail: ex.UnwrapExceptionAsString(),
                     title: ex.Message);

    }

    [Route("/error")]
    // In non development mode we don't return stack trace
    public IActionResult Error()
    {
      var ex = ExtractAndLogException(out int statusCode);

      return Problem(statusCode: statusCode,
                     detail: ex.GetBaseException().Message,
                     title: ex.Message);
    }
  }
}