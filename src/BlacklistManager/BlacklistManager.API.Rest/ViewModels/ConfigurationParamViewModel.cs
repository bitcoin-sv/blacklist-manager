// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class ConfigurationParamViewModel
  {
    public ConfigurationParamViewModel()
    {
    }

    public ConfigurationParamViewModel(ConfigurationParam configParam)
    {
      Key = configParam.Key;
      Value = configParam.Value;
    }

    [Required]
    [JsonPropertyName("key")]
    public string Key { get; set; }

    [Required]
    [JsonPropertyName("value")]
    public string Value { get; set; }
  }
}
