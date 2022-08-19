// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Test.Functional.MockServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using BMDomain = BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Models;
using System.Linq;
using Microsoft.Extensions.Logging;
using BlacklistManager.API.Rest.ViewModels;
using Common.SmartEnums;
using System.Collections.Generic;
using System.Text.Json;
using BlacklistManager.Test.Functional.Server;
using System.Net;
using Microsoft.AspNetCore.Mvc;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class ProcessCourtOrder : TestBase
  {
    public const string testPrivateKeyWif = "cNpxQaWe36eHdfU3fo2jHVkWXVt5CakPDrZSYguoZiRHSz9rq8nF";
    public const string testAddress = "msRNSw5hHA1W1jXXadxMDMQCErX1X8whTk";

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
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));


      //act

      // #2 Freeze order1 arrives, requiring freeze of A and its children:
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 1),
        (1000, "A1", 1),
        (1000, "A2", 1),
        (1000, "B2", 1),
        (1000, "B3", 1));
      await BackgroundJobs.WaitAllAsync();

      // #3 Freeze order2 arrives, requiring freeze of C and its children:
      var order2Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "C", 1),
        (1000, "C1", 1),
        (1000, "C2", 1),
        (1000, "B2", 1),
        (1000, "B3", 1));
      await BackgroundJobs.WaitAllAsync();

      // #4: An unfreeze order for all fund from order1 arrives. Unfreeze order does not need to reach the consensus
      _ = await Utils.SubmitUnfreezeOrderAsync(Client,
        order1Hash, new (long, string, long)[] { });
      await BackgroundJobs.WaitAllAsync();

      // #5:  Court order 3 arrives, requiring freeze of A0 and its children
      var order3Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A0", 1),
        (1000, "A1", 1),
        (1000, "A2", 1),
        (1000, "B2", 1),
        (1000, "B3", 1));
      await BackgroundJobs.WaitAllAsync();

      // #6 Miner reach consensus to start enforcing consensus freeze for order 2 (containing C and its children)
      await CourtOrders.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.WaitAllAsync();

      // #7 Unfreeze order arrives, referencing freeze order2. It requires unfreeze of C2 and B3, while keeping the rest (C, C1, B2) frozen.
      var unfreezeOrder2Hash = await Utils.SubmitUnfreezeOrderAsync(Client,
        order2Hash,
        (1000, "C2", 1),
        (1000, "B3", 1));
      await BackgroundJobs.WaitAllAsync();

      // #7 Miners reach consensus on unfreeze order
      await CourtOrders.SetCourtOrderStatusAsync(unfreezeOrder2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await BackgroundJobs.WaitAllAsync();

      // #8 Unfreeze order for all funds from Order3 arrives.
      _ = await Utils.SubmitUnfreezeOrderAsync(Client,
        order3Hash, new (long, string, long)[] { });
      await BackgroundJobs.WaitAllAsync();

      //assert
      var callList = BitcoindFactory.callList;

      Assert.AreEqual(3, callList.AddToPolicyCalls.Count, "wrong number of addToPolicyBlacklist calls");
      Assert.AreEqual(3, callList.RemoveFromPolicyCalls.Count, "wrong number of removeFromPolicyBlacklist calls");
      Assert.AreEqual(3, callList.AddToConsensusCalls.Count, "wrong number of addToConsensusBlacklist calls");
      Assert.AreEqual(9, callList.AllCalls.Count, "wrong number of total calls");

      Assert.AreEqual("node1:addToPolicy/a,1||/a1,1||/a2,1||/b2,1||/b3,1||", callList.AllCalls[0].ToString());                      // #2
      Assert.AreEqual("node1:addToPolicy/c,1||/c1,1||/c2,1||", callList.AllCalls[1].ToString());                                    // #3
      Assert.AreEqual("node1:removeFromPolicy/a,1||/a1,1||/a2,1||", callList.AllCalls[2].ToString());                               // #4
      Assert.AreEqual("node1:addToPolicy/a0,1||/a1,1||/a2,1||", callList.AllCalls[3].ToString());                                   // #5
      Assert.AreEqual("node1:addToConsensus/b2,1|100,|False/b3,1|100,|False/c,1|100,|True/c1,1|100,|True/c2,1|100,|True", callList.AllCalls[4].ToString());  // #6
      Assert.AreEqual("node1:addToConsensus/b3,1|100,200|False/c2,1|100,200|True", callList.AllCalls[5].ToString());                // #7
      Assert.AreEqual("node1:removeFromPolicy/a1,1||/a2,1||", callList.AllCalls[6].ToString());                                            // #8
      Assert.AreEqual("node1:addToConsensus/b2,1|100,|True/b3,1|100,200|True", callList.AllCalls[7].ToString());                    // #8
      Assert.AreEqual("node1:removeFromPolicy/a0,1||", callList.AllCalls[8].ToString());                                            // #8
    }

    [TestMethod]
    public async Task ProcessCourtOrder_AddSameNodeTwice_ShouldNotCallClearBlacklistAsync()
    {
      //arrange
      // see base.Initialize()
      BitcoindFactory.Reset(new string[] { BitcoindCallList.Methods.GetBlockCount });

      // Create node
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await BackgroundJobs.WaitAllAsync();

      //act
      await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "a", 0)
      );
      await BackgroundJobs.WaitAllAsync();

      // Recreate node
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await BackgroundJobs.WaitAllAsync();

      //act
      await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "b", 0)
      );
      await BackgroundJobs.WaitAllAsync();

      //assert
      BitcoindFactory.AssertEqualAndClear(
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
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await Nodes.CreateNodeAsync(new BMDomain.Node("node2", 0, "mocked", "mocked", null));
      await Nodes.CreateNodeAsync(new BMDomain.Node("node3", 0, "mocked", "mocked", null));
      BitcoindFactory.DisconnectNode("node2");

      await BackgroundJobs.WaitAllAsync();

      //act
      await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 0),
        (1000, "B", 1)
      );

      BitcoindFactory.AssertEqualAndClear(
        "node1:addToPolicy/a,0||/b,1||",
        // node node2 here, because it is disconnected
        "node3:addToPolicy/a,0||/b,1||"
      );

      // The first and the third node should be without errors
      Assert.IsTrue(string.IsNullOrEmpty((await NodeRepository.GetNodeAsync("node1:0")).LastError));
      Assert.IsNull((await NodeRepository.GetNodeAsync("node1:0")).LastErrorAt);

      Assert.IsTrue(string.IsNullOrEmpty((await NodeRepository.GetNodeAsync("node3:0")).LastError));
      Assert.IsNull((await NodeRepository.GetNodeAsync("node3:0")).LastErrorAt);

      // The second node should have error set.
      Assert.IsFalse(string.IsNullOrEmpty((await NodeRepository.GetNodeAsync("node2:0")).LastError));
      Assert.IsNotNull((await NodeRepository.GetNodeAsync("node2:0")).LastErrorAt);

      // Try to propagate OK - it should fail
      BitcoindFactory.WaitUntilNodeException();
      BitcoindFactory.AssertEqualAndClear(""); // No calls were made

      // Reconnect and try to propagate again
      BitcoindFactory.ReconnectNode("node2");

      await BackgroundJobs.WaitAllAsync();

      BitcoindFactory.AssertEqualAndClear(
        "node2:addToPolicy/a,0||/b,1||"
      );

      // Error for Node2 should be cleared
      Assert.IsTrue(string.IsNullOrEmpty((await NodeRepository.GetNodeAsync("node2:0")).LastError));
      Assert.IsNull((await NodeRepository.GetNodeAsync("node2:0")).LastErrorAt);

      // Propagate again - there is nothing to do
      await BackgroundJobs.WaitAllAsync();
      BitcoindFactory.AssertEqualAndClear(""); // No calls were made

      // Add another court order - its state should now be propagated to all nodes
      await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "C", 0),
        (1000, "D", 1)
      );

      await BackgroundJobs.WaitAllAsync();

      BitcoindFactory.AssertEqualAndClear(
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
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      BitcoindFactory.DisconnectNode("node1");

      //act
      _ = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 1));

      BitcoindFactory.WaitUntilNodeException();
      BitcoindFactory.ReconnectNode("node1");

      await BackgroundJobs.WaitAllAsync();

      //assert
      BitcoindFactory.AssertEqualAndClear(
        $"node1:addToPolicy/a,1||");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_2xFreezeUnfreezeProcessAsync()
    {
      //act
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));

      // first process
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 0));
      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      var order2Hash = await Utils.SubmitUnfreezeOrderAsync(Client,
        order1Hash,
        (1000, "A", 0));
      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      BitcoindFactory.AssertEqualAndClear(
       "node1:addToPolicy/a,0||",
       "node1:addToConsensus/a,0|100,|True",
       "node1:addToConsensus/a,0|100,200|True");

      // second process
      var order3Hash = await Utils.SubmitFreezeOrderAsync(Client, "somecourtorder2", (1000, "A", 0));

      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order3Hash, CourtOrderStatus.FreezeConsensus, 300);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      var order4Hash = await Utils.SubmitUnfreezeOrderAsync(Client, order3Hash, (1000, "A", 0));
      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order4Hash, CourtOrderStatus.UnfreezeConsensus, 400);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      //assert
      BitcoindFactory.AssertEqualAndClear(
        "node1:addToConsensus/a,0|100,200|False",
        "node1:addToConsensus/a,0|100,200;300,|True",
        "node1:addToConsensus/a,0|100,200;300,400|True");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_2xFreezeUnfreezeProcess_PartialUnfreezeAsync()
    {
      //act
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));

      // first process
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 0));
      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      var order2Hash = await Utils.SubmitUnfreezeOrderAsync(Client,
        order1Hash,
        (1000, "A", 0));
      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      BitcoindFactory.AssertEqualAndClear(
       "node1:addToPolicy/a,0||",
       "node1:addToConsensus/a,0|100,|True",
       "node1:addToConsensus/a,0|100,200|True");

      // second process
      var order3Hash = await Utils.SubmitFreezeOrderAsync(Client,
         (1000, "A", 0),
         (1000, "B", 1));

      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order3Hash, CourtOrderStatus.FreezeConsensus, 300);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      var order4Hash = await Utils.SubmitUnfreezeOrderAsync(Client,
        order3Hash,
        (1000, "A", 0));
      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order4Hash, CourtOrderStatus.UnfreezeConsensus, 400);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      var order5Hash = await Utils.SubmitUnfreezeOrderAsync(Client,
        order3Hash,
        (1000, "B", 1));
      await BackgroundJobs.WaitAllAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order5Hash, CourtOrderStatus.UnfreezeConsensus, 500);
      await BackgroundJobs.StartPropagateFundsStatesAsync();
      await BackgroundJobs.WaitAllAsync();

      //assert      
      BitcoindFactory.AssertEqualAndClear(
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
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 0),
        (1000, "B", 1));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      var order2Hash = await Utils.SubmitUnfreezeOrderAsync(Client,
        order1Hash,
        (1000, "A", 0),
        (1000, "B", 1));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await Nodes.CreateNodeAsync(new BMDomain.Node("node1", 0, "mocked", "mocked", null));
      await BackgroundJobs.WaitAllAsync();

      BitcoindFactory.AssertEqualAndClear(
       "node1:addToPolicy/a,0||/b,1||",
       "node1:addToConsensus/a,0|100,|True/a,0|100,200|True/b,1|100,|True/b,1|100,200|True");
    }

    [DataTestMethod]
    [DataRow(false, false, false, TEST_PRIVATE_KEY_WIF_ALT1, TEST_PUBLIC_KEY_ALT1, $"Public key '{TEST_PUBLIC_KEY_ALT1}' used to sign the court order is not trusted.")]
    public async Task RejectCourtOrderSignedWithNotTrustedKeyAsync(bool shouldPass, bool addToTrustlist, bool linkKeys, string privateKeyForSignature, string publicKey, string errorMessage)
    {
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 0),
        (1000, "B", 1));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      await CourtOrders.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      var unfreezeCourtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.UnfreezeOrder,
        CourtOrderId = "UnfreezeCO",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = order1Hash,
        Funds = new List<CourtOrderViewModelCreate.Fund>() { new CourtOrderViewModelCreate.Fund { TxOut = new API.Rest.ViewModels.TxOut { TxId = "A", Vout = 0}, Value = 1000 },
                                                             new CourtOrderViewModelCreate.Fund { TxOut = new API.Rest.ViewModels.TxOut { TxId = "B", Vout = 1}, Value = 1000 }},
        Blockchain = $"BSV-{NBitcoin.Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(unfreezeCourtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);

      if (addToTrustlist)
      {
        await TrustlistRepository.CreatePublicKeyAsync(publicKey, true, null);
      }
      if (linkKeys)
      {
        await TrustlistRepository.UpdatePublicKeyAsync(Utils.PublicKey, false, null, NBitcoin.Key.Parse(privateKeyForSignature, NBitcoin.Network.RegTest).PubKey.ToHex());
      }

      string signed = Common.SignatureTools.CreateJSONWithBitcoinSignature(payload, privateKeyForSignature, NBitcoin.Network.RegTest, true);
      var reqContent = Utils.JsonToStringContent(signed);
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      if (shouldPass)
      {
        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
      }
      else
      {
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        var error = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
        Assert.AreEqual(errorMessage, error.Detail);
      }
    }

    [DataTestMethod]
    [DataRow(true, true, true, TEST_PRIVATE_KEY_WIF_ALT1, TEST_PUBLIC_KEY_ALT1, null)]
    public async Task ProcessCourtOrderSignedWithReplacedTrustedKeyAsync(bool shouldPass, bool addToTrustlist, bool linkKeys, string privateKeyForSignature, string publicKey, string errorMessage)
    {
      await RejectCourtOrderSignedWithNotTrustedKeyAsync(shouldPass, addToTrustlist, linkKeys, privateKeyForSignature, publicKey, errorMessage);
    }

    [DataTestMethod]
    [DataRow(false, true, false, TEST_PRIVATE_KEY_WIF_ALT1, TEST_PUBLIC_KEY_ALT1, "Key that was used to sign court order does not belong to the trust chain which was used to sign referenced order.")]
    public async Task RejectCourtOrderSignedWithValidNotLinkedTrustedKeyAsync(bool shouldPass, bool addToTrustlist, bool linkKeys, string privateKeyForSignature, string publicKey, string errorMessage)
    {
      await RejectCourtOrderSignedWithNotTrustedKeyAsync(shouldPass, addToTrustlist, linkKeys, privateKeyForSignature, publicKey, errorMessage);
    }

    [DataTestMethod]
    [DataRow(false, true, true, TEST_PRIVATE_KEY_WIF, TEST_PUBLIC_KEY_ALT2, $"Public key '{TEST_PUBLIC_KEY}' used to sign the court order is not trusted.")]
    public async Task RejectCourtOrderSignedWithIncorrectTrustedKeyAsync(bool shouldPass, bool addToTrustlist, bool linkKeys, string privateKeyForSignature, string publicKey, string errorMessage)
    {
      await RejectCourtOrderSignedWithNotTrustedKeyAsync(shouldPass, addToTrustlist, linkKeys, privateKeyForSignature, publicKey, errorMessage);
    }
  }
}
