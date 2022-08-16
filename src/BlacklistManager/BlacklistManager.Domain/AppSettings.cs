// Copyright (c) 2020 Bitcoin Association

using Newtonsoft.Json;
using System;
using System.ComponentModel.DataAnnotations;

namespace BlacklistManager.Domain
{
  public class AppSettings
  {
    [JsonIgnore]
    [Required]
    public string REST_APIKey { get; set; }
    [Required]
    public int BackgroundJobDelayTime { get; set; }
    [Required]
    public string BitcoinNetwork { get; set; }
    public string AllowedHosts { get; set; }
    [JsonIgnore]
    [Required]
    public string EncryptionKey { get; set; }
  }
}
