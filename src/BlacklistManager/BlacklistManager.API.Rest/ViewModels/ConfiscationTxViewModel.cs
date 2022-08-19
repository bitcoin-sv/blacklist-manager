// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class ConfiscationTxViewModel
  {
    [JsonPropertyName("hex")]
    public string Hex { get; set; }
  }
}
