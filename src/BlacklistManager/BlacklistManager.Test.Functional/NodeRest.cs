// Copyright (c) 2020 Bitcoin Association

using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Test.Functional.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class NodeRest : TestRestBase<NodeViewModelGet, NodeViewModelCreate>
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(mockedServices: true);
    }

    [TestCleanup]
    public async Task TestCleanupAsync()
    {
      await base.CleanupAsync();
    }

    public override string GetNonExistentKey() => "ThisKeyDoesNotExists:123"; 
    public override string GetBaseUrl() => BlacklistManagerServer.ApiNodeUrl;
    public override string ExtractGetKey(NodeViewModelGet entry) => entry.Id;
    public override string ExtractPostKey(NodeViewModelCreate entry) => entry.Id;

    public override void SetPostKey(NodeViewModelCreate entry, string key)
    {
      entry.Id = key;
    }

    public override NodeViewModelCreate GetItemToCreate()
    {
      return new NodeViewModelCreate
      {
        Id = "some.host:123",
        Remarks = "Some remarks",
        Password = "somePassword",
        Username = "someUsername"
      };
    }

    public override void ModifyEntry(NodeViewModelCreate entry)
    {
      entry.Remarks += "Updated remarks";
      entry.Username += "updatedUsername";
    }

    public override NodeViewModelCreate[] GetItemsToCreate()
    {
      return
        new[]
        {
          new NodeViewModelCreate
          {
            Id = "some.host1:123",
            Remarks = "Some remarks1",
            Password = "somePassword1",
            Username = "user1"
          },

          new NodeViewModelCreate
          {
            Id = "some.host2:123",
            Remarks = "Some remarks2",
            Password = "somePassword2",
            Username = "user2"
          },
        };

    }

    public override void CheckWasCreatedFrom(NodeViewModelCreate post, NodeViewModelGet get)
    {
      Assert.AreEqual(post.Id.ToLower(), get.Id.ToLower()); // Ignore key case
      Assert.AreEqual(post.Remarks, get.Remarks);
      Assert.AreEqual(post.Username, get.Username);
      // Password can not be retrieved. We also do not check additional fields such as LastErrorAt
    }

    [TestMethod]
    public async Task CreateNode_WrongIdSyntax_ShouldReturnBadREquestAsync()
    {
      //arrange
      var create = new NodeViewModelCreate
      {
        Id = "some.host2", // missing port
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = "user2"
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, MediaTypeNames.Application.Json);

      //act
      var response = await Client.PostAsync(UrlForKey(""), content);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.AreEqual("Id", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task CreateNode_WrongIdSyntax2_ShouldReturnBadREquestAsync()
    {
      //arrange
      var create = new NodeViewModelCreate
      {
        Id = "some.host2:abs", // not a port number
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = "user2"
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, MediaTypeNames.Application.Json);

      //act
      var response = await Client.PostAsync(UrlForKey(""), content);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.AreEqual("Id", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task CreateNode_NoUsername_ShouldReturnBadRequestAsync()
    {
      //arrange
      var create = new NodeViewModelCreate
      {
        Id = "some.host2:2",
        Remarks = "Some remarks2",
        Password = "somePassword2",
        Username = null // missing username
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, MediaTypeNames.Application.Json);

      //act
      var response = await Client.PostAsync(UrlForKey(""), content);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.AreEqual("Username", vpd.Errors.First().Key);
    }
  }
}