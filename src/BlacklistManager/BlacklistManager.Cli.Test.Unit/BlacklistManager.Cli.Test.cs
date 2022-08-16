// Copyright (c) 2020 Bitcoin Association

using System;
using System.CommandLine;
using System.IO;
using System.Net;
using System.Net.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlacklistManager.Cli.Test.Unit
{

  [TestClass]
  public class BlackListmanagerCliTests
  {
    readonly IConsole console = new TestConsole();

    void TestCall(string[] args, MockHttpMessageHandler handler, string[] errorMustContain = null)
    {

      var cli = new BlacklistManagerCli();

      handler.ProcessRequest = (request) => new HttpResponseMessage
      {
        StatusCode = HttpStatusCode.OK,
        Content = new StringContent("responseFromRestCall") 
      };

      cli.InvokeCommand(args, console, handler);

      var errorOutput = console.Error.ToString() ?? string.Empty;
      if (!string.IsNullOrWhiteSpace(errorOutput))
      {
        Console.WriteLine("Errors written to error output:");
        Console.WriteLine(errorOutput);
      }

      if (errorMustContain == null)
      {
        // We expect that call is made when there are no errors
        Assert.AreEqual(1, handler.Invoked);
        // Response should be printed to console
        Assert.AreEqual("responseFromRestCall" + Environment.NewLine, console.Out.ToString());

      }
      else
      {
        // No call should be made in case of errors
        Assert.AreEqual(0, handler.Invoked);
   
        foreach (var error in errorMustContain)
        {
          Assert.IsTrue(errorOutput.Contains(error), $"Error message should contain {error}, but it does not");
        }


      }

    }


    [TestMethod]
    public void PostTrustList()
    {
      TestCall(
        new[] { "host:8000", "POST", "TrustList", "someData", "--apiKey=123" },
        new MockHttpMessageHandler(
          HttpMethod.Post,
          "http://host:8000/api/v1/TrustList",
          "someData")

        );
    }


    [TestMethod]
    public void GetTrustList()
    {
      TestCall(
        new[] { "host:8000", "GET", "TrustList", "--apiKey=123" },
        new MockHttpMessageHandler(
        HttpMethod.Get,
        "http://host:8000/api/v1/TrustList",
        null)
      );
    }

    [TestMethod]
    public void GetTrustListWithId()
    {
      TestCall(
        new[] { "host:8000", "GET", "TrustList/some:key","--apiKey=123" },
        new MockHttpMessageHandler(
          HttpMethod.Get,
          "http://host:8000/api/v1/TrustList/some:key",// semicolon is not encoded
          null)
      );
    }


    [TestMethod]
    public void PutTrustList() 
    {
      TestCall(
        new[] { "host:8000", "PUT", "TrustList/someId", "newValue", "--apiKey=123" },
        new MockHttpMessageHandler(
          HttpMethod.Put,
          "http://host:8000/api/v1/TrustList/someId",
          "newValue")
      );
    }

    [TestMethod]
    public void DeleteTrustList()
    {
      TestCall(
        new[] { "host:8000", "DELETE", "TrustList/someId", "--apiKey=123" },
        new MockHttpMessageHandler(
          HttpMethod.Delete,
          "http://host:8000/api/v1/TrustList/someId",
          null)
      );
    }

    [TestMethod]
    public void PostNode()
    {
      TestCall(
        new[] { "host:8000", "POST", "Node", "someData", "--apiKey=123" },
        new MockHttpMessageHandler(
          HttpMethod.Post,
          "http://host:8000/api/v1/Node",
          "someData")

      );
    }
    [TestMethod]
    public void PostPostCourtOrder()
    {
      TestCall(
        new[] { "host:8000", "POST", "CourtOrder", "someData", "--apiKey=123" },
        new MockHttpMessageHandler(
          HttpMethod.Post,
          "http://host:8000/api/v1/CourtOrder",
          "someData")

      );
    }

    [TestMethod]
    public void GetCourtOrderWithParam()
    {
      TestCall(
        new[] { "host:8000", "GET", "CourtOrder?includeFunds=true", "--apiKey=123" },
        new MockHttpMessageHandler(
          HttpMethod.Get,
          "http://host:8000/api/v1/CourtOrder?includeFunds=true",
          null)
      );
    }

    [TestMethod]
    public void PostCourtFromDataFile()
    {
      string tempFile = Path.GetTempFileName();
      File.WriteAllText(tempFile, "someDataFromFile");
      try
      {

        TestCall(
          new[] {"host:8000", "POST", "CourtOrder", $"--dataFile={tempFile}", "--apiKey=123"},
          new MockHttpMessageHandler(
            HttpMethod.Post,
            "http://host:8000/api/v1/CourtOrder",
            "someDataFromFile")

        );
      }
      finally
      {
        File.Delete(tempFile);
      }
    }


    [TestMethod]
    public void PostCourtOrderWithResponseFile()
    {
      // Use response file and datafile
      string tempFile = Path.GetTempFileName();
      string responseFile = Path.GetTempFileName();
      File.WriteAllText(tempFile, "someDataFromFile");
      File.WriteAllText(responseFile, string.Join(Environment.NewLine, 
        new [] { "host:8000", "POST", "CourtOrder", $"--dataFile={tempFile}", "--apiKey=123" }));
      try
      {

        TestCall(
          new[] { $"@{responseFile}" },
          new MockHttpMessageHandler(
            HttpMethod.Post,
            "http://host:8000/api/v1/CourtOrder",
            "someDataFromFile")

        );

      }
      finally
      {
        File.Delete(tempFile);
        File.Delete(responseFile);
      }

    }


    [TestMethod]
    public void PostCourtOrderWithResponseFileMixed()
    {
      // Provide some options and response file and some om command line
      string responseFile = Path.GetTempFileName();
      
      File.WriteAllText(responseFile, string.Join(Environment.NewLine,
        new[] { "host:8000", "POST", "CourtOrder", "dataToPost"}));
      try
      {

        TestCall(
          new[] { $"@{responseFile}","--apiKey=123" },
          new MockHttpMessageHandler(
            HttpMethod.Post,
            "http://host:8000/api/v1/CourtOrder",
            "dataToPost")

        );

      }
      finally
      {
        File.Delete(responseFile);
      }
    }

    [TestMethod]
    public void PostCourtFromStdIn()
    {

      // IConsole does not provide a way for mocking the input. We will redirect a real one:
      Console.SetIn(new StringReader("DataFromStdin"));
      TestCall(
        new[] {"host:8000", "POST", "CourtOrder", "--dataStdin", "--apiKey=123"},
        new MockHttpMessageHandler(
          HttpMethod.Post,
          "http://host:8000/api/v1/CourtOrder",
          "DataFromStdin")
      );
    }


    [TestMethod]
    public void ApiKeyIsRequired()
    {
      TestCall(
        new[] { "host:8000", "GET", "TrustList", "apiKey1=123" },
        new MockHttpMessageHandler(
          HttpMethod.Get,
          "http://host:8000/api/v1/TrustList",
          null),
        new [] {"apiKey", "required"}
      );
    }

  }
}
