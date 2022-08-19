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
    readonly CreateDB _BMTestDB;

    public CreateTestDatabase(ILogger<CreateDB> logger, IConfiguration configuration)
    {
      System.Console.WriteLine(Directory.GetCurrentDirectory());
      _BMTestDB = new CreateDB(
        logger, 
        "BlacklistManager", 
        RDBMS.Postgres, 
        configuration["BlacklistManagerConnectionStrings:DBConnectionString"], 
        configuration["BlacklistManagerConnectionStrings:DBConnectionStringMaster"], 
        "..\\..\\..\\Database\\Scripts");
    }

    public bool DoCreateTestDatabase(out string errorMessage, out string errorMessageShort)
    {
      return _BMTestDB.CreateDatabase(out errorMessage, out errorMessageShort);
    }
  }
}
