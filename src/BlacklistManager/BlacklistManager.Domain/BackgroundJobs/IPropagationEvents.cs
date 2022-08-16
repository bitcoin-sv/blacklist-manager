// Copyright (c) 2020 Bitcoin Association

namespace BlacklistManager.Domain.BackgroundJobs
{
  public interface IPropagationEvents
  {
    void Finished(bool propagationSuccessful);
  }
}
