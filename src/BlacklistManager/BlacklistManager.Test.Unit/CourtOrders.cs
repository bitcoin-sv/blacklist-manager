// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Test.Unit.Mocks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Unit
{
  [TestClass]
  public class CourtOrders
  {
    [TestMethod]
    public async Task ProcessConsensusActivation_OnError_ShouldReturnNotSuccessfulAsync()
    {
      // arrange
      var courtOrderRepository = new Mock<ICourtOrderRepository>();
      courtOrderRepository.Setup(x => x.GetPendingConsensusActivationsAsync()).Throws(new Exception("error in db"));

      var c = InitCourtOrders(
        courtOrderRepository: courtOrderRepository.Object);

      var cancellationSource = new CancellationTokenSource();

      //act
      var result = await c.ProcessConsensusActivationsAsync(cancellationSource.Token);

      //assert
      Assert.IsFalse(result.WasSuccessful);
    }

    [TestMethod]
    public async Task ProcessConsensusActivation_OneFail_ShouldProcessOthersAsync()
    {
      // arrange

      // setup court order repository
      var courtOrderRepository = new Mock<ICourtOrderRepository>();
      var pcs = new List<PendingConsensusActivation>() {
        new PendingConsensusActivation() { CourtOrderHash = "hash1"},
        new PendingConsensusActivation() { CourtOrderHash = "hash2"}
      }.Select(x => x);
      courtOrderRepository.Setup(x => x.GetPendingConsensusActivationsAsync()).Returns(Task.FromResult(pcs));
      var cos = new List<CourtOrder>() {
        new CourtOrder((int)CourtOrderType.Freeze, "id", null, null, "hash2", null, (int)CourtOrderStatus.FreezePolicy, null, null)
      }.Select(x => x);
      courtOrderRepository.Setup(x => x.GetCourtOrdersAsync("hash2", false)).Returns(Task.FromResult(cos));

      // setup legal entity endpoint
      var legalEntity = new Mock<ILegalEntity>();
      // req for hash1 throws exception
      legalEntity.Setup(x => x.GetConsensusActivationAsync("hash1")).Throws(new Exception("error getting data"));
      // req for hash2 returns successfully
      var ca = new ConsensusActivation("", "hash2", 1, "", "");
      legalEntity.Setup(x => x.GetConsensusActivationAsync("hash2")).Returns(Task.FromResult(ca));
      var legalEntityFactory = new LegalEntityFactoryMock(legalEntity.Object);

      var c = InitCourtOrders(
        courtOrderRepository: courtOrderRepository.Object,
        legalEntityFactory: legalEntityFactory);

      var cancellationSource = new CancellationTokenSource();

      //act
      var result = await c.ProcessConsensusActivationsAsync(cancellationSource.Token);

      //assert
      Assert.AreEqual(1, result.Processed);
      Assert.IsTrue(result.AnyConsensusActivationsStillPending);
    }

    private Domain.Models.CourtOrders InitCourtOrders(
      ICourtOrderRepository courtOrderRepository = null,
      INodeRepository nodeRepository = null,
      IBackgroundJobs backgroundJobs = null,
      IFundPropagatorFactory fundPropagatorFactory = null,
      ILegalEntityFactory legalEntityFactory = null,
      IConsensusActivationValidatorFactory consensusActivationValidatorFactory = null)
    {
      if (courtOrderRepository == null)
      {
        courtOrderRepository = new Mock<ICourtOrderRepository>().Object;
      }
      if (nodeRepository == null)
      {
        nodeRepository = new Mock<INodeRepository>().Object;
      }
      if (backgroundJobs == null)
      {
        backgroundJobs = new Mock<IBackgroundJobs>().Object;
      }
      if (fundPropagatorFactory == null)
      {
        fundPropagatorFactory = new Mock<IFundPropagatorFactory>().Object;
      }
      if (legalEntityFactory == null)
      {
        legalEntityFactory = new LegalEntityFactoryMock(null);
      }
      if (consensusActivationValidatorFactory == null)
      {
        consensusActivationValidatorFactory = new ConsensusActivationValidatorFactoryMock(true);
      }
      var logger = new NullLoggerFactory();

      return new Domain.Models.CourtOrders(
        courtOrderRepository,
        nodeRepository,
        backgroundJobs,
        fundPropagatorFactory,
        legalEntityFactory,
        logger,
        consensusActivationValidatorFactory);
    }
  }
}
