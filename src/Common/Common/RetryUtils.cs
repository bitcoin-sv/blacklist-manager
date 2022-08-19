// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading.Tasks;

namespace Common
{
  public static class RetryUtils
  {
    public async static Task ExecuteWithRetriesAsync(int noOfRetries, string errorMessage, Func<Task> methodToExecute, int sleepTimeBetweenRetries = 1000)
    {
      try
      {
        do
        {
          noOfRetries--;
          try
          {
            await methodToExecute();
            return;
          }
          catch
          {
            await Task.Delay(sleepTimeBetweenRetries);
            if (noOfRetries == 0)
            {
              throw;
            }
          }
        }
        while (noOfRetries > 0);
      }
      catch (Exception ex)
      {
        if (!string.IsNullOrEmpty(errorMessage))
          throw new Exception(errorMessage, ex);
        throw;
      }
    }
  }
}
