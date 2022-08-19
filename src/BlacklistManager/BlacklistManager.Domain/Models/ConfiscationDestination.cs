// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class ConfiscationDestination
  {
    public string Address { get; set; }
  
    public long? Amount { get; set; }
  }
}
