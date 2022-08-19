// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.Models
{
  public class Propagation
  {
    public Propagation(FundStateToPropagate stateToPropagate, PropagationAction action)
    {
      FundStateId = stateToPropagate.Id;
      Action = action;
      StateToPropagate = stateToPropagate;
    }

    public long FundStateId { get; private set; }
    public FundStateToPropagate StateToPropagate { get; set; }
    public PropagationAction Action { get; private set; }

    public override string ToString()
    {
      return $"{StateToPropagate}, {Action}";
    }
  }
}
