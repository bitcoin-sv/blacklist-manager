// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class StatusViewModel
  {
    public bool OfflineModeInitiated { get; set; }
    public List<CheckMessageViewModel> CheckMessages { get; init; } = new();
    public BackgroundJobStatusViewModel[] BackgroundJobStatuses { get; set; }
    public AppSettings AppSettings { get; set; }

    public void AddCheckMessage(CheckMessageViewModel newMessage)
    {
      var checkMessage = CheckMessages.SingleOrDefault(x => x.Component == newMessage.Component &&
                                                            x.Endpoint == newMessage.Endpoint &&
                                                            x.Severity == newMessage.Severity);

      if (checkMessage == null)
      {
        CheckMessages.Add(newMessage);
      }
      else
      {
        checkMessage.Messages.AddRange(newMessage.Messages);
      }
    }
  }
}
