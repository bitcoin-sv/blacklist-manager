// Copyright (c) 2020 Bitcoin Association
using System;
using System.Collections.Generic;

namespace BlacklistManager.API.Rest.ViewModels
{
  [Serializable]
  public class CheckMessageViewModel
  {
    private const string BLACKLIST_MANAGER = "Blacklist Manager";
    private const string ALERT_MANAGER = "Alert Manager";
    private const string BITCOIND = "bitcoind";

    private const string ERROR = "Error";
    private const string WARNING = "Warning";
    private const string INFO = "Info";

    public string Component { get; init; }
    public string Endpoint { get; init; }
    public string Severity { get; init; }
    public List<string> Messages { get; init; } = new ();

    public static CheckMessageViewModel SetBitcoindError(string endpoint, string message) => SetData(BITCOIND, endpoint, ERROR, message);
    public static CheckMessageViewModel SetBMError(string endpoint, string message) => SetData(BLACKLIST_MANAGER, endpoint, ERROR, message);
    public static CheckMessageViewModel SetAlertManagerError(string endpoint, string message) => SetData(ALERT_MANAGER, endpoint, ERROR, message);
    public static CheckMessageViewModel SetAlertManagerWarning(string endpoint, string message) => SetData(ALERT_MANAGER, endpoint, WARNING, message);
    public static CheckMessageViewModel SetAlertManagerInfo(string endpoint, string message) => SetData(ALERT_MANAGER, endpoint, INFO, message);
    public static CheckMessageViewModel SetAlertManagerInfo(string endpoint, string[] message) => SetData(ALERT_MANAGER, endpoint, INFO, message);

    private static CheckMessageViewModel SetBasicData(string component, string endpoint, string severity) => new CheckMessageViewModel
                                                                                                             {
                                                                                                               Component = component,
                                                                                                               Endpoint = endpoint,
                                                                                                               Severity = severity
                                                                                                             };
    private static CheckMessageViewModel SetData(string component, string endpoint, string severity, string message)
    {
      var checkMessage = SetBasicData(component, endpoint, severity);
      checkMessage.Messages.Add(message);

      return checkMessage;
    }
    private static CheckMessageViewModel SetData(string component, string endpoint, string severity, string[] message)
    {
      var checkMessage = SetBasicData(component, endpoint, severity);
      checkMessage.Messages.AddRange(message);

      return checkMessage;
    }
  }
}
