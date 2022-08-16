// Copyright (c) 2020 Bitcoin Association


using System.Threading.Tasks;

namespace Common
{
  public interface IStartupChecker
  {
    public Task<bool> CheckAsync(bool testingEnvironment = false);
  }
}
