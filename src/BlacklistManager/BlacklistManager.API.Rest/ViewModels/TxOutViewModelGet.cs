// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class TxOutViewModelGet : IValidatableObject
  {
    [Required]
    [JsonPropertyName("txId")]
    public string TxId { get; set; }

    [Required]
    [JsonPropertyName("vout")]
    public long Vout { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (Vout < 0)
      {
        yield return new ValidationResult(
          $"The {nameof(Vout)} field must be zero or greater",
            new[] { nameof(Vout) });
      }
    }
  }
}
