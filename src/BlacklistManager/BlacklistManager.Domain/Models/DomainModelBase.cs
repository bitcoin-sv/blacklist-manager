// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class DomainModelBase
  {
    public long Id { get; private set; }

    public DomainModelBase(long id)
    {
      Id = id;
    }
  }
}
