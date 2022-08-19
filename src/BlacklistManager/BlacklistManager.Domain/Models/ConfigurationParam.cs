// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class ConfigurationParam
  {
    public ConfigurationParam(string paramKey, string paramValue)
    {
      Key = paramKey.ToLower();
      Value = paramValue;
    }

    public string Key { get; private set; }
    public string Value { get; private set; }
  }
}
