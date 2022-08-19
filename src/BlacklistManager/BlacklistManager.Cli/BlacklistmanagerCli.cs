// Copyright (c) 2020 Bitcoin Association

using Common.CommandLine;
using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace BlacklistManager.Cli
{

  public class BlacklistManagerCli
  {
    readonly RootCommand rootCommand;
    // Used to share state between InvokeCommand and DoTheWork
    HttpMessageHandler messageHandler;

    IConsole console;
    
    public BlacklistManagerCli()
    {
      // set up commands and options
      var argResource = new Argument("resource")
      {
        Arity = ArgumentArity.ExactlyOne,
        Description = "Resource type that operation will be executed on."
      };

      argResource.AddSuggestions(new string[] { "TrustList", "CourtOrder", "Node" });
      var argDataOptional = new Argument("data")
      {
        Arity = ArgumentArity.ZeroOrOne,
        Description = "Data used for POST or PUT request. You must use this argument or provide data with --dataFile or -dataStdin"
      };

      var optionDataFile = new Option<FileInfo>(new [] {"--dataFile", "-f"})
      {
        Description = "Specifies file that contains data"
      };
      optionDataFile.ExistingOnly();

      var optionDataStdIn = new Option<bool>(new[] { "--dataStdin", "-s" })
      {
        Description = "Read data from standard input"
      };

      var argHostAndPort = new Argument("hostAndPort")
      {
        Arity = ArgumentArity.ExactlyOne,
        Description = "Host and port of Blacklist manager server. Example localhost:8000"
      };

      var optionApiKey = new Option<string>(new[] { "--apiKey", "-k"}) 
      {
        IsRequired = true,
        Description = "Api key used to authenticate HTTP requests"
      };

      var optionVerbose = new Option<bool>(new[] { "--verbose", "-v"})
      {
        Description = "Verbose output - (print HTTP headers etc)"
      };

      // We provide apiKeyOption as an option to specific to each of sub commands, so that we will be able to add additional
      // commands that do not require apikey in the future.
      // The downside is that --apiKey is not listed on the global help screen.

      string shellCat = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
        ? "cat courtOrder.json |"
        : "type courtOrder.json |";

      var putCommand = new CommandWithExamples(
        "PUT",
        "Updates a resource",
        new []
          {
          "%1 host:8000 PUT Node/nodeHost:8333 \"{ \\\"username\\\": \\\"newUsername\\\",  \\\"password\\\": \\\"newpassword\\\" }\" --apiKey=123"
          }

        ) {  argResource,  argDataOptional, optionDataFile, optionDataStdIn, optionApiKey };

      var postCommand = new CommandWithExamples(
        "POST",
        "Creates new resource",
        new[]
        {
          "%1 host:8000 POST Node \"{ \\\"id\\\" : \\\"nodeHost:8333\\\", \\\"username\\\": \\\"initialUsername\\\",  \\\"password\\\": \\\"initialPassword\\\" }\" --apiKey=123",
          "%1 host:8000 POST TrustList \"{\\\"id\\\":\\\"03e99d37d215927a5561be0822aa93076078f2156002230c53eead8919d54af12c\\\"}\" --apiKey=123",
          "%1 host:8000 POST CourtOrder --dataFile courtOrder.json --apiKey=123",
          shellCat + " %1 host:8000 POST CourtOrder --dataStdin --apiKey=123"
        }

      ) { argResource, argDataOptional, optionDataFile, optionDataStdIn, optionApiKey };

      var getCommand = new CommandWithExamples(
        "GET",
        "Retrieves a resource",
        new[]
        {
          "%1 host:8000 GET Node --apiKey=123",
          "%1 host:8000 GET Node/nodeHost:8333 --apiKey=123",
          "%1 host:8000 GET CourtOrder?includeFunds=false --apiKey=123",
          "%1 host:8000 GET Node @responseFile",
          "(options can be specified on command line or in a response file)",
        }

      ) { argResource,  optionApiKey};


      var deleteCommand = new CommandWithExamples(
        "DELETE",
        "Delete a resource",
        new[]
          {"%1 host:8000 DELETE Node/nodeHost:8333 --apiKey=123"}
      ) { argResource, optionDataFile, optionDataStdIn, optionApiKey };

      rootCommand = new RootCommand
      {
        optionVerbose,
        getCommand,
        putCommand,
        postCommand,
        deleteCommand,
        argHostAndPort,
      };

      rootCommand.Description = "Blacklist Manager CLI Tool can be used to control Blacklist Manager through command line.";

      rootCommand.Handler = CommandHandler.Create(() => { WriteError("Command is required"); }); 

      // Parameter  names in handler must match argument names
      putCommand.Handler = CommandHandler.Create(
        (string hostAndPort, string resource,  string data, FileInfo dataFile, bool dataStdin, string apiKey, bool verbose) =>
          DoTheWorkAsync(HttpMethod.Put, hostAndPort, resource, data, dataFile, dataStdin, apiKey, verbose)
      );

      postCommand.Handler = CommandHandler.Create(
        (string hostAndPort, string resource, string data, FileInfo dataFile, bool dataStdin, string apiKey, bool verbose) =>
          DoTheWorkAsync(HttpMethod.Post, hostAndPort, resource,  data, dataFile, dataStdin, apiKey, verbose)
      );

      getCommand.Handler = CommandHandler.Create(
        (string hostAndPort, string resource,  string apiKey, bool verbose) =>
          DoTheWorkAsync(HttpMethod.Get, hostAndPort, resource, null, null, false, apiKey, verbose));


      deleteCommand.Handler = CommandHandler.Create(
        (string hostAndPort, string resource, string apiKey, bool verbose) =>
          DoTheWorkAsync(HttpMethod.Delete, hostAndPort, resource, null, null, false, apiKey, verbose));

    }

    // Helper method. System.CommandLine defines overloads just up to T7
    static ICommandHandler CommandHandlerCreate<T1, T2, T3, T4, T5, T6, T7, T8>(
      Func<T1, T2, T3, T4, T5, T6, T7, T8, int> action) =>
      HandlerDescriptor.FromDelegate(action).GetCommandHandler();

    /// <summary>
    /// Returns REST url used to access the resource
    /// </summary>
    /// <returns></returns>
    public static Uri GetUrl(string hostAndPort, string resourceType)
    {
      var ub = new UriBuilder
      {
        Scheme = "http" // use http by default
      };

      var hostAndPortLower = hostAndPort.ToLower();

      if (hostAndPortLower.StartsWith("http://"))
      {
        hostAndPortLower = hostAndPortLower.Substring("http://".Length);
      }
      else if (hostAndPortLower.StartsWith("https://"))
      {
        ub.Scheme = "https";
        hostAndPortLower = hostAndPortLower.Substring("https://".Length);
      }

      if (!hostAndPortLower.Contains(":"))
      {
        ub.Host = hostAndPortLower;
      }
      else
      {
        var parts = hostAndPortLower.Split(":");
        if (parts.Length > 2 || !int.TryParse(parts[1], out int port))
        {
          throw new ArgumentException("Invalid syntax for hostAndPort");
        }
        ub.Port = port;
        ub.Host = parts[0];
      }

      if (resourceType.Contains("?"))
      {
        var split = resourceType.Split("?", 2);
        resourceType = split[0];
        ub.Query = split[1];
      }

      string path = $"/api/v1/{resourceType}";
      ub.Path = path;
      if (ub.Uri.AbsolutePath != path)
      {
        throw new ArgumentException($"Invalid syntax for resource {resourceType}");
      }
      return ub.Uri;
    }

    /// <summary>
    /// Obtain content that should be posted either from command line parameter or from an external file
    /// </summary>
    static HttpContent GetContent(string data, FileInfo dataFile, bool dataStdIn)
    {
      int count = 0;
      if (data != null) count++;
      if (dataFile != null) count++;
      if (dataStdIn) count++;

      if (count == 0 )
      {
        throw new ArgumentException("Either 'data', 'dataFile' or 'dataStdin' is required");
      }

      if (count > 1)
      {
        throw new ArgumentException("At most one of the following can be specified: 'data', 'dataFile' or 'dataStdin'");
      }

      HttpContent content = null;
      if (dataFile != null)
      {
        if (!File.Exists(dataFile.FullName)) // this is already  checked by command line parser
        {
          throw new FileNotFoundException($"File {dataFile} does not exists!");
        }

        content = new StreamContent(File.Open(dataFile.FullName, FileMode.Open)); 
      }
      else if(dataStdIn)
      {
        content = new StringContent(Console.In.ReadToEnd());
      }
      else
      {
        content = new StringContent(data);
      }

      content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
      return content;
    }

    /// <summary>
    ///  Not thread safe - it used shared state messageHandler
    /// </summary>
    public int InvokeCommand(string[] args, IConsole console = null, HttpMessageHandler messageHandler = null)
    {
      console ??= new SystemConsole();
      this.console = console;

      this.messageHandler = messageHandler;

      var parser = new CommandLineBuilder(rootCommand)
                     .UseDefaults()
                     .UseHelpBuilder(context => new AppendExamplesToHelp(context.Console, rootCommand))
                     .Build();

      var parseResult = parser.Parse(args);

      try
      {
        return parseResult.Invoke(console);
      }
      catch (Exception e)
      {
        WriteError(e.Message);
        return -1;
      }

    }

    async Task<int> DoTheWorkAsync(HttpMethod httpMethod, string hostAndPort, string resource,  string data, FileInfo dataFile, bool dataStdin, string apiKey, bool verbose)
    {
      try
      {
        if (string.IsNullOrEmpty(resource))
        {
          throw new ArgumentException("'resource' is required");
        }
        var uri = GetUrl(hostAndPort, resource);

        HttpContent content = null;
        using var client = new HttpClient(messageHandler ?? new HttpClientHandler()); // use messageHandler that was set in InvokeCommand

        client.DefaultRequestHeaders.Add(Common.Consts.ApiKeyHeaderName, apiKey);

        HttpResponseMessage response;
        try
        {

          if (httpMethod == HttpMethod.Get)
          {
            response = await client.GetAsync(uri);
          }
          else if (httpMethod == HttpMethod.Put)
          {
            content = GetContent(data, dataFile, dataStdin);
            response = await client.PutAsync(uri, content);
          }
          else if (httpMethod == HttpMethod.Post)
          {
            content = GetContent(data, dataFile, dataStdin);
            response = await client.PostAsync(uri, content);
          }
          else if (httpMethod == HttpMethod.Delete)
          {
            response = await client.DeleteAsync(uri);
          }
          else
          {
            throw new Exception($"Unknown http method '{httpMethod}'");
          }

        }
        finally
        {
          content?.Dispose();
        }

        if (!response.IsSuccessStatusCode)
        {
          WriteError($"Error {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        if (verbose)
        {
          console.Out.WriteLine((int) response.StatusCode + " " + response.ReasonPhrase);
          foreach (var h in response.Headers)
          {
            console.Out.WriteLine(h.Key + " " + string.Join(" ", h.Value.ToArray()));
          }

        }

        console.Out.WriteLine(await response.Content.ReadAsStringAsync());
      }
      catch (Exception e)
      {
        WriteError(e.Message);
      }

      return string.IsNullOrEmpty(console.Error.ToString()) ? 0 : 1;
    }

    void WriteError(string s)
    {
      Console.ForegroundColor = ConsoleColor.Red;
      console.Error.WriteLine(s);
      Console.ResetColor();
    }

  }
}
