// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sodium;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlacklistManager.Infrastructure.Authentication
{
  public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
  {
    private AppSettings appSettings;
    private const string ProblemDetailsContentType = "application/problem+json";

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISystemClock clock,
        IOptions<AppSettings> appSettingsOptions) : base(options, logger, encoder, clock)
    {
      appSettings = appSettingsOptions.Value;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
      return await Task.Run(() =>
      {
        if (!Request.Headers.TryGetValue(Common.Consts.ApiKeyHeaderName, out var apiKeyHeaderValues))
        {
          return AuthenticateResult.NoResult();
        }

        var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
        
        if (Utilities.Compare(Encoding.UTF8.GetBytes(appSettings.REST_APIKey), Encoding.UTF8.GetBytes(providedApiKey)))
        {
          var claims = new List<Claim>
             {
                new Claim(ClaimTypes.NameIdentifier, providedApiKey)
             };

          var identity = new ClaimsIdentity(claims, Options.AuthenticationType);
          var identities = new List<ClaimsIdentity> { identity };
          var principal = new ClaimsPrincipal(identities);
          var ticket = new AuthenticationTicket(principal, Options.Scheme);

          return AuthenticateResult.Success(ticket);
        }

        return AuthenticateResult.Fail("Invalid API Key provided.");
      });
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
      Response.StatusCode = 401;
      Response.ContentType = ProblemDetailsContentType;
      var problemDetails = new { Error = "Unauthorized" };

      await Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
      Response.StatusCode = 403;
      Response.ContentType = ProblemDetailsContentType;
      var problemDetails = new { Error = "Forbidden" };

      await Response.WriteAsync(JsonSerializer.Serialize(problemDetails));
    }
  }
}
