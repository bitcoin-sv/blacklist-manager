// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServiceViewModel;
using BlacklistManager.Domain.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DocumentType = Common.SmartEnums.DocumentType;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class ProcessConsensusActivation : TestBase
  {
    LegalEntityEndpoint _legalEntity;
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(mockedServices: true, addValidDelegatedKey: true);
      // This key is needed when adding NT endpoint
      await TrustlistRepository.CreatePublicKeyAsync("0293ff7c31eaa93ce4701a462676c1e46dac745f6848097f57357d2a414b379a34", true, null);

      //arrange
      LegalEntityFactory.SetCourtOrderResponse(new CourtOrdersViewModel() { CourtOrders = new List<SignedPayloadViewModel>() });
      _legalEntity = await LegalEndpoints.CreateAsync(System.Guid.NewGuid().ToString(), "apiKey1");
      await Nodes.CreateNodeAsync(new Node("host", 0, "mocked", "mocked", null));
    }

    [TestCleanup]
    public async Task TestCleanupAsync()
    {
      await base.CleanupAsync();
    }

    [TestMethod]
    public async Task ProcessConsensusActivation_ImportFreezeAndUnfreezeCourtOrder_ShouldSetEnforceAtHeightAsync()
    {
      //act & assert
      // import freeze court order
      var freezeOrder1 = new Domain.Models.CourtOrder
      {
        CourtOrderHash = "h1",
        CourtOrderId = "id1",
        DocumentType = DocumentType.FreezeOrder,
        Blockchain = $"BSV-{Network.RegTest.Name}",
        SignedDate = DateTime.UtcNow
      };
      freezeOrder1.AddFund(new Domain.Models.TxOut("A", 0), 1000L);
      var result = await CourtOrders.ProcessCourtOrderAsync(new Common.JsonEnvelope { PublicKey = "key"}, freezeOrder1, _legalEntity.LegalEntityEndpointId);
      var freezeOrder1Hash = result.CourtOrderHash;

      await BackgroundJobs.StartProcessCourtOrdersAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_COURTORDERS);
      
      await BackgroundJobs.StartProcessCourtOrderAcceptancesAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_ACCEPTANCES);

      // setup consensus activation response
      var ca = new Domain.Models.ConsensusActivation("mock", freezeOrder1Hash, 100, "mock", DateTime.UtcNow, "mockFreeze");
      LegalEntityFactory.SetConsensusActivationResponse(ca, freezeOrder1Hash);

      await BackgroundJobs.StartGetConsensusActivationsAsync();
      await Task.Delay(1000);
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_CONSENSUS_ACTIVATIONS);

      // assert enforceAtHeight was set on freeze order
      var cos = await CourtOrderRepository.GetCourtOrdersAsync(freezeOrder1Hash, true);
      var co = cos.First();
      Assert.AreEqual(100, co.EnforceAtHeight);
      Assert.AreEqual(CourtOrderStatus.FreezeConsensus, co.Status);

      // import unFreeze court order for freeze court order 1
      var unFreezeOrder1 = new Domain.Models.CourtOrder
      {
        CourtOrderId = "id2",
        CourtOrderHash = "h2",
        DocumentType = DocumentType.UnfreezeOrder,
        FreezeCourtOrderId = freezeOrder1.CourtOrderId,
        FreezeCourtOrderHash = freezeOrder1Hash,
        Blockchain = $"BSV-{Network.RegTest.Name}",
        SignedDate = DateTime.UtcNow
      };
      unFreezeOrder1.AddFund(new Domain.Models.TxOut("A", 0), 1000L);
      result = await CourtOrders.ProcessCourtOrderAsync(new Common.JsonEnvelope { PublicKey = "key" }, unFreezeOrder1, _legalEntity.LegalEntityEndpointId);
      var unFreezeOrder1Hash = result.CourtOrderHash;
      await BackgroundJobs.StartProcessCourtOrdersAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_COURTORDERS);

      await BackgroundJobs.StartProcessCourtOrderAcceptancesAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_ACCEPTANCES);

      // setup consensus activation response
      ca = new ConsensusActivation("mock", unFreezeOrder1Hash, 111, "mock", DateTime.UtcNow, "mockUnfreeze");
      LegalEntityFactory.SetConsensusActivationResponse(ca, unFreezeOrder1Hash);

      await Task.Delay(1000);
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_CONSENSUS_ACTIVATIONS);
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROPAGATE_FUNDS);

      // assert enforceAtHeight was set on unfreeze order 1
      cos = await CourtOrderRepository.GetCourtOrdersAsync(unFreezeOrder1Hash, true);
      co = cos.First();
      Assert.AreEqual(111, co.EnforceAtHeight);
      Assert.AreEqual(CourtOrderStatus.UnfreezeConsensus, co.Status);

      // assert bitcoind RPC calls
      BitcoindFactory.AssertEqualAndClear(
        "host:addToPolicy/a,0||",
        "host:addToConsensus/a,0|100,|True",
        "host:addToConsensus/a,0|100,111|True");
    }

    [TestMethod]
    public async Task ProcessConsensusActivation4ConfiscationAsync()
    {
      // import freeze court order
      var freezeOrder1 = new Domain.Models.CourtOrder
      {
        CourtOrderId = "id1",
        CourtOrderHash = "h1",
        DocumentType = DocumentType.FreezeOrder,
        Blockchain = $"BSV-{Network.RegTest.Name}",
        SignedDate = DateTime.UtcNow
      };

      freezeOrder1.AddFund(new Domain.Models.TxOut("A", 0), 1000L);
      var result = await CourtOrders.ProcessCourtOrderAsync(new Common.JsonEnvelope { PublicKey = "key" }, freezeOrder1, _legalEntity.LegalEntityEndpointId);
      var freezeOrder1Hash = result.CourtOrderHash;

      await BackgroundJobs.StartProcessCourtOrdersAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_COURTORDERS);
      
      await BackgroundJobs.StartProcessCourtOrderAcceptancesAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_ACCEPTANCES);

      // setup consensus activation response
      var ca = new ConsensusActivation("mock", freezeOrder1Hash, 2, "mock", DateTime.UtcNow, "mockFreeze");
      LegalEntityFactory.SetConsensusActivationResponse(ca, freezeOrder1Hash);

      await BackgroundJobs.StartGetConsensusActivationsAsync();
      await Task.Delay(1000);
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_CONSENSUS_ACTIVATIONS);
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROPAGATE_FUNDS);

      // assert enforceAtHeight was set on freeze order
      var cos = await CourtOrderRepository.GetCourtOrdersAsync(freezeOrder1Hash, true);
      var co = cos.First();
      Assert.AreEqual(2, co.EnforceAtHeight);
      Assert.AreEqual(CourtOrderStatus.FreezeConsensus, co.Status);

      var confiscationOrder = new Domain.Models.CourtOrder
      {
        CourtOrderId = "id2",
        CourtOrderHash = "h2",
        DocumentType = DocumentType.ConfiscationOrder,
        FreezeCourtOrderId = freezeOrder1.CourtOrderId,
        FreezeCourtOrderHash = freezeOrder1.CourtOrderHash,
        Blockchain = $"BSV-{Network.RegTest.Name}",
        SignedDate = DateTime.UtcNow
      };
      confiscationOrder.AddFund(new Domain.Models.TxOut("A", 0), 1000L);
      result = await CourtOrders.ProcessCourtOrderAsync(new Common.JsonEnvelope { PublicKey = "key" }, confiscationOrder, _legalEntity.LegalEntityEndpointId);
      var confiscationOrderHash = result.CourtOrderHash;
      await BackgroundJobs.StartProcessCourtOrdersAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_COURTORDERS);

      await BackgroundJobs.StartProcessCourtOrderAcceptancesAsync();
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_ACCEPTANCES);


      // setup consensus activation response
      var confiscationCA = new ConsensusActivation("mockConfiscation", confiscationOrderHash, 10, "mock", DateTime.UtcNow, "mockConfiscation");
      LegalEntityFactory.SetConsensusActivationResponse(confiscationCA, confiscationOrderHash);

      await Task.Delay(1000);
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROCESS_CONSENSUS_ACTIVATIONS);
      await WaitBackgrounJobUntilCompletedAsync(Infrastructure.BackgroundJobs.BackgroundJobs.PROPAGATE_FUNDS);

      // assert enforceAtHeight was set on freeze order
      var confiscationCOS = await CourtOrderRepository.GetCourtOrdersAsync(confiscationOrderHash, true);
      var confiscationCO = confiscationCOS.First();
      Assert.AreEqual(10, confiscationCO.EnforceAtHeight);
      Assert.AreEqual(CourtOrderStatus.ConfiscationConsensus, confiscationCO.Status);
    }
  }
}
