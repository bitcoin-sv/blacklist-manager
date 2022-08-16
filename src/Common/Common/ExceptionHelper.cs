// Copyright (c) 2020 Bitcoin Association

using System;

namespace Common
{
  public static class ExceptionHelper
  {
    public static string UnwrapExceptionAsString(this Exception ex)
    {
      return $"Message: {ex.GetBaseException().Message}; {Environment.NewLine} StackTrace: {ex.GetBaseException().StackTrace}";
    }
  }
}
