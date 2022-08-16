// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Test.Functional.MockServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using BMDomain = BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Models;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class ProcessCourtOrder : TestBase
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
    public async Task ProcessCourtOrder_TestRpcCallsForMultipleOrdersAsync()
    {
      //
      // test use case is same as for test BlacklistRepository_TestFundStatusForMultipleOrders but on higher level - bitcoind RPC calls are asserted
      // we are not testing process/timers. Process events are triggered/simulated with direct call to business logic
      //
      // See spec, chapter 8.5	Handling funds affected by multiple court orders for details
      //

      //arrange
      // see base.Initialize()
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));


      //act

      // #2 Freeze order1 arrives, requiring freeze of A and its children:
      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A", 1),
        ("A1", 1),
        ("A2", 1),
        ("B2", 1),
        ("B3", 1));
      await backgroundJobs.WaitAllAsync();

      // #3 Freeze order2 arrives, requiring freeze of C and its children:
      var order2Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("C", 1),
        ("C1", 1),
        ("C2", 1),
        ("B2", 1),
        ("B3", 1));
      await backgroundJobs.WaitAllAsync();

      // #4: An unfreeze order for all fund from order1 arrives. Unfreeze order does not need to reach the consensus
      var unfreezeOrder1Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order1Hash, new (string TxId, long vOut)[] { });
      await backgroundJobs.WaitAllAsync();

      // #5:  Court order 3 arrives, requiring freeze of A0 and its children
      var order3Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A0", 1),
        ("A1", 1),
        ("A2", 1),
        ("B2", 1),
        ("B3", 1));
      await backgroundJobs.WaitAllAsync();

      // #6 Miner reach consensus to start enforcing consensus freeze for order 2 (containing C and its children)
      await domainLogic.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.FreezeConsensus, 100);
      await backgroundJobs.WaitAllAsync();

      // #7 Unfreeze order arrives, referencing freeze order2. It requires unfreeze of C2 and B3, while keeping the rest (C, C1, B2) frozen.
      var unfreezeOrder2Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order2Hash,
        ("C2", 1),
        ("B3", 1));
      await backgroundJobs.WaitAllAsync();

      // #7 Miners reach consensus on unfreeze order
      await domainLogic.SetCourtOrderStatusAsync(unfreezeOrder2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await backgroundJobs.WaitAllAsync();

      // #8 Unfreeze order for all funds from Order3 arrives.
      var unfreezeOrder3Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order3Hash, new (string TxId, long vOut)[] { });
      await backgroundJobs.WaitAllAsync();

      //assert
      var callList = bitcoindFactory.callList;

      Assert.AreEqual(3, callList.AddToPolicyCalls.Count, "wrong number of addToPolicyBlacklist calls");
      Assert.AreEqual(3, callList.RemoveFromPolicyCalls.Count, "wrong number of removeFromPolicyBlacklist calls");
      Assert.AreEqual(3, callList.AddToConsensusCalls.Count, "wrong number of addToConsensusBlacklist calls");

      Assert.AreEqual("node1:addToPolicy/a,1||/a1,1||/a2,1||/b2,1||/b3,1||", callList.AllCalls[0].ToString());                      // #2
      Assert.AreEqual("node1:addToPolicy/c,1||/c1,1||/c2,1||", callList.AllCalls[1].ToString());                                    // #3
      Assert.AreEqual("node1:removeFromPolicy/a,1||/a1,1||/a2,1||", callList.AllCalls[2].ToString());                               // #4
      Assert.AreEqual("node1:addToPolicy/a0,1||/a1,1||/a2,1||", callList.AllCalls[3].ToString());                                   // #5
      Assert.AreEqual("node1:addToConsensus/b2,1|100,|False/b3,1|100,|False/c,1|100,|True/c1,1|100,|True/c2,1|100,|True", callList.AllCalls[4].ToString());  // #6
      Assert.AreEqual("node1:addToConsensus/b3,1|100,200|False/c2,1|100,200|True", callList.AllCalls[5].ToString());                // #7
      Assert.AreEqual("node1:removeFromPolicy/a1,1||/a2,1||", callList.AllCalls[6].ToString());                              // #8
      Assert.AreEqual("node1:addToConsensus/b2,1|100,|True/b3,1|100,200|True", callList.AllCalls[7].ToString());                    // #8
      Assert.AreEqual("node1:removeFromPolicy/a0,1||", callList.AllCalls[8].ToString());                                            // #8
    }

    [TestMethod]
    public async Task ProcessCourtOrder_AddSameNodeTwice_ShouldNotCallClearBlacklistAsync()
    {
      //arrange
      // see base.Initialize()
      bitcoindFactory.Reset(new string[] { BitcoindCallList.Methods.GetBlockCount});

      // Create node
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await backgroundJobs.WaitAllAsync();

      //act
      await Utils.SubmitFreezeOrderAsync(client,
        ("a", 0)        
      );
      await backgroundJobs.WaitAllAsync();

      // Recreate node
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await backgroundJobs.WaitAllAsync();

      //act
      await Utils.SubmitFreezeOrderAsync(client,
        ("b", 0)
      );
      await backgroundJobs.WaitAllAsync();

      //assert
      bitcoindFactory.AssertEqualAndClear(
        "node1:clearAllBlacklists",
        "node1:addToPolicy/a,0||",        
        "node1:addToPolicy/b,0||"
        );
    }

    [TestMethod]
    public async Task ProcessCourtOrder_WithUnrespondingNodesAsync()
    {
      //arrange
      // see base.Initialize()

      // Create three nodes, disconnect the second one
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node2", 0, "mocked", "mocked", null));
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node3", 0, "mocked", "mocked", null));
      bitcoindFactory.DisconnectNode("node2");

      await backgroundJobs.WaitAllAsync();

      // fire signal after first propagation cycle 
      propagationEvents.SetPropagationSync(1);


      //act
      await Utils.SubmitFreezeOrderAsync(client,
        ("A", 0),
        ("B", 1)
      );

      await backgroundJobs.WaitForCourtOrderProcessingAsync();

      // wait for first cycle of propagation to finish
      Assert.IsTrue(propagationEvents.PropagationSync.WaitOne(1000), "Should receive propagationSync signal");

      Assert.AreEqual(TaskStatus.WaitingForActivation, backgroundJobs.Tasks.PropagateFunds.Last().Status, "Propagation background task should keep running");

      bitcoindFactory.AssertEqualAndClear(
        "node1:addToPolicy/a,0||/b,1||",
        // node node2 here, because it is disconnected
        "node3:addToPolicy/a,0||/b,1||"
      );

      // The first and the third node should be without errors
      Assert.IsTrue(string.IsNullOrEmpty(NodeRepository.GetNode("node1:0").LastError));
      Assert.IsNull(NodeRepository.GetNode("node1:0").LastErrorAt);

      Assert.IsTrue(string.IsNullOrEmpty(NodeRepository.GetNode("node3:0").LastError));
      Assert.IsNull(NodeRepository.GetNode("node3:0").LastErrorAt);

      // The second node should have error set.
      Assert.IsFalse(string.IsNullOrEmpty(NodeRepository.GetNode("node2:0").LastError));
      Assert.IsNotNull(NodeRepository.GetNode("node2:0").LastErrorAt);

      // Try to propagate OK - it should fail
      bitcoindFactory.WaitUntilNodeException();
      bitcoindFactory.AssertEqualAndClear(""); // No calls were made

      // Reconnect and try to propagate again
      bitcoindFactory.ReconnectNode("node2");

      await backgroundJobs.WaitAllAsync();

      bitcoindFactory.AssertEqualAndClear(
        "node2:addToPolicy/a,0||/b,1||"
      );

      // Error for Node2 should be cleared
      Assert.IsTrue(string.IsNullOrEmpty(NodeRepository.GetNode("node2:0").LastError));
      Assert.IsNull(NodeRepository.GetNode("node2:0").LastErrorAt);

      // Propagate again - there is nothing to do
      await backgroundJobs.WaitAllAsync();
      bitcoindFactory.AssertEqualAndClear(""); // No calls were made

      // Add another court order - its state should now be propagated to all nodes
      await Utils.SubmitFreezeOrderAsync(client,
        ("C", 0),
        ("D", 1)
      );

      await backgroundJobs.WaitAllAsync();

      bitcoindFactory.AssertEqualAndClear(
        "node1:addToPolicy/c,0||/d,1||",
        "node2:addToPolicy/c,0||/d,1||",
        "node3:addToPolicy/c,0||/d,1||"
      );
    }

    [TestMethod]
    public async Task PropagationFails_ShouldRetryPropagationAsync()
    {
      //arrange
      // see base.Initialize()
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      bitcoindFactory.DisconnectNode("node1");

      //act
      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A", 1));

      bitcoindFactory.WaitUntilNodeException();
      bitcoindFactory.ReconnectNode("node1");

      await backgroundJobs.WaitAllAsync();

      //assert
      bitcoindFactory.AssertEqualAndClear(
        $"node1:addToPolicy/a,1||");
    }

    [TestMethod]
    public async Task PropagationFails_NewPropagationRequested_ShouldCancelPreviousBackgroundTaskAsync()
    {
      //arrange
      // see base.Initialize()
      backgroundJobs.RetryDelayOverride = 10000;  // ensure background job will be canceled

      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await backgroundJobs.WaitAllAsync();
      bitcoindFactory.DisconnectNode("node1");

      //act
      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A", 1));

      bitcoindFactory.WaitUntilNodeException();
      bitcoindFactory.ReconnectNode("node1");

      var order2Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("B", 1));

      await backgroundJobs.WaitAllAsync();

      //assert
      var pft = backgroundJobs.Tasks.PropagateFunds;
      Assert.AreEqual(TaskStatus.Canceled, pft[pft.Count - 2].Status, "Penultimate propagated funds task should be canceled");

      bitcoindFactory.AssertEqualAndClear(
        "node1:addToPolicy/a,1||/b,1||");
    }

    [TestMethod]
    public async Task PropagationFails_BackgroundTaskShouldNotStopAsync()
    {
      //arrange
      // see base.Initialize()
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      bitcoindFactory.DisconnectNode("node1");

      await backgroundJobs.WaitAllAsync();

      // fire signal after first propagation cycle 
      propagationEvents.SetPropagationSync(1);


      //act
      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A1", 1));

      await backgroundJobs.WaitForCourtOrderProcessingAsync();

      // wait for first cycle of propagation to finish
      Assert.IsTrue(propagationEvents.PropagationSync.WaitOne(1000), "Should receive propagationSync signal");


      //assert
      Assert.AreEqual(TaskStatus.WaitingForActivation, backgroundJobs.Tasks.PropagateFunds.Last().Status);
      bitcoindFactory.AssertEqualAndClear("");

      await backgroundJobs.StopAllAsync();
      await backgroundJobs.WaitAllAsync();
    }

    [TestMethod]
    public async Task PropagationCanceled_ShouldSavePropagationsToDatabaseAsync()
    {
      //arrange
      // see base.Initialize()      

      // fire signal after first propagation
      bitcoindFactory.SetPropagationSync(1);

      //act
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      bitcoindFactory.DisconnectNode("node1");

      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A", 1));

      await backgroundJobs.WaitForCourtOrderProcessingAsync();  // only court order status needs to be processed, fund propagations are not yet finished (node disconnected)

      await domainLogic.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);

      bitcoindFactory.ReconnectNode("node1");

      // wait for first propagation to finish
      Assert.IsTrue(bitcoindFactory.propagationSyncWaitForPropagation.WaitOne(1000), "Should receive propagationSync signal");

      loggerTest.LogDebug("Stopping background jobs");
      await backgroundJobs.StopAllAsync();
      loggerTest.LogDebug("Background jobs stopped from tests");

      //assert
      Assert.AreEqual(TaskStatus.Canceled, backgroundJobs.Tasks.PropagateFunds.Last().Status);

      bitcoindFactory.AssertEqualAndClear(
        "node1:addToPolicy/a,1||"
        //"node1:addToConsensus/a,1" is missing because job was canceled after first propagation ended
        );

      var fsp = await CourtOrderRepository.GetFundStateToPropagateAsync();
      Assert.AreEqual(1, fsp.Count(), "Only one state expected for propagation");
      AssertExtension.AreEqual($"1|a,1|{order1Hash},100,-1,False|{order1Hash},-1,-1,False", fsp.First());
    }

    [TestMethod]
    public async Task ProcessCourtOrder_2xFreezeUnfreezeProcessAsync()
    {
      //act
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));

      // first process
      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A", 0));
      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await backgroundJobs.WaitAllAsync();

      var order2Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order1Hash,
        ("A", 0));
      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await backgroundJobs.WaitAllAsync();

      bitcoindFactory.AssertEqualAndClear(
       "node1:addToPolicy/a,0||",
       "node1:addToConsensus/a,0|100,|True",
       "node1:addToConsensus/a,0|100,200|True");

      // second process
      var order3Hash = await Utils.SubmitFreezeOrderAsync(client, "somecourtorder2",
         ("A", 0));

      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order3Hash, CourtOrderStatus.FreezeConsensus, 300);
      await backgroundJobs.WaitAllAsync();

      var order4Hash = await Utils.SubmitUnfreezeOrderAsync(client, "somecourtorder2",
        order3Hash,
        ("A", 0));
      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order4Hash, CourtOrderStatus.UnfreezeConsensus, 400);
      await backgroundJobs.WaitAllAsync();

      //assert
      bitcoindFactory.AssertEqualAndClear(
        "node1:addToConsensus/a,0|100,200|False",
        "node1:addToConsensus/a,0|100,200;300,|True",
        "node1:addToConsensus/a,0|100,200;300,400|True");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_2xFreezeUnfreezeProcess_PartialUnfreezeAsync()
    {
      //act
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));

      // first process
      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A", 0));
      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await backgroundJobs.WaitAllAsync();

      var order2Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order1Hash,
        ("A", 0));
      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await backgroundJobs.WaitAllAsync();

      bitcoindFactory.AssertEqualAndClear(
       "node1:addToPolicy/a,0||",
       "node1:addToConsensus/a,0|100,|True",
       "node1:addToConsensus/a,0|100,200|True");

      // second process
      var order3Hash = await Utils.SubmitFreezeOrderAsync(client,
         ("A", 0),
         ("B", 1));

      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order3Hash, CourtOrderStatus.FreezeConsensus, 300);
      await backgroundJobs.WaitAllAsync();

      var order4Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order3Hash,
        ("A", 0));
      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order4Hash, CourtOrderStatus.UnfreezeConsensus, 400);
      await backgroundJobs.WaitAllAsync();

      var order5Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order3Hash,
        ("B", 1));
      await backgroundJobs.WaitAllAsync();

      await domainLogic.SetCourtOrderStatusAsync(order5Hash, CourtOrderStatus.UnfreezeConsensus, 500);
      await backgroundJobs.WaitAllAsync();

      //assert      
      bitcoindFactory.AssertEqualAndClear(
        "node1:addToConsensus/a,0|100,200|False",
        "node1:addToPolicy/b,1||",
        "node1:addToConsensus/a,0|100,200;300,|True/b,1|300,|True",
        "node1:addToConsensus/a,0|100,200;300,400|True",
        "node1:addToConsensus/b,1|300,500|True");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_FreezeUnfreezeProcess_DisconectedNode_ShouldPropagateGroupedByStateChangeAsync()
    {
      //act

      // first process
      var order1Hash = await Utils.SubmitFreezeOrderAsync(client,
        ("A", 0),
        ("B", 1));
      await backgroundJobs.WaitForCourtOrderProcessingAsync();

      await domainLogic.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await backgroundJobs.WaitForCourtOrderProcessingAsync();

      var order2Hash = await Utils.SubmitUnfreezeOrderAsync(client,
        order1Hash,
        ("A", 0),
        ("B", 1));
      await backgroundJobs.WaitForCourtOrderProcessingAsync();

      await domainLogic.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await domainLogic.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await backgroundJobs.WaitAllAsync();

      bitcoindFactory.AssertEqualAndClear(
       "node1:addToPolicy/a,0||/b,1||",
       "node1:addToConsensus/a,0|100,|True/a,0|100,200|True/b,1|100,|True/b,1|100,200|True");
    }
  }
}
