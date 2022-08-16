// Copyright (c) 2020 Bitcoin Association

using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.Test.Functional.Server
{
  public class TestServerBase
  {
    public static async Task<TestServer> CreateServerAsync(bool mockedServices) 
    {
      var path = Assembly.GetAssembly(typeof(BlacklistManagerServer))
        .Location;

      var hostBuilder = new HostBuilder().ConfigureWebHost(configure => 
        { 
          configure.UseTestServer();
          if (mockedServices)
          {
            configure.UseEnvironment("Testing");
            configure.UseStartup<BlacklistManagerTestsStartup>();
          }
          else
          {
            configure.UseStartup<API.Rest.Startup>();
          }
          configure.ConfigureServices((s) =>
          {
            s.AddScoped<Database.ICreateDB>((x) =>
            {
              return new Database.CreateTestDatabase(x.GetService<ILogger<nChain.CreateDB.CreateDB>>(), x.GetService<IConfiguration>());
            });
          });
        })
        .UseContentRoot(Path.GetDirectoryName(path))
        .ConfigureAppConfiguration(cb =>
        {
          cb.AddJsonFile("appsettings.json", optional: false)
            .AddEnvironmentVariables();
        })
        .ConfigureLogging((context, logging) =>
        {
          logging.AddConfiguration(context.Configuration.GetSection("Logging"));
          logging.AddConsole();
          //logging.AddDebug();
        });

      var host = hostBuilder.Build();
      using var scope = host.Services.CreateScope();

      var createTestDb = scope.ServiceProvider.GetRequiredService<Database.ICreateDB>();
      createTestDb.DoCreateTestDatabase(out _, out _);

      var startup = scope.ServiceProvider.GetRequiredService<IStartupChecker>();
      await startup.CheckAsync(true);
      host.Start();
      return host.GetTestServer();
    }
  }
}
