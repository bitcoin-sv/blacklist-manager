// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class StatusViewModel
  {
    public CheckMessageViewModel[] CheckMessages { get; set; }
    public AppSettings AppSettings { get; set; }
  }
}
