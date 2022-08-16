// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class CourtOrderRepository : TestBase
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(mockedServices : true);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      base.Cleanup();
    }

    [TestMethod]
    public async Task CourtOrderRepository_TestFundStatusForMultipleOrdersAsync()
    {
      // See spec, chapter 8.5	Handling funds affected by multiple court orders for details

      var repo = CourtOrderRepository;

      // #2 Freeze order1 arrives, requiring freeze of A and its children:
      var order1 = new CourtOrder("order1", "H1", Common.SmartEnums.DocumentType.FreezeOrder);
      order1.AddFunds(
        new[]
        {
          new TxOut("A", 1),
          new TxOut("A1", 1),
          new TxOut("A2", 1),
          new TxOut("B2", 1),
          new TxOut("B3", 1),
        }
      );
      await repo.InsertCourtOrderAsync(order1, null, null);
      await repo.SetCourtOrderStatusAsync(order1.CourtOrderHash, CourtOrderStatus.FreezePolicy, null);


      // #3 Freeze order2 arrives, requiring freeze of C and its children:
      var order2 = new CourtOrder("order2", "H2", Common.SmartEnums.DocumentType.FreezeOrder);
      order2.AddFunds(
        new[]
        {
          new TxOut("C", 1),
          new TxOut("C1", 1),
          new TxOut("C2", 1),
          new TxOut("B2", 1),
          new TxOut("B3", 1),
        }
      );


      await repo.InsertCourtOrderAsync(order2, null, null);
      await repo.SetCourtOrderStatusAsync(order2.CourtOrderHash, CourtOrderStatus.FreezePolicy, null);


      // #4: An unfreeze order for all fund from order1 arrives. Unfreeze order does not need to reach the consensus
      var order1Unfreeze = new CourtOrder("order1Unfreeze", "UH1", Common.SmartEnums.DocumentType.UnfreezeOrder, null, null, "order1", order1.CourtOrderHash);
      
      await repo.InsertCourtOrderAsync(order1Unfreeze, null, null);
      await repo.SetCourtOrderStatusAsync(order1Unfreeze.CourtOrderHash, CourtOrderStatus.UnfreezeNoConsensusYet, null);


      // #5:  Court order 3 arrives, requiring freeze of A0 and its children
      var order3 = new CourtOrder("order3", "H3", Common.SmartEnums.DocumentType.FreezeOrder);
      order3.AddFunds(
        new[]
        {
          new TxOut("A0", 1),
          new TxOut("A1", 1),
          new TxOut("A2", 1),
          new TxOut("B2", 1),
          new TxOut("B3", 1),
        }
      );

      await repo.InsertCourtOrderAsync(order3, null, null);
      await repo.SetCourtOrderStatusAsync(order3.CourtOrderHash, CourtOrderStatus.FreezePolicy, null);


      // #6 Miner reach consensus to start enforcing consensus freeze for order 2 (containing C and its children)
      await repo.SetCourtOrderStatusAsync(order2.CourtOrderHash, CourtOrderStatus.FreezeConsensus, 100);

      var funds = repo.GetFunds().ToArray();
      Assert.AreEqual(9, funds.Count(), "Wrong funds count");
      AssertExtension.AreEqual($"a,1|H1,-1,-1,True", funds[0]);
      AssertExtension.AreEqual($"a1,1|H1,-1,-1,True;H3,-1,-1,False", funds[1]);
      AssertExtension.AreEqual($"a2,1|H1,-1,-1,True;H3,-1,-1,False", funds[2]);
      AssertExtension.AreEqual($"b2,1|H1,-1,-1,True;H2,100,-1,False;H3,-1,-1,False", funds[3]);
      AssertExtension.AreEqual($"b3,1|H1,-1,-1,True;H2,100,-1,False;H3,-1,-1,False", funds[4]);
      AssertExtension.AreEqual($"c,1|H2,100,-1,False", funds[5]);
      AssertExtension.AreEqual($"c1,1|H2,100,-1,False", funds[6]);
      AssertExtension.AreEqual($"c2,1|H2,100,-1,False", funds[7]);
      AssertExtension.AreEqual($"a0,1|H3,-1,-1,False", funds[8]);


      // #7 Unfreeze order arrives, referencing freeze order2. It requires unfreeze of C2 and B3, while keeping the rest (C, C1, B2) frozen. Miners reach consensus.
      var order2Unfreeze = new CourtOrder("order2Unfreeze", "UH2", Common.SmartEnums.DocumentType.UnfreezeOrder, null, null, order2.CourtOrderId, order2.CourtOrderHash);
      order2Unfreeze.AddFunds(
        new[]
        {
          new TxOut("C2", 1),
          new TxOut("B3", 1),
        }
      );

      await repo.InsertCourtOrderAsync(order2Unfreeze, null, null);
      await repo.SetCourtOrderStatusAsync(order2Unfreeze.CourtOrderHash, CourtOrderStatus.UnfreezeNoConsensusYet, null);
      await repo.SetCourtOrderStatusAsync(order2Unfreeze.CourtOrderHash, CourtOrderStatus.UnfreezeConsensus, 200);
      
      funds = repo.GetFunds().ToArray();
      Assert.AreEqual(9, funds.Count(), "Wrong funds count");
      AssertExtension.AreEqual($"a,1|H1,-1,-1,True", funds[0]);
      AssertExtension.AreEqual($"a1,1|H1,-1,-1,True;H3,-1,-1,False", funds[1]);
      AssertExtension.AreEqual($"a2,1|H1,-1,-1,True;H3,-1,-1,False", funds[2]);
      AssertExtension.AreEqual($"b2,1|H1,-1,-1,True;H2,100,-1,False;H3,-1,-1,False", funds[3]);
      AssertExtension.AreEqual($"b3,1|H1,-1,-1,True;H2,100,200,True;H3,-1,-1,False", funds[4]);
      AssertExtension.AreEqual($"c,1|H2,100,-1,False", funds[5]);
      AssertExtension.AreEqual($"c1,1|H2,100,-1,False", funds[6]);
      AssertExtension.AreEqual($"c2,1|H2,100,200,True", funds[7]);
      AssertExtension.AreEqual($"a0,1|H3,-1,-1,False", funds[8]);


      // #8 Unfreeze order for all funds from Order3 arrives.
      var order3Unfreeze = new CourtOrder("order3Unfreeze", "UH3", Common.SmartEnums.DocumentType.UnfreezeOrder, null, null, "order3", order3.CourtOrderHash);

      await repo.InsertCourtOrderAsync(order3Unfreeze, null, null);
      await repo.SetCourtOrderStatusAsync(order3Unfreeze.CourtOrderHash, CourtOrderStatus.UnfreezeNoConsensusYet, null);

      funds = repo.GetFunds().ToArray();
      Assert.AreEqual(9, funds.Count(), "Wrong funds count");
      AssertExtension.AreEqual($"a,1|H1,-1,-1,True", funds[0]);
      AssertExtension.AreEqual($"a1,1|H1,-1,-1,True;H3,-1,-1,True", funds[1]);
      AssertExtension.AreEqual($"a2,1|H1,-1,-1,True;H3,-1,-1,True", funds[2]);
      AssertExtension.AreEqual($"b2,1|H1,-1,-1,True;H2,100,-1,False;H3,-1,-1,True", funds[3]);
      AssertExtension.AreEqual($"b3,1|H1,-1,-1,True;H2,100,200,True;H3,-1,-1,True", funds[4]);
      AssertExtension.AreEqual($"c,1|H2,100,-1,False", funds[5]);
      AssertExtension.AreEqual($"c1,1|H2,100,-1,False", funds[6]);
      AssertExtension.AreEqual($"c2,1|H2,100,200,True", funds[7]);
      AssertExtension.AreEqual($"a0,1|H3,-1,-1,True", funds[8]);
    }

    [TestMethod]
    public async Task CourtOrderRepository_TestUnfreezeOrderWithoutFunds_ShouldUnfreezeAllAsync()
    {
      //arrange
      var repo = CourtOrderRepository;
      var order1 = new CourtOrder("order1", "H1", Common.SmartEnums.DocumentType.FreezeOrder);
      order1.AddFunds(
        new[]
        {
          new TxOut("A", 0),
          new TxOut("B", 1),
          new TxOut("C", 2),
        }
      );
      await repo.InsertCourtOrderAsync(order1, null, null);
      await repo.SetCourtOrderStatusAsync(order1.CourtOrderHash, CourtOrderStatus.FreezePolicy, null);
      await repo.SetCourtOrderStatusAsync(order1.CourtOrderHash, CourtOrderStatus.FreezeConsensus, 100);
      var dbOrders = await repo.GetCourtOrdersAsync(order1.CourtOrderHash, false);
      var dbOrder = dbOrders.Single();

      //act
      var order2 = new CourtOrder("order2", "H2", Common.SmartEnums.DocumentType.UnfreezeOrder, null, null, dbOrder.CourtOrderId, dbOrder.CourtOrderHash);
      await repo.InsertCourtOrderAsync(order2, null, null);
      await repo.SetCourtOrderStatusAsync(order2.CourtOrderHash, CourtOrderStatus.UnfreezeNoConsensusYet, null);
      await repo.SetCourtOrderStatusAsync(order2.CourtOrderHash, CourtOrderStatus.UnfreezeConsensus, 200);


      //assert
      var funds = repo.GetFunds().ToArray();
      Assert.AreEqual(3, funds.Count(), "Wrong funds count");
      AssertExtension.AreEqual($"a,0|H1,100,200,True", funds[0]);
      AssertExtension.AreEqual($"b,1|H1,100,200,True", funds[1]);
      AssertExtension.AreEqual($"c,2|H1,100,200,True", funds[2]);
    }

    [TestMethod]
    public async Task CourtOrderRepository_GetFundStateToPropagateAsync()
    {
      //arrange
      var repo = CourtOrderRepository;
      var node1 = new Node("host1", 0, "u", "p", null);
      node1 = NodeRepository.CreateNode(node1);

      var node2 = new Node("host2", 0, "u", "p", null);
      node2 = NodeRepository.CreateNode(node2);

      var order1 = new CourtOrder("order1", "H1", Common.SmartEnums.DocumentType.FreezeOrder);
      order1.AddFunds(
        new[]
        {
          new TxOut("A1", 1),
          new TxOut("A2", 1)
        }
      );

      //act & assert
      // import order
      await repo.InsertCourtOrderAsync(order1, null, null);

      var fsp = await repo.GetFundStateToPropagateAsync();
      Assert.AreEqual(0, fsp.Count(), "No propagation should be requested for imported order");

      // activate order
      await repo.SetCourtOrderStatusAsync(order1.CourtOrderHash, CourtOrderStatus.FreezePolicy, null);

      fsp = await repo.GetFundStateToPropagateAsync();
      Assert.AreEqual(4, fsp.Count(), "All fund states should be propagated for activated order");

      // propagate first three fund states
      var propagationList = new List<FundStatePropagated>();
      for (int i = 0; i < 3; i++)
      {
        propagationList.Add(new FundStatePropagated(fsp.ElementAt(i), fsp.ElementAt(i).NodeId == 1 ? node1 : node2, DateTime.UtcNow));
      }
      repo.InsertFundStateNode(propagationList);

      fsp = await repo.GetFundStateToPropagateAsync();
      Assert.AreEqual(1, fsp.Count(), "Only one fund state is expected for propagation");
      AssertExtension.AreEqual("2|a2,1|H1,-1,-1,False|", fsp.First());

      // activate order
      await repo.SetCourtOrderStatusAsync(order1.CourtOrderHash, CourtOrderStatus.FreezeConsensus, 100);

      fsp = await repo.GetFundStateToPropagateAsync();
      Assert.AreEqual(5, fsp.Count(), "Wrong number of expected propagations");
      var p = fsp.ToArray();
      AssertExtension.AreEqual("1|a1,1|H1,100,-1,False|H1,-1,-1,False", p[0]);
      AssertExtension.AreEqual("1|a2,1|H1,100,-1,False|H1,-1,-1,False", p[1]);
      AssertExtension.AreEqual("2|a2,1|H1,-1,-1,False|",  p[2]);
      AssertExtension.AreEqual("2|a1,1|H1,100,-1,False|H1,-1,-1,False", p[3]);
      AssertExtension.AreEqual("2|a2,1|H1,100,-1,False|H1,-1,-1,False", p[4]);
    }

  }
}
