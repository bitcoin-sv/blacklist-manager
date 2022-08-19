// Copyright (c) 2020 Bitcoin Association

using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class ProcessCourtOrderViewModelResult
  {
    public ProcessCourtOrderViewModelResult(Domain.Models.ProcessCourtOrderResult result)
    {
      Id = result.CourtOrderHash;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }
  }
}
