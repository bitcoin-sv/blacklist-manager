// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServiceViewModel;
using BlacklistManager.Domain.Models;
using Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DocumentType = Common.SmartEnums.DocumentType;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class ProcessConsensusActivation : TestBase
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(mockedServices: true, addValidDelegatedKey: true);
      // This key is needed when adding NT endpoint
      TrustlistRepository.CreatePublicKey("0293ff7c31eaa93ce4701a462676c1e46dac745f6848097f57357d2a414b379a34", true, null);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      base.Cleanup();
    }

    [TestMethod]
    public async Task ProcessConsensusActivation_ImportFreezeAndUnfreezeCourtOrder_ShouldSetEnforceAtHeightAsync()
    {
      //arrange
      legalEntityFactory.SetCourtOrderResponse(new CourtOrdersViewModel() { CourtOrders = new List<SignedPayloadViewModel>() });
      var legalEntity = await domainLogic.CreateLegalEntityEndpointAsync("url1", "apiKey1");
      await domainLogic.CreateNodeAsync(new Node("host", 0, "mocked", "mocked", null));

      //act & assert
      // import freeze court order
      var freezeOrder1 = new Domain.Models.CourtOrder("id1", "h1", DocumentType.FreezeOrder);
      freezeOrder1.AddFund(new Domain.Models.TxOut("A", 0));
      var result = await domainLogic.ProcessCourtOrderAsync("", freezeOrder1, legalEntity.LegalEntityEndpointId, true);
      var freezeOrder1Hash = result.CourtOrderHash;

      // setup consensus activation response
      var ca = new Domain.Models.ConsensusActivation("mock", freezeOrder1Hash, 100, "mock", "mock");
      legalEntityFactory.SetConsensusActivationResponse(ca, freezeOrder1Hash);

      await backgroundJobs.WaitAllAsync();

      // assert enforceAtHeight was set on freeze order
      var cos = await CourtOrderRepository.GetCourtOrdersAsync(freezeOrder1Hash, true);
      var co = cos.First();
      Assert.AreEqual(100, co.EnforceAtHeight);
      Assert.AreEqual(CourtOrderStatus.FreezeConsensus, co.Status);

      // import unFreeze court order for freeze court order 1
      var unFreezeOrder1 = new Domain.Models.CourtOrder("id2", "h2", DocumentType.UnfreezeOrder, null, null, freezeOrder1.CourtOrderId, freezeOrder1Hash);
      unFreezeOrder1.AddFund(new Domain.Models.TxOut("A", 0));
      result = await domainLogic.ProcessCourtOrderAsync("", unFreezeOrder1, legalEntity.LegalEntityEndpointId, true);
      var unFreezeOrder1Hash = result.CourtOrderHash;

      // setup consensus activation response
      ca = new Domain.Models.ConsensusActivation("mock", unFreezeOrder1Hash, 111, "mock", "mock");
      legalEntityFactory.SetConsensusActivationResponse(ca, unFreezeOrder1Hash);

      await backgroundJobs.WaitAllAsync();

      // assert enforceAtHeight was set on unfreeze order 1
      cos = await CourtOrderRepository.GetCourtOrdersAsync(unFreezeOrder1Hash, true);
      co = cos.First();
      Assert.AreEqual(111, co.EnforceAtHeight);
      Assert.AreEqual(CourtOrderStatus.UnfreezeConsensus, co.Status);

      // assert bitcoind RPC calls
      bitcoindFactory.AssertEqualAndClear(
        "host:addToPolicy/a,0||",
        "host:addToConsensus/a,0|100,|True",
        "host:addToConsensus/a,0|100,111|True");
    }
  }
}
