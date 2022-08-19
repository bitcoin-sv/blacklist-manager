// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.Domain.Models
{
  public class LegalEntityEndpoint
  {
    public LegalEntityEndpoint() { }

    public LegalEntityEndpoint(
      string baseUrl,
      string apiKey)
    {
      BaseUrl = baseUrl;
      APIKey = apiKey;
    }

    public LegalEntityEndpoint(
      int legalEntityEndpointId,
      string baseUrl,
      string apiKey) : this(baseUrl, apiKey)
    {
      LegalEntityEndpointId = legalEntityEndpointId;
    }

    public int LegalEntityEndpointId { get; private set; }
    public string BaseUrl { get; private set; }
    public string APIKey { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public DateTime? LastContactedAt { get; private set; }
    public string LastError { get; private set; }
    public DateTime? LastErrorAt { get; private set; }
    //TODO: remove
    public string CourtOrderSyncToken { get; private set; }
    //TODO: remove
    public string CourtOrderAcceptanceSyncToken { get; private set; }
    public string CourtOrderDeltaLink { get; private set; }
    public int ProcessedOrdersCount { get; init; }
    public int FailureCount { get; init; }
  }
}
