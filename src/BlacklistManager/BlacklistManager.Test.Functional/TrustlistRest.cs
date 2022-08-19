// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
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
  public class TrustListRest : TestRestBase<TrustListItemViewModelGet, TrustListItemViewModelCreate>
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(addPK: false, mockedServices: true);
    }

    [TestCleanup]
    public async Task TestCleanupAsync()
    {
      await base.CleanupAsync();
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
      var expectedTrusted = post.Trusted ?? false; // default trust is false
      Assert.AreEqual(expectedTrusted, get.Trusted);
      Assert.AreEqual(post.Remarks, get.Remarks);
    }

    public override Task DeleteTestAsync()
    {
      return Task.CompletedTask;
    }

    public override Task Delete_NoElement_ShouldReturnNoContentAsync()
    {
      return Task.CompletedTask;
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

    private async Task<(string, string)> CreateLinkedKeysAsync()
    {
      var pubKey1 = new NBitcoin.Key().PubKey.ToHex();
      var pubKey2 = new NBitcoin.Key().PubKey.ToHex();

      var create = new TrustListItemViewModelCreate
      {
        Id = pubKey1,
        Trusted = true,
      };
      await PostTrustListRequestAsync(create);

      create.Id = pubKey2;
      var content = new StringContent(JsonSerializer.Serialize(create), Encoding.UTF8, MediaTypeNames.Application.Json);

      // Create a new key
      var response = await Client.PostAsync(UrlForKey(""), content);
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
      var responseContent = await response.Content.ReadAsStringAsync();
      var getResponse = JsonSerializer.Deserialize<TrustListItemViewModelGet>(responseContent);
      Assert.IsNotNull(getResponse);
      Assert.AreEqual(pubKey2, getResponse.Id);
      Assert.IsTrue(getResponse.Trusted);

      var put = new TrustListItemViewModelPut
      {
        Id = pubKey1,
        ReplacedBy = pubKey2,
        Trusted = false
      };
      await PutTrustListRequestAsync(put, pubKey1);

      // Check everything is ok
      response = await Client.GetAsync(UrlForKey(pubKey1));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

      responseContent = await response.Content.ReadAsStringAsync();

      getResponse = JsonSerializer.Deserialize<TrustListItemViewModelGet>(responseContent);
      Assert.IsNotNull(getResponse);
      Assert.AreEqual(pubKey1, getResponse.Id);
      Assert.IsFalse(getResponse.Trusted);

      return (pubKey1, pubKey2);
    }

    [TestMethod]
    public async Task RelinkKeysAsync()
    {
      var (pubKey1, pubKey2) = await CreateLinkedKeysAsync();

      var pubKey3 = new NBitcoin.Key().PubKey.ToHex();
      // create 3rdKey
      var create = new TrustListItemViewModelCreate
      {
        Id = pubKey3,
        Trusted = true,
      };
      await PostTrustListRequestAsync(create);

      var put = new TrustListItemViewModelPut
      {
        Id = pubKey2,
        ReplacedBy = pubKey3,
        Trusted = false
      };
      await PutTrustListRequestAsync(put, pubKey2);

      var response = await Client.GetAsync(UrlForKey(""));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var responseContent = await response.Content.ReadAsStringAsync();
      var getRespArray = JsonSerializer.Deserialize<IEnumerable<TrustListItemViewModelGet>>(responseContent);

      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey1 && x.ReplacedBy == pubKey2));
      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey2 && x.ReplacedBy == pubKey3));
      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey3 && x.ReplacedBy is null));

      // relink key 1 to key 3. leaving key 2 as sole key
      put.Id = pubKey1;
      put.ReplacedBy = pubKey3;
      await PutTrustListRequestAsync(put, pubKey1);

      response = await Client.GetAsync(UrlForKey(""));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      responseContent = await response.Content.ReadAsStringAsync();
      getRespArray = JsonSerializer.Deserialize<IEnumerable<TrustListItemViewModelGet>>(responseContent);

      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey1 && x.ReplacedBy == pubKey3));
      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey2 && x.ReplacedBy == pubKey3));
      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey3 && x.ReplacedBy is null));
    }

    [TestMethod]
    public async Task RejectCreateLinkToNonExistantKeyAsync()
    {
      var pubKey1 = new NBitcoin.Key().PubKey.ToHex();
      var nonExistantKey = new NBitcoin.Key().PubKey.ToHex();
      var create = new TrustListItemViewModelCreate
      {
        Id = pubKey1,
        Trusted = true,
      };
      await PostTrustListRequestAsync(create);

      var put = new TrustListItemViewModelPut
      {
        Id = pubKey1,
        ReplacedBy = nonExistantKey
      };
      var responseContent = await PutTrustListRequestAsync(put, pubKey1, HttpStatusCode.BadRequest);
      var pd = JsonSerializer.Deserialize<ProblemDetails>(responseContent);
      Assert.AreEqual($"Public key {nonExistantKey} does not exist.", pd.Title);
    }


    private async Task PostTrustListRequestAsync(TrustListItemViewModelCreate request)
    {
      var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, MediaTypeNames.Application.Json);
      var response = await Client.PostAsync(UrlForKey(""), content);
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<string> PutTrustListRequestAsync(TrustListItemViewModelPut request, string pubKeyId, HttpStatusCode expectedStatusCode = HttpStatusCode.NoContent)
    {
      var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, MediaTypeNames.Application.Json);
      var response = await Client.PutAsync(UrlForKey(pubKeyId), content);
      Assert.AreEqual(expectedStatusCode, response.StatusCode);
      return await response.Content.ReadAsStringAsync();
    }

    [TestMethod]
    public async Task CreateLinkWithTwoKeysAsync()
    {
      var pubKey1 = new NBitcoin.Key().PubKey.ToHex();
      var pubKey2 = new NBitcoin.Key().PubKey.ToHex();
      var pubKey3 = new NBitcoin.Key().PubKey.ToHex();

      var create = new TrustListItemViewModelCreate
      {
        Id = pubKey1,
        Trusted = true,
      };
      await PostTrustListRequestAsync(create);

      create.Id = pubKey2;
      await PostTrustListRequestAsync(create);

      create.Id = pubKey3;
      await PostTrustListRequestAsync(create);

      var put = new TrustListItemViewModelPut
      {
        Id = pubKey1,
        ReplacedBy = pubKey3,
        Trusted = true,
      };
      await PutTrustListRequestAsync(put, pubKey1);

      put.Id = pubKey2;
      await PutTrustListRequestAsync(put, pubKey2);

      var response = await Client.GetAsync(UrlForKey(""));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var responseContent = await response.Content.ReadAsStringAsync();
      var getRespArray = JsonSerializer.Deserialize<IEnumerable<TrustListItemViewModelGet>>(responseContent);

      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey1 && x.ReplacedBy == pubKey3));
      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey2 && x.ReplacedBy == pubKey3));
      Assert.IsTrue(getRespArray.Any(x => x.Id == pubKey3 && x.ReplacedBy is null));
    }

    [TestMethod]
    public async Task RejectRelinkForUsedKeyAsync()
    {
      var pubKey1 = new NBitcoin.Key().PubKey.ToHex();

      var create = new TrustListItemViewModelCreate
      {
        Id = pubKey1,
        Trusted = true,
      };
      await PostTrustListRequestAsync(create);

      create.Id = Utils.PublicKey;
      await PostTrustListRequestAsync(create);

      var put = new TrustListItemViewModelPut
      {
        Id = pubKey1,
        ReplacedBy = Utils.PublicKey,
        Trusted = true,
      };
      await PutTrustListRequestAsync(put, pubKey1);

      await Utils.SubmitFreezeOrderAsync(Client, (1000, "A", 0), (2000, "B", 0));

      var pubKey2 = new NBitcoin.Key().PubKey.ToHex();
      create.Id = pubKey2;
      await PostTrustListRequestAsync(create);

      put.ReplacedBy = pubKey2;
      await PutTrustListRequestAsync(put, pubKey1, HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task RejectInfiniteLoopCreationAsync()
    {
      var pubKey1 = new NBitcoin.Key().PubKey.ToHex();
      var pubKey2 = new NBitcoin.Key().PubKey.ToHex();
      var pubKey3 = new NBitcoin.Key().PubKey.ToHex();
      var pubKey4 = new NBitcoin.Key().PubKey.ToHex();

      // Create keys
      var create = new TrustListItemViewModelCreate
      {
        Id = pubKey1,
        Trusted = true,
      };
      await PostTrustListRequestAsync(create);

      create.Id = pubKey2;
      await PostTrustListRequestAsync(create);

      create.Id = pubKey3;
      await PostTrustListRequestAsync(create);

      create.Id = pubKey4;
      await PostTrustListRequestAsync(create);

      // Link them from 1 -> 4
      var put = new TrustListItemViewModelPut
      {
        Id = pubKey1,
        ReplacedBy = pubKey2,
        Trusted = true,
      };
      await PutTrustListRequestAsync(put, pubKey1);

      put.Id = pubKey2;
      put.ReplacedBy = pubKey3;
      await PutTrustListRequestAsync(put, pubKey2);

      put.Id = pubKey3;
      put.ReplacedBy = pubKey4;
      await PutTrustListRequestAsync(put, pubKey3);

      // Link the 4th to 1st, it must be rejected
      put.Id = pubKey4;
      put.ReplacedBy = pubKey1;
      var responseContent = await PutTrustListRequestAsync(put, pubKey4, HttpStatusCode.BadRequest);
      var pd = JsonSerializer.Deserialize<ProblemDetails>(responseContent);
      Assert.AreEqual($"Public key {put.ReplacedBy} is already part of key chain. Key looping is not allowed.", pd.Title);
    }
  }
}