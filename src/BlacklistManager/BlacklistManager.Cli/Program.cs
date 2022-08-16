// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Cli
{

  class Program
  {
    static int Main(string[] args)
    {
      var cli = new BlacklistManagerCli();
      var result = cli.InvokeCommand(args);

      return result;
    }

  }
}
