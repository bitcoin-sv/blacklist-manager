// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class ValidationError
  {
    public int LegalEntityEndpointId { get; set; }

    public string CourtOrderHash { get; set; }

    public string ErrorData { get; set; }
  }
}
