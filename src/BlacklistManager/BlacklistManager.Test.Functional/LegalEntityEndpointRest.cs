// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Test.Functional.Server;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class LegalEntityEndpointRest : TestRestBase<LegalEntityEndpointViewModelGet, LegalEntityEndpointViewModelCreate>
  {

    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(mockedServices: true);
      await backgroundJobs.StopAllAsync(); // don't want process for court order acceptance to start 
      // This key is needed when adding NT endpoint
      TrustlistRepository.CreatePublicKey("0293ff7c31eaa93ce4701a462676c1e46dac745f6848097f57357d2a414b379a34", true, null);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      base.Cleanup();
    }

    [TestMethod]
    public override async Task Delete_NoElement_ShouldReturnNoContentAsync()
    {
      // test not applicable to legalEntityEndpoint. There is no delete action
      await Task.CompletedTask;
    }

    [TestMethod]
    public override async Task GetKeyShouldBeCaseInsensitiveAsync()
    {
      // test not applicable to legalEntityEndpoint. GetKey is a number.
      await Task.CompletedTask;
    }

    [TestMethod]
    public override async Task DeleteTestAsync()
    {
      var entry = GetItemToCreate();

      // Create new one using POST
      var (le1, httpResponse) = await PostAsync<LegalEntityEndpointViewModelCreate, LegalEntityEndpointViewModelGet>(client, entry, HttpStatusCode.Created);

      var firstKey = le1.Id.ToString();

      // disable first one
      await client.PostAsync(UrlForKey(firstKey) + "/disable", null);

      // check validUntil
      var entry1Response = await GetAsync<LegalEntityEndpointViewModelGet>(client, UrlForKey(firstKey), HttpStatusCode.OK);

      Assert.IsNotNull(entry1Response.ValidUntil);
      Assert.IsTrue(entry1Response.ValidUntil <= DateTime.UtcNow);

      // enable first one
      await client.PostAsync(UrlForKey(firstKey) + "/enable", null);

      // check validUntil
      entry1Response = await GetAsync<LegalEntityEndpointViewModelGet>(client, UrlForKey(firstKey), HttpStatusCode.OK);

      Assert.IsNull(entry1Response.ValidUntil);
    }

    public override string GetNonExistentKey() => "123";

    public override string GetBaseUrl() => BlacklistManagerServer.ApiLegalEntityEndpointUrl;

    public override LegalEntityEndpointViewModelCreate GetItemToCreate()
    {
      return new LegalEntityEndpointViewModelCreate() { BaseUrl = "baseUrl1", APIKey = "apiKey1" };
    }

    public override LegalEntityEndpointViewModelCreate[] GetItemsToCreate()
    {
      return new[]
      {
        new LegalEntityEndpointViewModelCreate() { BaseUrl = "baseUrl1", APIKey = "apiKey1" },
        new LegalEntityEndpointViewModelCreate() { BaseUrl = "baseUrl2", APIKey = "apiKey2" }
      };
    }

    public override void CheckWasCreatedFrom(LegalEntityEndpointViewModelCreate post, LegalEntityEndpointViewModelGet get)
    {
      Assert.AreEqual(post.BaseUrl.ToLower(), get.BaseUrl);
      Assert.AreEqual(post.APIKey, get.APIKey);
    }

    public override string ExtractGetKey(LegalEntityEndpointViewModelGet entry)
    {
      return entry.Id.ToString();
    }

    public override string ExtractPostKey(LegalEntityEndpointViewModelCreate entry)
    {
      switch (entry.APIKey)
      {
        case "apiKey1":
          return "1";
        case "apiKey2":
          return "2";
      }
      throw new BadRequestException("Map entry with its future database key");
    }

    public override void SetPostKey(LegalEntityEndpointViewModelCreate entry, string key)
    {      
    }

    public override void ModifyEntry(LegalEntityEndpointViewModelCreate entry)
    {
      entry.APIKey += "x";
    }
  }
}