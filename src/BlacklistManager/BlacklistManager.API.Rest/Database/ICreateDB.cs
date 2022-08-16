// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.API.Rest.Database
{
  public interface ICreateDB
  {
    bool DoCreateDB(out string errorMessage, out string errorMessageShort);
    bool DatabaseExists();
  }
}
