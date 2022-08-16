// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using nChain.CreateDB;
using nChain.CreateDB.DB;
using System.IO;

namespace BlacklistManager.Test.Functional.Database
{
  public class CreateTestDatabase : ICreateDB
  {
    readonly CreateDB blacklistManagerTestDB;

    public CreateTestDatabase(ILogger<CreateDB> logger, IConfiguration configuration)
    {
      System.Console.WriteLine(Directory.GetCurrentDirectory());
      blacklistManagerTestDB = new CreateDB(logger, "BlacklistManager", RDBMS.Postgres, configuration["BlacklistManagerConnectionStrings:DBConnectionString"], configuration["BlacklistManagerConnectionStrings:DBConnectionStringMaster"], "..\\..\\..\\Database\\Scripts");
    }

    public bool DoCreateTestDatabase(out string errorMessage, out string errorMessageShort)
    {
      return blacklistManagerTestDB.CreateDatabase(out errorMessage, out errorMessageShort);
    }
  }
}
