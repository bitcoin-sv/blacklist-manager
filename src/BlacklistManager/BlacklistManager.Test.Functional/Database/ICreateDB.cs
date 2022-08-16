// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Test.Functional.Database
{
  public interface ICreateDB
  {
    bool DoCreateTestDatabase(out string errorMessage, out string errorMessageShort);
  }
}
