// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class LegalEntityEndpointViewModelPut : IValidatableObject
  {
    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; }

    [JsonPropertyName("apiKey")]
    public string APIKey { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
      if (string.IsNullOrEmpty(BaseUrl) && string.IsNullOrEmpty(APIKey))
      {
        yield return new ValidationResult(
          $"The {nameof(BaseUrl)} or {nameof(APIKey)} field must be set",
            new[] { nameof(BaseUrl), nameof(APIKey) });
      }
    }
  }
}
