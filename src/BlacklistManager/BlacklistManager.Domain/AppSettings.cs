// Copyright (c) 2020 Bitcoin Association

using Common;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.Domain
{
  public class AppSettings
  {
    [JsonIgnore]
    [Required]
    public string REST_APIKey { get; set; }

    [Range(1, 600)]
    public int BackgroundJobDelayTime { get; set; } = 30;
    
    [Required]
    [BitcoinNetworkValue]
    public string BitcoinNetwork { get; set; }
    
    public string AllowedHosts { get; set; }
    
    [JsonIgnore]
    [Required]
    public string EncryptionKey { get; set; }
    
    [Required]
    [Range(1, 20)]
    public byte TxResubmitionPeriodInBlocks { get; set; }

    [JsonIgnore]
    [Required]
    [Range(1, byte.MaxValue)]
    public byte BlockHashCollectionSize { get; set; }
    
    [Required]
    [Range(1, 500)]
    public int MaxRetryCount { get; set; }

    [Range(1, 600)]
    public int ConsensusActivationRetryDelayTime { get; set; } = 30;

    [Range(1, 600)]
    public int OnErrorRetryDelayTime { get; set; } = 60;

    [Required]
    public int ConsensusWaitDays { get; set; }
  }
}
