// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading;

namespace Common
{
  public static class RetryUtils
  {
    public static void Exec(Action action, int retry = 6)
    {      
      int retryDelay = 100;
      int initialRetry = retry;
      do
      {
        try
        {
          retry--;
          action();
          return;

        }
        catch (Exception ex)
        {
          if (retry == 0)
          {
            throw new Exception($"Failed after {initialRetry} retries", ex);
          }
        }

        Thread.Sleep(retryDelay);
        retryDelay *= 2;

      } while (retry > 0);

      throw new Exception("Exec with retry reached the end");
    }
  }
}
