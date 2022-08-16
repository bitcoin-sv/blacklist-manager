// Copyright (c) 2020 Bitcoin Association

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using nChain.CreateDB;
using nChain.CreateDB.DB;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.API.Rest.Database
{
  public class CreateBlacklistManagerDB : ICreateDB
  {
    // !!!! IMPORTANT: Keep this const in sync with placeholders in 01_SYS_CreateDB.sql scripts
    public const string FROZENFUND_PASSWORD = "FROZENFUND_PASSWORD";
    readonly CreateDB createDB;

    public CreateBlacklistManagerDB(ILogger<CreateDB> logger, IConfiguration configuration)
    {
      var variables = PrepareScriptVariables(configuration);
      createDB = new CreateDB(logger, 
                              "BlacklistManager", 
                              RDBMS.Postgres, 
                              configuration["BlacklistManagerConnectionStrings:DBConnectionString"], 
                              configuration["BlacklistManagerConnectionStrings:DBConnectionStringMaster"],
                              variables: variables);
    }
    public bool DatabaseExists()
    {
      return createDB.DatabaseExists();
    }

    public bool DoCreateDB(out string errorMessage, out string errorMessageShort)
    {
      return createDB.CreateDatabase(out errorMessage, out errorMessageShort);
    }

    private Dictionary<string, string> PrepareScriptVariables(IConfiguration configuration)
    {
      var variables = new Dictionary<string, string>();

      var dbConnString = configuration["BlacklistManagerConnectionStrings:DBConnectionString"];
      var connStringPasswordPart = dbConnString.Split(";").Where(x => x.ToLower().Contains("password")).Single();

      variables.Add(FROZENFUND_PASSWORD, connStringPasswordPart.Split("=")[1]);

      return variables;
    }
  }
}
