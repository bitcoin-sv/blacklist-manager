// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.Domain.Models
{
  public class LegalEntityEndpoint
  {
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

    public LegalEntityEndpoint(
      int legalEntityEndpointId,
      string baseUrl,
      string apiKey,
      DateTime createdAt) : this(legalEntityEndpointId, baseUrl, apiKey)
    {
      CreatedAt = createdAt;
    }

    public LegalEntityEndpoint(
      int legalEntityEndpointId, 
      string baseUrl, 
      string apiKey, 
      DateTime createdAt,
      DateTime? validUntil, 
      DateTime? lastContactedAt,
      DateTime? lastErrorAt, 
      string lastError,
      string courtOrderSyncToken,
      string courtOrderAcceptanceSyncToken, 
      string courtOrderDeltaLink) : this(legalEntityEndpointId, baseUrl, apiKey, createdAt)
    {
      ValidUntil = validUntil;
      LastContactedAt = lastContactedAt;
      LastErrorAt = lastErrorAt;
      LastError = lastError;
      CourtOrderSyncToken = courtOrderSyncToken;
      CourtOrderAcceptanceSyncToken = courtOrderAcceptanceSyncToken;
      CourtOrderDeltaLink = courtOrderDeltaLink;
    }

    public int LegalEntityEndpointId { get; private set; }
    public string BaseUrl { get; private set; }
    public string APIKey { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public DateTime? LastContactedAt { get; private set; }
    public string LastError { get; private set; }
    public DateTime? LastErrorAt { get; private set; }
    public string CourtOrderSyncToken { get; private set; }
    public string CourtOrderAcceptanceSyncToken { get; private set; }
    public string CourtOrderDeltaLink { get; private set; }
  }
}
