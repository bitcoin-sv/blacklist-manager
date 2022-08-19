// Copyright (c) 2020 Bitcoin Association

using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using BlacklistManager.API.Rest.Database;
using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Actions;
using BlacklistManager.Infrastructure.Authentication;
using BlacklistManager.Infrastructure.BackgroundJobs;
using BlacklistManager.Infrastructure.ExternalServices;
using BlacklistManager.Infrastructure.Repositories;
using Common.Bitcoin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBitcoin;
using OpenTelemetry.Metrics;

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
      services.AddOptions();

      services.AddAuthentication(options =>
      {
        options.DefaultAuthenticateScheme = ApiKeyAuthenticationOptions.DefaultScheme;
        options.DefaultChallengeScheme = ApiKeyAuthenticationOptions.DefaultScheme;
        options.AddScheme(ApiKeyAuthenticationOptions.DefaultScheme, a => a.HandlerType = typeof(ApiKeyAuthenticationHandler));
      });

      // time in database is UTC so it is automatically mapped to Kind=UTC
      Dapper.SqlMapper.AddTypeHandler(new Common.DateTimeHandler());

      services.AddLogging(logging =>
      {
        logging.AddSimpleConsole(conf =>
        {
          conf.TimestampFormat = "yyyy.MM.dd HH:mm:ss:fff ";
          conf.UseUtcTimestamp = true;
          conf.ColorBehavior = Microsoft.Extensions.Logging.Console.LoggerColorBehavior.Default;
          conf.IncludeScopes = false;
        });
      });

      services.AddControllers().AddJsonOptions(options => { options.JsonSerializerOptions.WriteIndented = true;});
      
      services.AddTransient<IStartupChecker, StartupChecker>();
      services.AddTransient<ICreateDB, CreateBlacklistManagerDB>();
      services.AddTransient<ICourtOrderRepository, CourtOrderRepositoryPostgres>();
      services.AddTransient<ITrustListRepository, TrustListRepositoryPostgres>();
      services.AddTransient<INodeRepository, NodeRepositoryPostgres>();
      services.AddTransient<ILegalEntityRepository, LegalEntityRepositoryPostgres>();
      services.AddTransient<IDelegatedKeyRepositoryPostgres, DelegatedKeyRepositoryPostgres>();
      services.AddTransient<ICourtOrders, CourtOrders>();
      services.AddTransient<IFundPropagator, FundPropagator>();
      services.AddTransient<INodes, Nodes>();
      services.AddTransient<ILegalEndpoints, LegalEndpoints>();
      services.AddTransient<IDelegatedKeys, DelegatedKeys>();
      services.AddTransient<ILongWait, LongWait>();
      services.AddTransient<IConfigurationParamRepository, ConfigurationParamRepositoryPostgres>();
      services.AddTransient<IConfiscationTxProcessing, ConfiscationTxProcessing>();

      services.AddTransient<IConfigurationParams, ConfigurationParams>();

      if (HostEnvironment.EnvironmentName != "Testing")
      {
        services.AddTransient<IBitcoinFactory, BitcoinFactory>(sp => 
        {
          return new BitcoinFactory(Configuration["AppSettings:BlockChain"], Network.GetNetwork(Configuration["AppSettings:BitcoinNetwork"]), sp.GetRequiredService<ILoggerFactory>(), sp.GetRequiredService<IHttpClientFactory>());
        });
        services.AddTransient<ILegalEntityFactory, LegalEntityFactory>();
        services.AddTransient<IConsensusActivationValidatorFactory, ConsensusActivationValidatorFactory>();
        services.AddSingleton<IBackgroundJobs, BackgroundJobs>();
        services.AddHostedService<BackgroundJobStarter>();
      }

      services.AddHttpClient(LegalEntityFactory.CLIENT_NAME, config =>
        {          
          var productValue = new ProductInfoHeaderValue(
             Assembly.GetExecutingAssembly().GetName().Name, 
             Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
          config.DefaultRequestHeaders.UserAgent.Add(productValue);
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
          UseCookies =
            false, // Disable cookies they are not needed and we do not want to leak them - https://docs.microsoft.com/en-us/aspnet/core/fundamentals/http-requests?view=aspnetcore-3.1#cookies
          AllowAutoRedirect = false
        });

      services.AddSingleton<IMetrics, Infrastructure.Actions.Metrics>();
      services.AddOpenTelemetryMetrics(x => 
      {
        x.AddMeter(Infrastructure.Actions.Metrics.COURT_ORDER_STATISTICS_METER);
        x.AddPrometheusExporter();
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, 
                          IWebHostEnvironment env, 
                          IHostApplicationLifetime lifetime,
                          IDelegatedKeys delegatedKeys,
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

      app.UseOpenTelemetryPrometheusScrapingEndpoint();

      lifetime.ApplicationStarted.Register(() => { OnApplicationStarted(delegatedKeys, Configuration); });

      app.UseEndpoints(endpoints =>
      {
        endpoints.MapControllers();
      });

      logger.LogInformation("Version: " + Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "VSTHRD002:Avoid problematic synchronous waits", Justification = "ApplicationStarted.Register() doesn't support async methods")]
    private void OnApplicationStarted(IDelegatedKeys delegatedKeys, IConfiguration configuration)
    {
      delegatedKeys.CreateInitialSignerKeyAsync(Network.GetNetwork(configuration["AppSettings:BitcoinNetwork"]), HostEnvironment.EnvironmentName != "Testing").GetAwaiter().GetResult();
    }
  }
}
