// Copyright (c) 2020 Bitcoin Association

using System.Linq;
using System.Net;
using System.Net.Http;
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
  public class TrustListRest : TestRestBase<TrustListItemViewModelGet, TrustListItemViewModelCreate>
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(addPK: false, mockedServices: true);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      base.Cleanup();
    }

    public override string GetBaseUrl() => BlacklistManagerServer.ApiTrustListUrl;
    public override string ExtractGetKey(TrustListItemViewModelGet entry) => entry.Id;
    public override string ExtractPostKey(TrustListItemViewModelCreate entry) => entry.Id;
    public override void SetPostKey(TrustListItemViewModelCreate entry, string key)
    {
      entry.Id = key;
    }

    public override TrustListItemViewModelCreate  GetItemToCreate()
    {
      return new TrustListItemViewModelCreate
      {
        Id = "entry1",
        Remarks = "entry1Remarks"
      };
    }

    public override void ModifyEntry(TrustListItemViewModelCreate entry)
    {
      entry.Remarks += "Updated remarks";
      entry.Trusted = !(entry.Trusted ?? true);
    }

    public override TrustListItemViewModelCreate[] GetItemsToCreate()
    {
      return
        new[]
        {
          new TrustListItemViewModelCreate
          {
            Id = "entry1",
            Remarks = "entry1Remarks"
          },
          new TrustListItemViewModelCreate
          {
            Id = "entry2",
            Remarks = "Remarks2",
            Trusted = false
          }
        };

    }

    public override void CheckWasCreatedFrom(TrustListItemViewModelCreate post, TrustListItemViewModelGet get)
    {
      Assert.AreEqual(post.Id.ToLower(), get.Id.ToLower()); // Ignore key case
      var expectedTrusted = post.Trusted ?? true; // default trust is true
      Assert.AreEqual(expectedTrusted, get.Trusted);
      Assert.AreEqual(post.Remarks, get.Remarks);
    }

    [TestMethod]
    public async Task TrustListItemCreate_NoId_ShouldReturnBadRequestAsync()
    {
      //arrange
      var create = new TrustListItemViewModelCreate
      {
        Id = null, // no id
        Trusted = true,
        Remarks = "Some remarks2"
      };
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, "application/json");

      //act
      var response = await client.PostAsync(UrlForKey(""), content);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.AreEqual("Id", vpd.Errors.First().Key);
    }
  }
}