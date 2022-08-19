// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class PendingConsensusActivation
  {
    public long InternalCourtOrderId { get; set; }
    public string CourtOrderHash { get; set; }
    public int CourtOrderTypeId { get; set; }
    public int LegalEntityEndpointId { get; set; }
    public string LegalEntityEndpointUrl { get; set; }
    public string LegalEntityEndpointApiKey { get; set; }
    public int RetryCount { get; set; }
  }
}
