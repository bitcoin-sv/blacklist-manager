// Copyright (c) 2020 Bitcoin Association

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Domain.Actions;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class ConfigurationParams : TestBase
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await base.InitializeAsync(mockedServices: true);
    }

    [TestCleanup]
    public async Task TestCleanupAsync()
    {
      await base.CleanupAsync();
    }

    [TestMethod]
    public async Task ConfigurationParams_DefaultsAsync()
    {
      var cp = Server.Services.GetRequiredService<IConfigurationParams>();
      Assert.AreEqual(75, await cp.GetDesiredHashrateAcceptancePercentAsync());
    }

    [TestMethod]
    public async Task ConfigurationParams_ValuesFromDatabaseAsync()
    {
      var cp = Server.Services.GetRequiredService<IConfigurationParams>();
      var repo = Server.Services.GetRequiredService<IConfigurationParamRepository>();

      //act
      await repo.InsertAsync(new ConfigurationParam("DesiredHashrateAcceptancePercent", "76"));

      //assert
      Assert.AreEqual(76, await cp.GetDesiredHashrateAcceptancePercentAsync());
    }

    [TestMethod]
    public async Task ConfigurationParams_IligalValueInDatabase_ShouldReturnDefaultAsync()
    {
      //arrange
      var cp = Server.Services.GetRequiredService<IConfigurationParams>();
      var repo = Server.Services.GetRequiredService<IConfigurationParamRepository>();

      //act
      await repo.InsertAsync(new ConfigurationParam("DesiredHashrateAcceptancePercent", "x"));

      //assert
      Assert.AreEqual(75, await cp.GetDesiredHashrateAcceptancePercentAsync());
    }

    [TestMethod]
    public async Task ConfigurationParams_UpdateValuesAsync()
    {
      var cp = Server.Services.GetRequiredService<IConfigurationParams>();
      var repo = Server.Services.GetRequiredService<IConfigurationParamRepository>();

      //act
      await repo.InsertAsync(new ConfigurationParam("DesiredHashrateAcceptancePercent", "76"));
      await repo.UpdateAsync(new ConfigurationParam("DesiredHashrateAcceptancePercent", "77"));

      //assert
      Assert.AreEqual(77, await cp.GetDesiredHashrateAcceptancePercentAsync());
    }
  }
}
