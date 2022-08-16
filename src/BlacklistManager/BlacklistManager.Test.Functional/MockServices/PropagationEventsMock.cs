// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.BackgroundJobs;
using System.Threading;

namespace BlacklistManager.Test.Functional.MockServices
{
  public class PropagationEventsMock : IPropagationEvents
  {
    public AutoResetEvent PropagationSync = new AutoResetEvent(false);
    private int? propagationCycleSync = null;
    private int propagationCycleCount = 0;

    public void Finished(bool propagationSuccessful)
    {
      propagationCycleCount++;
      if (propagationCycleCount == propagationCycleSync)
      {
        PropagationSync.Set();
      }
    }

    public void SetPropagationSync(int propagationCycle)
    {
      propagationCycleCount = 0;
      propagationCycleSync = propagationCycle;
      PropagationSync.Reset();
    }
  }
}
