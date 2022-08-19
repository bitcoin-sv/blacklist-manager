// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain;
using BlacklistManager.Domain.Actions;
using BlacklistManager.Domain.BackgroundJobs;
using BlacklistManager.Domain.ExternalServices;
using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Actions;
using BlacklistManager.Test.Unit.Mocks;
using Common.Bitcoin;
using Common.SmartEnums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Unit
{
  [TestClass]
  public class CourtOrdersTests
  {
    [TestMethod]
    public async Task ProcessConsensusActivation_OnError_ShouldReturnNotSuccessfulAsync()
    {
      // arrange
      var courtOrderRepository = new Mock<ICourtOrderRepository>();
      courtOrderRepository.Setup(x => x.GetPendingConsensusActivationsAsync(100, 1)).Throws(new Exception("error in db"));

      var c = InitCourtOrders(
        courtOrderRepository: courtOrderRepository.Object);

      var cancellationSource = new CancellationTokenSource();

      //act
      var result = await c.GetConsensusActivationsAsync(cancellationSource.Token);

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
        new PendingConsensusActivation() { CourtOrderHash = "hash1", CourtOrderTypeId = CourtOrderType.Freeze },
        new PendingConsensusActivation() { CourtOrderHash = "hash2", CourtOrderTypeId = CourtOrderType.Freeze }
      }.Select(x => x);
      courtOrderRepository.Setup(x => x.GetPendingConsensusActivationsAsync(100, 1)).Returns(Task.FromResult(pcs));
      var cos = new List<CourtOrder>() {
        new CourtOrder
        {
          Type = CourtOrderType.Freeze,
          CourtOrderId = "id",
          CourtOrderHash = "hash2",
          Status = CourtOrderStatus.FreezePolicy,
          Blockchain = $"BSV-{Network.RegTest.Name}"
        }
      }.Select(x => x);
      courtOrderRepository.Setup(x => x.GetCourtOrdersAsync("hash2", false)).Returns(Task.FromResult(cos));

      var cancellationSource = new CancellationTokenSource();
      // setup legal entity endpoint
      var legalEntity = new Mock<ILegalEntity>();
      // req for hash1 throws exception
      legalEntity.Setup(x => x.GetConsensusActivationAsync("hash1", cancellationSource.Token)).Throws(new Exception("error getting data"));
      // req for hash2 returns successfully
      var ca = new ConsensusActivation("", "hash2", 1, "", DateTime.UtcNow, "");
      legalEntity.Setup(x => x.GetConsensusActivationAsync("hash2", cancellationSource.Token)).Returns(Task.FromResult(ca));
      var legalEntityFactory = new LegalEntityFactoryMock(legalEntity.Object);

      var c = InitCourtOrders(
        courtOrderRepository: courtOrderRepository.Object,
        legalEntityFactory: legalEntityFactory);

      //act
      var result = await c.GetConsensusActivationsAsync(cancellationSource.Token);

      //assert
      Assert.AreEqual(1, result.Processed);
      Assert.IsTrue(result.AnyConsensusActivationsStillPending);
    }

    private CourtOrders InitCourtOrders(
      ICourtOrderRepository courtOrderRepository = null,
      INodeRepository nodeRepository = null,
      ITrustListRepository trustListRepository = null,
      IBackgroundJobs backgroundJobs = null,
      ILegalEntityFactory legalEntityFactory = null,
      IConsensusActivationValidatorFactory consensusActivationValidatorFactory = null,
      ILegalEndpoints legalEndpointsService = null,
      IBitcoinFactory bitcoindFactory = null,
      IDelegatedKeys delegatedKeys = null,
      IConfigurationParams configurationParams = null,
      IOptions<AppSettings> options = null)
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
      if (legalEndpointsService == null)
      {
        legalEndpointsService = new Mock<ILegalEndpoints>().Object;
      }
      if (bitcoindFactory == null)
      {
        bitcoindFactory = new Mock<IBitcoinFactory>().Object;
      }
      if (delegatedKeys == null)
      {
        delegatedKeys = new Mock<IDelegatedKeys>().Object;
      }
      if (trustListRepository == null)
      {
        trustListRepository = new Mock<ITrustListRepository>().Object;
      }
      if (configurationParams == null)
      {
        configurationParams = new Mock<IConfigurationParams>().Object;
      }
      if (options == null)
      {
        var appSettings = new AppSettings
        {
          BlockHashCollectionSize = 0,
          BitcoinNetwork =  Network.RegTest.ToString(),
          MaxRetryCount = 100,
          ConsensusWaitDays = 1
        };
        options = Options.Create(appSettings);

      }
      if (legalEntityFactory == null)
      {
        legalEntityFactory = new LegalEntityFactoryMock(null);
      }
      if (consensusActivationValidatorFactory == null)
      {
        consensusActivationValidatorFactory = new ConsensusActivationValidatorFactoryMock(true);
      }

      return new CourtOrders(trustListRepository,
        courtOrderRepository,
        nodeRepository,
        legalEntityFactory,
        new Mock<ILogger<CourtOrders>>().Object,
        consensusActivationValidatorFactory,
        legalEndpointsService,
        bitcoindFactory,
        delegatedKeys,
        configurationParams,
        new Metrics(),
        options);
    }
  }
}
