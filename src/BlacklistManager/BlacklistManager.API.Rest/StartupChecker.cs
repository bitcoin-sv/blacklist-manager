// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.Database;
using Common;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace BlacklistManager.API.Rest
{
  public class StartupChecker : IStartupChecker
  {
    readonly IHostApplicationLifetime hostApplicationLifetime;
    readonly ILogger<BlackListManagerLogger> logger;
    readonly ICreateDB createDB;

    public StartupChecker(IHostApplicationLifetime hostApplicationLifetime, 
                          ILogger<BlackListManagerLogger> logger,
                          ICreateDB createDB)
    {
      this.hostApplicationLifetime = hostApplicationLifetime;
      this.logger = logger;
      this.createDB = createDB;
    }

    public async Task<bool> CheckAsync(bool testingEnvironment = false)
    {
      logger.LogInformation("Health checks starting.");
      try
      {
        await HelperTools.ExecuteWithRetriesAsync(10, "Unable to open connection to database", () => TestDBConnectionAsync());
        ExecuteCreateDb();

        logger.LogInformation("Health checks completed successfully.");
      }
      catch (Exception ex)
      {
        logger.LogError("Health checks failed. {0}", ex.GetBaseException().ToString());
        // If exception was thrown then we stop the application. All methods in try section must pass without exception
        hostApplicationLifetime.StopApplication();
      }
      
      return true;
    }

    private void ExecuteCreateDb()
    {
      logger.LogInformation($"Starting with execution of CreateDb ...");

      if (createDB.DoCreateDB(out string errorMessage, out string errorMessageShort))
      {
        logger.LogInformation("CreateDB finished successfully.");
      }
      else
      {
        // if error we must stop application
        throw new Exception($"Error when executing CreateDB: { errorMessage }{ Environment.NewLine }ErrorMessage: {errorMessageShort}");
      }

      logger.LogInformation($"ExecuteCreateDb completed.");
    }

    private Task TestDBConnectionAsync()
    {
      logger.LogInformation("Checking if 'BlacklistManager' database exists.");
      bool databaseExists = createDB.DatabaseExists();
      if (databaseExists)
      {
        logger.LogInformation($"Successfully connected to DB.");
      }
      else
      {
        logger.LogError("Connect to DB unsuccessful.");
      }
      return Task.CompletedTask;
    }
  }
}
