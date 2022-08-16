// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.API.Rest.ViewModels
{
  public class CheckMessageViewModel
  {
    private const string BlackListManager = "Blacklist Manager";
    private const string AlertManager = "Alert Manager";
    private const string BitcoinD = "bitcoind";

    public string Component { get; set; }
    public string Endpoint { get; set; }
    public string Severity { get; private set; }
    public string Message { get; set; }

    public static CheckMessageViewModel SetBitcoindError(string endpoint, string message) => SetError(BitcoinD, endpoint, message);
    public static CheckMessageViewModel SetBMError(string endpoint, string message) => SetError(BlackListManager, endpoint, message);
    public static CheckMessageViewModel SetAlertManagerError(string endpoint, string message) => SetError(AlertManager, endpoint, message);
    public static CheckMessageViewModel SetAlertManagerWarning(string endpoint, string message) => SetWarning(AlertManager, endpoint, message);
    public static CheckMessageViewModel SetAlertManagerInfo(string endpoint, string message) => SetInfo(AlertManager, endpoint, message);

    private static CheckMessageViewModel SetError(string component, string endpoint, string message)
    {
      return new CheckMessageViewModel
      {
        Component = component,
        Endpoint = endpoint,
        Message = message,
        Severity = "Error"
      };
    }

    private static CheckMessageViewModel SetWarning(string component, string endpoint, string message)
    {
      return new CheckMessageViewModel
      {
        Component = component,
        Endpoint = endpoint,
        Message = message,
        Severity = "Warning"
      };
    }

    private static CheckMessageViewModel SetInfo(string component, string endpoint, string message)
    {
      return new CheckMessageViewModel
      {
        Component = component,
        Endpoint = endpoint,
        Message = message,
        Severity = "Info"
      };
    }
  }
}
