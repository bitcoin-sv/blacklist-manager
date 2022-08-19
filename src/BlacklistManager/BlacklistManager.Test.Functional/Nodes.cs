// Copyright (c) 2020 Bitcoin Association

using System.Threading.Tasks;
using BlacklistManager.Test.Functional.MockServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlacklistManager.Domain.Models;
using Common;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class Nodes : TestBase
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


    [TestMethod]
    public async Task WhenAddingNewNodeBlacklistShouldBeUpdatedAsync()
    {
      //arrange
      // see base.Initialize()
      BitcoindFactory.Reset(new string[] { BitcoindCallList.Methods.GetBlockCount});

      //assert
      string courtOrderHash1 = await Utils.SubmitFreezeOrderAsync(Client, 
        (1000, "a", 1), 
        (1000, "b", 2));
      await BackgroundJobs.WaitForPropagationAsync();
      BitcoindFactory.AssertEqualAndClear("");

      await Nodes.CreateNodeAsync(new Node("node1", 0, "mocked", "mocked", null));
      await BackgroundJobs.WaitAllAsync();
      BitcoindFactory.AssertEqualAndClear(
        "node1:clearAllBlacklists",
        "node1:addToPolicy/a,1||/b,2||");

      // add second node
      await Nodes.CreateNodeAsync(new Node("node2", 0, "mocked", "mocked", null));
      await BackgroundJobs.WaitAllAsync();
      BitcoindFactory.AssertEqualAndClear(
        "node2:clearAllBlacklists",
        "node2:addToPolicy/a,1||/b,2||");

      // add third node
      await Nodes.CreateNodeAsync(new Node("node3", 0, "mocked", "mocked", null));
      await BackgroundJobs.WaitAllAsync();
      BitcoindFactory.AssertEqualAndClear(
        "node3:clearAllBlacklists",
        "node3:addToPolicy/a,1||/b,2||");


      // Activate consensus - it should be propagated to all nodes
      await CourtOrders.SetCourtOrderStatusAsync(courtOrderHash1, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      BitcoindFactory.AssertEqualAndClear(
        "node1:addToConsensus/a,1|100,|True/b,2|100,|True",
        "node2:addToConsensus/a,1|100,|True/b,2|100,|True",
        "node3:addToConsensus/a,1|100,|True/b,2|100,|True"
      );
    }

    [TestMethod]
    public async Task WhenAddingNewNodeCheckConnectivityAsync()
    {
      //arrange
      // see base.Initialize()
      BitcoindFactory.Reset(null);

      // Act
      await Nodes.CreateNodeAsync(new Node("node1", 0, "mocked", "mocked", null));

      BitcoindFactory.AssertEqualAndClear(
        "node1:getBlockCount",
        "node1:clearAllBlacklists"); 
      
      // We are able to retrieve a node
      Assert.IsNotNull(await Nodes.GetNodeAsync("node1:0"));

      BitcoindFactory.DisconnectNode("node2");
      
      // Node is disconnected, will not be added
      await Assert.ThrowsExceptionAsync<BadRequestException>(async () => await Nodes.CreateNodeAsync(new Node("node2", 0, "mocked", "mocked", null)));
      BitcoindFactory.AssertEqualAndClear(""); // no successful call was made
      Assert.IsNull(await Nodes.GetNodeAsync("node2:0"));
    }

  }
}
