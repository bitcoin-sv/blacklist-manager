// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlacklistManager.Test.Functional
{
  public abstract class TestRestBase<TGetViewModel, TPostViewModel> : TestBase
  where TGetViewModel : class
  where TPostViewModel : class
  {
    public virtual string GetNonExistentKey() => "ThisKeyDoesNotExists";

    public abstract string GetBaseUrl();


    public abstract TPostViewModel GetItemToCreate();

    public abstract TPostViewModel[] GetItemsToCreate();

    /// <summary>
    /// Throws if entry "post" posted to server does not match the one returned from "get"
    /// </summary>
    public abstract void CheckWasCreatedFrom(TPostViewModel post, TGetViewModel get);


    // NOTE: wee need separate ExtractXXXKeys methods because view and post model can be the same. 
    public abstract string ExtractGetKey(TGetViewModel entry);
    public abstract string ExtractPostKey(TPostViewModel entry);
    public abstract void SetPostKey(TPostViewModel entry, string key);

    public string ChangeKeyCase(string s)
    {
      StringBuilder sb = new StringBuilder(s.Length);
      foreach (var c in s)
      {
        if (char.IsLower(c))
        {
          sb.Append(char.ToUpper(c));
        }
        else
        {
          // This will not convert non-letter characters
          sb.Append(char.ToLower(c));
        }
      }
      return sb.ToString();
    }

    public abstract void ModifyEntry(TPostViewModel entry);

    public string UrlForKey(string key)
    {
      return GetBaseUrl() + "/" + HttpUtility.UrlEncode(key);
    }
    public async Task<(TResponse response, HttpResponseMessage httpResponse)> PostAsync<TRequest, TResponse>(HttpClient client,
      TRequest request,
      HttpStatusCode expectedStatusCode)
      where TResponse : class
    {
      var httpResponse = await client.PostAsync(GetBaseUrl(),
        new StringContent(JsonSerializer.Serialize(request),
          Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode);

      string responseString = await httpResponse.Content.ReadAsStringAsync();
      TResponse response = null;

      if (httpResponse.IsSuccessStatusCode) // Only try to deserialize in case there are no exception
      {
        response = JsonSerializer.Deserialize<TResponse>(responseString);
      }
      return (response, httpResponse);
    }

    public async Task<HttpResponseMessage> PutAsync<TRequest>(HttpClient client,
      string uri,
      TRequest request,
      HttpStatusCode expectedStatusCode)
    {
      var httpResponse = await client.PutAsync(uri,
        new StringContent(JsonSerializer.Serialize(request),
          Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode);

      string responseString = await httpResponse.Content.ReadAsStringAsync();
      return httpResponse;
    }


    public async Task<TResponse> GetAsync<TResponse>(HttpClient client, string uri, HttpStatusCode expectedStatusCode)
    {
      var httpResponse = await client.GetAsync(uri);

      Assert.AreEqual(expectedStatusCode, httpResponse.StatusCode);

      string responseString = await httpResponse.Content.ReadAsStringAsync();
      if (string.IsNullOrEmpty(responseString))
      {
        return default;
      }
      var response = JsonSerializer.Deserialize<TResponse>(responseString);
      return response;
    }


    public async Task DeleteAsync(HttpClient client, string uri)
    {
      var httpResponse = await client.DeleteAsync(uri);

      // Delete always return NoContent to make (response) idempotent
      Assert.AreEqual(HttpStatusCode.NoContent, httpResponse.StatusCode); 
    }

    [TestMethod]
    public async Task GetByID_NonExistingKey_ShouldReturn404Async()
    {
      var httpResponse = await Client.GetAsync(UrlForKey(GetNonExistentKey()));
      Assert.AreEqual(HttpStatusCode.NotFound, httpResponse.StatusCode);
    }

    [TestMethod]
    public async Task GetCollection_NoElements_ShouldReturn200EmptyAsync()
    {
      var httpResponse = await Client.GetAsync(GetBaseUrl());
      Assert.AreEqual(HttpStatusCode.OK, httpResponse.StatusCode);
      var content = await httpResponse.Content.ReadAsStringAsync();
      Assert.AreEqual("[]", content);
    }

    [TestMethod]
    public virtual async Task GetKeyShouldBeCaseInsensitiveAsync()
    {
      var item1 = GetItemToCreate();
      var item1Key = ExtractPostKey(item1);

      // Check that id does not exists (database is deleted at start of test)
      await GetAsync<TGetViewModel>(Client, UrlForKey(item1Key), HttpStatusCode.NotFound);

      // Create new one using POST
      await PostAsync<TPostViewModel, TGetViewModel>(Client, item1, HttpStatusCode.Created);

      // We should be able to retrieve it:
      var entry1Response = await GetAsync<TGetViewModel>(Client, UrlForKey(item1Key), HttpStatusCode.OK);
      Assert.AreEqual(item1Key, ExtractGetKey(entry1Response));

      // Retrieval by key should be case sensitive
      await GetAsync<TGetViewModel>(Client, UrlForKey(ChangeKeyCase(item1Key)), HttpStatusCode.OK);
    }

    [TestMethod]
    public async Task TestPostAsync()
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await GetAsync<TGetViewModel>(Client, UrlForKey(ExtractPostKey(entryPost)), HttpStatusCode.NotFound);

      // Create new one using POST
      var (entryResponsePost, reponsePost) = await PostAsync<TPostViewModel, TGetViewModel>(Client, entryPost, HttpStatusCode.Created);

      CheckWasCreatedFrom(entryPost, entryResponsePost);


      // Case insensitive compare (HttpUtility encoded ':'. as %3a, where server encoded it as %3A).
      Assert.AreEqual(0, string.Compare(reponsePost.Headers.Location.AbsolutePath, UrlForKey(entryPostKey), StringComparison.OrdinalIgnoreCase) );


      // And we should be able to retrieve the entry through GET
      var get2 = await GetAsync<TGetViewModel>(Client, UrlForKey(entryPostKey), HttpStatusCode.OK);

      // And entry returned by POST should be the same as entry returned by GET
      CheckWasCreatedFrom(entryPost, get2);
    }

    [TestMethod]
    public async Task TestPost_2x_ShouldReturn409Async()
    {
      var entryPost = GetItemToCreate();

      (_, _) = await PostAsync<TPostViewModel, TGetViewModel>(Client, entryPost, HttpStatusCode.Created);
      await Task.Delay(500);
      (_, _) = await PostAsync<TPostViewModel, TGetViewModel>(Client, entryPost, HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task GetMultipleAsync()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await PostAsync<TPostViewModel, TGetViewModel>(Client, entry, HttpStatusCode.Created);
      }

      // We should be able to retrieve it:
      var getEntries = await GetAsync<TGetViewModel[]>(Client,
        GetBaseUrl(), HttpStatusCode.OK);

      Assert.AreEqual(entries.Length, getEntries.Length);

      foreach (var postEntry in entries)
      {
        var postKey = ExtractPostKey(postEntry);
        var getEntry = getEntries.Single(x => ExtractGetKey(x) == postKey);
        CheckWasCreatedFrom(postEntry, getEntry);
      }
    }

    [TestMethod]
    public async Task MultiplePostAsync()
    {
      var entryPost = GetItemToCreate();

      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await GetAsync<TGetViewModel>(Client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);


      // Create new one using POST
      await PostAsync<TPostViewModel, TGetViewModel>(Client, entryPost, HttpStatusCode.Created);

      // Try to create it again - it should fail
      await PostAsync<TPostViewModel, TGetViewModel>(Client, entryPost, HttpStatusCode.Conflict);
    }

    [TestMethod]
    public async Task PutAsync()
    {
      var entryPost = GetItemToCreate();
      var entryPostKey = ExtractPostKey(entryPost);

      // Check that id does not exists (database is deleted at start of test)
      await GetAsync<TGetViewModel>(Client, UrlForKey(entryPostKey), HttpStatusCode.NotFound);


      // Try updating a non existent entry
      await PutAsync(Client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.NotFound);

      // Create new one using POST
      await PostAsync<TPostViewModel, TGetViewModel>(Client, entryPost, HttpStatusCode.Created);

      // Update entry:
      ModifyEntry(entryPost);
      await PutAsync(Client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.NoContent);

      var entryGot = await GetAsync<TGetViewModel>(Client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, entryGot);


      // Try to modify entry by using primary key with different case
      entryPostKey = ChangeKeyCase(entryPostKey);
      SetPostKey(entryPost, entryPostKey);
      ModifyEntry(entryPost);
      await PutAsync(Client, UrlForKey(entryPostKey), entryPost, HttpStatusCode.NoContent);

      var entryGot2 = await GetAsync<TGetViewModel>(Client, UrlForKey(entryPostKey), HttpStatusCode.OK);
      CheckWasCreatedFrom(entryPost, entryGot2);
    }

    [TestMethod]
    public virtual async Task DeleteTestAsync()
    {
      var entries = GetItemsToCreate();

      foreach (var entry in entries)
      {
        // Create new one using POST
        await PostAsync<TPostViewModel, TGetViewModel>(Client, entry, HttpStatusCode.Created);
      }

      // Check if all are there
      foreach (var entry in entries)
      {
        // Create new one using POST
        await GetAsync<TGetViewModel>(Client, UrlForKey(ExtractPostKey(entry)), HttpStatusCode.OK);
      }

      var firstKey = ExtractPostKey(entries.First());
        
      // Delete first one
      await DeleteAsync(Client, UrlForKey(firstKey));

      // GET should not find the first anymore, but it should find the rest
      foreach (var entry in entries)
      {
        var key = ExtractPostKey(entry);
        await GetAsync<TGetViewModel>(Client, UrlForKey(key),

          key == firstKey ? HttpStatusCode.NotFound : HttpStatusCode.OK);
      }
    }

    [TestMethod]
    public virtual async Task Delete_NoElement_ShouldReturnNoContentAsync()
    {
      // Delete always return NoContent to make (response) idempotent
      await DeleteAsync(Client, UrlForKey("XYZ:1"));
    }
  }
}
