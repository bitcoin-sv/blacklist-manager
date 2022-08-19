// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.API.Rest.ViewModels
{
  [Serializable]
  public class BackgroundJobStatusViewModel
  {
    public string Name { get; set; }
    public string Status { get; set; }
  }
}
