// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Test.Functional.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class ConfigurationParamRest : TestRestBase<ConfigurationParamViewModel, ConfigurationParamViewModel>
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

    public override string GetBaseUrl() => BlacklistManagerServer.ApiConfigurationParamUrl;

    public override ConfigurationParamViewModel GetItemToCreate()
    {
      return new ConfigurationParamViewModel() { Key = "key1", Value = "value1" };
    }

    public override ConfigurationParamViewModel[] GetItemsToCreate()
    {
      return new[]
      {
        new ConfigurationParamViewModel() { Key = "key1", Value = "value1" },
        new ConfigurationParamViewModel() { Key = "key2", Value = "value2" }
      };
    }

    public override void CheckWasCreatedFrom(ConfigurationParamViewModel post, ConfigurationParamViewModel get)
    {
      Assert.AreEqual(post.Key.ToLower(), get.Key.ToLower());
      Assert.AreEqual(post.Value, get.Value);
    }

    public override string ExtractGetKey(ConfigurationParamViewModel entry) => entry.Key;

    public override string ExtractPostKey(ConfigurationParamViewModel entry) => entry.Key;

    public override void SetPostKey(ConfigurationParamViewModel entry, string key)
    {
      entry.Key = key;
    }

    public override void ModifyEntry(ConfigurationParamViewModel entry)
    {
      entry.Value += "x";
    }
  }
}
