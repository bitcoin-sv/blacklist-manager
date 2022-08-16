// Copyright (c) 2020 Bitcoin Association

using System.Net.Http;
using System.Reflection;
using BlacklistManager.API.Rest.Database;
using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Authentication;
using BlacklistManager.Infrastructure.ExternalServices;
using BlacklistManager.Infrastructure.Repositories;
using Common;
using Common.BitcoinRpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace BlacklistManager.API.Rest
{
  public class Startup
  {
    IWebHostEnvironment HostEnvironment { get; set; }
    public Startup(IConfiguration configuration, IWebHostEnvironment hostEnvironment)
    {
      Configuration = configuration;
      HostEnvironment = hostEnvironment;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public virtual void ConfigureServices(IServiceCollection services)
    {
      services.AddOptions<AppSettings>()
        .Bind(Configuration.GetSection("AppSettings"))
        .ValidateDataAnnotations();

      string blackListManagerConnectionString = Configuration["BlacklistManagerConnectionStrings:DBConnectionString"];

      services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
        options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
        options.AddScheme(ApiKeyAuthenticationOptions.DefaultScheme, a => a.HandlerType = typeof(ApiKeyAuthenticationHandler));
      });

      // time in database is UTC so it is automatically mapped to Kind=UTC
      Dapper.SqlMapper.AddTypeHandler(new Common.DateTimeHandler());

      services.AddControllers().AddJsonOptions(options => { options.JsonSerializerOptions.WriteIndented = true;});
      
      services.AddTransient<IStartupChecker, StartupChecker>();
      services.AddTransient<ICreateDB, CreateBlacklistManagerDB>();
      services.AddTransient<IDomainAction, DomainAction>();
      services.AddTransient<IQueryAction>(x => new QueryAction(blackListManagerConnectionString));
      services.AddTransient<ICourtOrderRepository, CourtOrderRepositoryPostgres>(sp => 
        new CourtOrderRepositoryPostgres(
          blackListManagerConnectionString, 
          sp.GetRequiredService<ILoggerFactory>()));
      services.AddTransient<ITrustListRepository>( x => new TrustListRepositoryPostgres(blackListManagerConnectionString));
      services.AddTransient<INodeRepository>(x => new NodeRepositoryPostgres(blackListManagerConnectionString));
      services.AddTransient<ILegalEntityRepository>(x => new LegalEntityRepositoryPostgres(blackListManagerConnectionString));
      services.AddTransient<IDelegatedKeyRepositoryPostgres>(x => new DelegatedKeyRepositoryPostgres(blackListManagerConnectionString, x.GetRequiredService<ILoggerFactory>()));
      services.AddTransient<ICourtOrders, CourtOrders>();
      services.AddTransient<INodes, Nodes>();
      services.AddTransient<ILegalEndpoints, LegalEndpoints>();
      services.AddTransient<IDelegatedKeys, DelegatedKeys>();
      services.AddTransient<IFundPropagatorFactory, FundPropagatorFactory>();
      services.AddTransient<ILongWait, LongWait>();
      services.AddTransient<IConfigurationParamRepository>(x => new ConfigurationParamRepositoryPostgres(blackListManagerConnectionString));

      services.AddTransient<IConfigurationParams, ConfigurationParams>();

      if (HostEnvironment.EnvironmentName != "Testing")
      {
        services.AddTransient<IBitcoindFactory, BitcoindFactory>();
        services.AddTransient<ILegalEntityFactory, LegalEntityFactory>();
        services.AddTransient<IConsensusActivationValidatorFactory, ConsensusActivationValidatorFactory>();
        services.AddSingleton<IBackgroundJobs, BackgroundJobs>();
      }

      services.AddHostedService<BackgroundJobStarter>();

      services.AddSingleton<IBlacklistHttpClientFactory, BlacklistHttpClientFactoryDefault>();
      services.AddSingleton<IBitcoinRpcHttpClientFactory, BitcoinRpcHttpClientFactoryDefault>();

      services.AddHttpClient(BlacklistHttpClientFactoryDefault.ClientName)
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
          UseCookies =
            false, // Disable cookies they are not needed and we do not want to leak them - https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-3.1#cookies
          AllowAutoRedirect = false
        });

    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, 
                          IWebHostEnvironment env, 
                          IHostApplicationLifetime lifetime,
                          IDomainAction domainAction,
                          ILogger<BlackListManagerLogger> logger)
    {
      if (env.IsDevelopment())
      {
        app.UseExceptionHandler("/error-local-development");
      }
      else
      {
        app.UseExceptionHandler("/error");
      }

      app.Use(async (context, next) =>
      {
        context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate, max-age=0");
        context.Response.Headers.Add("Expires", "-1");
        context.Response.Headers.Add("Pragma", "no-cache");

        context.Response.Headers.Add("Content-Security-Policy", "frame-ancestors 'none'");
        context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Add("X-Frame-Options", "DENY");
        context.Response.Headers.Add("Strict-Transport-Security", "max-age=63072000; includeSubDomains; preload");
        await next();
      });

      app.UseRouting();

      app.UseAuthentication();
      app.UseAuthorization();

      lifetime.ApplicationStarted.Register(() => { OnApplicationStarted(domainAction, Configuration); });

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
      });

      logger.LogInformation("Version: " + Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "ApplicationStarted.Register() doesn't support async methods")]
    private void OnApplicationStarted(IDomainAction domainAction, IConfiguration configuration)
    {
      domainAction.CreateInitialSignerKeyAsync(Network.GetNetwork(configuration["AppSettings:BitcoinNetwork"])).GetAwaiter().GetResult();
    }
  }
}
