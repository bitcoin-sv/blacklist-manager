// Copyright (c) 2020 Bitcoin Association

using System;
using System.Net;

namespace Common
{
  public class HttpResponseException : Exception
  {
    public HttpResponseException(HttpStatusCode statusCode, string message) : base(message)
    {
      Status = (int)statusCode;
    }

    public HttpResponseException(HttpStatusCode statusCode, string message, Exception ex) : base(message, ex) 
    {
      Status = (int)statusCode;
    }
    public int Status { get; private set; }

    public object Value { get; set; }
  }

  public class BadRequestException : HttpResponseException
  {
    public BadRequestException(string message) : base(HttpStatusCode.BadRequest, message) { }

    public BadRequestException(string message, Exception ex) : base(HttpStatusCode.BadRequest, message, ex) { }
  }
}
