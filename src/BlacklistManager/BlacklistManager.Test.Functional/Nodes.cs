// Copyright (c) 2020 Bitcoin Association

using System;
using System.Threading.Tasks;
using BlacklistManager.Test.Functional.MockServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using BlacklistManager.Domain.Models;

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
    public void TestCleanup()
    {
      base.Cleanup();
    }


    [TestMethod]
    public async Task WhenAddingNewNodeBlacklistShouldBeUpdatedAsync()
    {
      //arrange
      // see base.Initialize()
      bitcoindFactory.Reset(new string[] { BitcoindCallList.Methods.GetBlockCount});

      //assert
      string courtOrderHash1 = await Utils.SubmitFreezeOrderAsync(client, 
        ("a", 1), 
        ("b", 2));
      await backgroundJobs.WaitAllAsync();
      bitcoindFactory.AssertEqualAndClear("");

      await domainLogic.CreateNodeAsync(new Node("node1", 0, "mocked", "mocked", null));
      await backgroundJobs.WaitAllAsync();
      bitcoindFactory.AssertEqualAndClear(
        "node1:clearAllBlacklists",
        "node1:addToPolicy/a,1||/b,2||");

      // add second node
      await domainLogic.CreateNodeAsync(new Node("node2", 0, "mocked", "mocked", null));
      await backgroundJobs.WaitAllAsync();
      bitcoindFactory.AssertEqualAndClear(
        "node2:clearAllBlacklists",
        "node2:addToPolicy/a,1||/b,2||");

      // add third node
      await domainLogic.CreateNodeAsync(new Node("node3", 0, "mocked", "mocked", null));
      await backgroundJobs.WaitAllAsync();
      bitcoindFactory.AssertEqualAndClear(
        "node3:clearAllBlacklists",
        "node3:addToPolicy/a,1||/b,2||");


      // Activate consensus - it should be propagated to all nodes
      await domainLogic.SetCourtOrderStatusAsync(courtOrderHash1, CourtOrderStatus.FreezeConsensus, 100);

      await backgroundJobs.WaitAllAsync();

      bitcoindFactory.AssertEqualAndClear(
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
      bitcoindFactory.Reset(null);

      // Act
      await domainLogic.CreateNodeAsync(new Node("node1", 0, "mocked", "mocked", null));

      bitcoindFactory.AssertEqualAndClear(
        "node1:getBlockCount",
        "node1:clearAllBlacklists"); 
      
      // We are able to retrieve a node
      Assert.IsNotNull(domainLogic.GetNode("node1:0"));

      bitcoindFactory.DisconnectNode("node2");
      
      // Node is disconnected, will not be added
      Assert.ThrowsException<AggregateException>( () => domainLogic.CreateNodeAsync(new Node("node2", 0, "mocked", "mocked", null)).Result);
      bitcoindFactory.AssertEqualAndClear(""); // no successful call was made
      Assert.IsNull(domainLogic.GetNode("node2:0"));
    }

  }
}
