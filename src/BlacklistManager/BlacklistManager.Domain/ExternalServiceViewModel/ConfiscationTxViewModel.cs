// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace BlacklistManager.Domain.ExternalServiceViewModel
{
  public class ConfiscationTxViewModel
  {
    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    public string GetHexStartingPart()
    {
      return Hex.Substring(0, Hex.Length > 20 ? Hex.Length - 1 : 19);
    }
  }
}
