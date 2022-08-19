// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Test.Functional.Server;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using BlacklistManager.Domain.Models;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class CourtOrderRest : TestBase
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
    public async Task QueryCourtOrders_WrongId_ShouldReturn404Async()
    {
      //act
      var response = await Client.GetAsync(BlacklistManagerServer.Get.GetCourtOrder("XYZ", false));

      //assert
      Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task QueryCourtOrders_CheckReturnValuesAsync()
    {
      //arrange
      // see base.Initialize()

      // freeze for A,9 and B,10 
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (2000, "A", 9),
        (1000, "B", 10));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      var courtOrder = await Utils.QueryCourtOrderAsync(Client, order1Hash, false);
      Assert.AreEqual(order1Hash, courtOrder.CourtOrderHash);
      Assert.AreEqual(Common.SmartEnums.DocumentType.FreezeOrder, courtOrder.DocumentType);
      Assert.AreEqual("somecourtorderid", courtOrder.CourtOrderId);
      Assert.AreEqual(0, courtOrder.Funds.Count);

      // freeze for A,9 and C,11 
      var order2Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 9),
        (3000, "C", 11));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      // assert data
      var courtOrders = await Utils.QueryCourtOrdersAsync(Client, false);
      
      Assert.AreEqual(2, courtOrders.Count());
      var courtOrder1 = courtOrders.First(c => c.CourtOrderHash == order1Hash);
      Assert.IsNotNull(courtOrder1.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on freeze order 1 should have a value");
      Assert.IsNull(courtOrder1.ConsensusEnforcementStartedAtHeight);
      Assert.IsNull(courtOrder1.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(0, courtOrder1.RelatedOrders.Count());
      Assert.AreEqual(0, courtOrder1.Funds.Count());

      var courtOrder2 = courtOrders.First(c => c.CourtOrderHash == order2Hash);
      Assert.IsNotNull(courtOrder2.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on freeze order 2 should have a value");
      Assert.IsNull(courtOrder2.ConsensusEnforcementStartedAtHeight);
      Assert.IsNull(courtOrder2.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(0, courtOrder2.RelatedOrders.Count());
      Assert.AreEqual(0, courtOrder2.Funds.Count());

      // unfreeze for order1
      var unfreezeOrder1Hash = await Utils.SubmitUnfreezeOrderAsync(Client, order1Hash, new (long, string TxId, long vOut)[] { });
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      // assert data
      courtOrders = await Utils.QueryCourtOrdersAsync(Client, true);

      Assert.AreEqual(3, courtOrders.Count());
      courtOrder1 = courtOrders.First(c => c.CourtOrderHash == order1Hash);
      Assert.IsNotNull(courtOrder1.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on freeze order 1 should have a value");
      Assert.IsNull(courtOrder1.ConsensusEnforcementStartedAtHeight);
      Assert.IsNull(courtOrder1.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(unfreezeOrder1Hash, string.Join(",", courtOrder1.RelatedOrders));
      Assert.AreEqual(2, courtOrder1.Funds.Count());
      var fundA = courtOrder1.Funds.First(f => f.TxOut.TxId == "a");
      AssertExtension.AreEqual($"a,9|{order2Hash},,,;{order1Hash},{unfreezeOrder1Hash},,", fundA);
      var fundB = courtOrder1.Funds.First(f => f.TxOut.TxId == "b");
      AssertExtension.AreEqual($"b,10|{order1Hash},{unfreezeOrder1Hash},,", fundB);

      courtOrder2 = courtOrders.First(c => c.CourtOrderHash == order2Hash);
      Assert.IsNotNull(courtOrder2.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on freeze order 2 should have a value");
      Assert.IsNull(courtOrder2.ConsensusEnforcementStartedAtHeight);
      Assert.IsNull(courtOrder2.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(0, courtOrder2.RelatedOrders.Count());
      Assert.AreEqual(2, courtOrder2.Funds.Count());
      fundA = courtOrder2.Funds.First(f => f.TxOut.TxId == "a");
      AssertExtension.AreEqual($"a,9|{order2Hash},,,;{order1Hash},{unfreezeOrder1Hash},,", fundA);
      var fundC = courtOrder2.Funds.First(f => f.TxOut.TxId == "c");
      AssertExtension.AreEqual($"c,11|{order2Hash},,,", fundC);

      var unfreezeCourtOrder1 = courtOrders.First(c => c.CourtOrderHash == unfreezeOrder1Hash);
      Assert.IsNotNull(unfreezeCourtOrder1.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on unfreeze order 1 should have a value");
      Assert.IsNull(unfreezeCourtOrder1.ConsensusEnforcementStartedAtHeight);
      Assert.IsNull(unfreezeCourtOrder1.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(order1Hash, string.Join(",", unfreezeCourtOrder1.RelatedOrders));
      Assert.AreEqual(2, unfreezeCourtOrder1.Funds.Count());
      fundA = unfreezeCourtOrder1.Funds.First(f => f.TxOut.TxId == "a");
      AssertExtension.AreEqual($"a,9|{order2Hash},,,;{order1Hash},{unfreezeOrder1Hash},,", fundA);
      fundB = unfreezeCourtOrder1.Funds.First(f => f.TxOut.TxId == "b");
      AssertExtension.AreEqual($"b,10|{order1Hash},{unfreezeOrder1Hash},,", fundB);

      // consensus for freeze order1
      await base.CourtOrders.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      // consensus for unfreeze order1
      await base.CourtOrders.SetCourtOrderStatusAsync(unfreezeOrder1Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      // assert data
      courtOrders = await Utils.QueryCourtOrdersAsync(Client, true);

      Assert.AreEqual(3, courtOrders.Count());
      courtOrder1 = courtOrders.First(c => c.CourtOrderHash == order1Hash);
      Assert.IsNotNull(courtOrder1.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on freeze order 1 should have a value");
      Assert.AreEqual(100, courtOrder1.ConsensusEnforcementStartedAtHeight);
      Assert.AreEqual(200, courtOrder1.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(unfreezeOrder1Hash, string.Join(",", courtOrder1.RelatedOrders));
      Assert.AreEqual(2, courtOrder1.Funds.Count());
      fundA = courtOrder1.Funds.First(f => f.TxOut.TxId == "a");
      AssertExtension.AreEqual($"a,9|{order2Hash},,,;{order1Hash},{unfreezeOrder1Hash},100,200", fundA);
      fundB = courtOrder1.Funds.First(f => f.TxOut.TxId == "b");
      AssertExtension.AreEqual($"b,10|{order1Hash},{unfreezeOrder1Hash},100,200", fundB);

      courtOrder2 = courtOrders.First(c => c.CourtOrderHash == order2Hash);
      Assert.IsNotNull(courtOrder2.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on freeze order 2 should have a value");
      Assert.IsNull(courtOrder2.ConsensusEnforcementStartedAtHeight);
      Assert.IsNull(courtOrder2.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(0, courtOrder2.RelatedOrders.Count());
      Assert.AreEqual(2, courtOrder2.Funds.Count());
      fundA = courtOrder2.Funds.First(f => f.TxOut.TxId == "a");
      AssertExtension.AreEqual($"a,9|{order2Hash},,,;{order1Hash},{unfreezeOrder1Hash},100,200", fundA);
      fundC = courtOrder2.Funds.First(f => f.TxOut.TxId == "c");
      AssertExtension.AreEqual($"c,11|{order2Hash},,,", fundC);

      unfreezeCourtOrder1 = courtOrders.First(c => c.CourtOrderHash == unfreezeOrder1Hash);
      Assert.IsNotNull(unfreezeCourtOrder1.PolicyEnforcementStartedAt, "PolicyEnforcementStartedAt on unfreeze order 1 should have a value");
      Assert.AreEqual(200, unfreezeCourtOrder1.ConsensusEnforcementStartedAtHeight);
      Assert.IsNull(unfreezeCourtOrder1.ConsensusEnforcementStoppedAtHeight);
      Assert.AreEqual(order1Hash, string.Join(",", unfreezeCourtOrder1.RelatedOrders));
      Assert.AreEqual(2, unfreezeCourtOrder1.Funds.Count());
      fundA = unfreezeCourtOrder1.Funds.First(f => f.TxOut.TxId == "a");
      AssertExtension.AreEqual($"a,9|{order2Hash},,,;{order1Hash},{unfreezeOrder1Hash},100,200", fundA);
      fundB = unfreezeCourtOrder1.Funds.First(f => f.TxOut.TxId == "b");
      AssertExtension.AreEqual($"b,10|{order1Hash},{unfreezeOrder1Hash},100,200", fundB);
    }

  }
}
