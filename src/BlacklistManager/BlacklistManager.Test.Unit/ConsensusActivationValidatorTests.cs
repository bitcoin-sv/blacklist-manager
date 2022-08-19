// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using BlacklistManager.Infrastructure.Actions;
using Common.SmartEnums;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Unit
{
  [TestClass]
  public class ConsensusActivationValidatorTests
  {
    [TestMethod]
    public async Task ConsensusActivationValidator_IsValidAsync()
    {
      //arrange
      var trustListRepositoryMock = new Mock<ITrustListRepository>();
      trustListRepositoryMock.Setup(x => x.IsPublicKeyTrustedAsync(It.IsAny<string>())).ReturnsAsync(true);

      var courtOrderRepositoryMock = new Mock<ICourtOrderRepository>();
      var cos = new[] {
        new Domain.Models.CourtOrder
        {
          CourtOrderId = "id0",
          CourtOrderHash = "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
          DocumentType = DocumentType.FreezeOrder
        }
      }.Select(s => s);
      courtOrderRepositoryMock
        .Setup(x => x.GetCourtOrdersAsync("8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d", false))
        .Returns(Task.FromResult(cos));

      var ca = await GetConsensusActivationFromFileAsync("SignedConsensusActivationJSON_Valid.txt");
      
      //act
      var validator = new ConsensusActivationValidator(
        ca,
        "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
        trustListRepositoryMock.Object,
        courtOrderRepositoryMock.Object);

      bool isValid = await validator.IsValidAsync();

      //assert
      Assert.IsTrue(isValid);
    }

    [TestMethod]
    public async Task ConsensusActivationValidator_SignatureNotValidAsync()
    {
      //arrange
      var trustListRepositoryMock = new Mock<ITrustListRepository>();
      trustListRepositoryMock.Setup(x => x.IsPublicKeyTrustedAsync(It.IsAny<string>())).ReturnsAsync(true);

      var courtOrderRepositoryMock = new Mock<ICourtOrderRepository>();
      var cos = new[] {
        new Domain.Models.CourtOrder
        {
          CourtOrderId = "id0",
          CourtOrderHash = "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
          DocumentType = DocumentType.FreezeOrder
        }
      }.Select(s => s);
      courtOrderRepositoryMock
        .Setup(x => x.GetCourtOrdersAsync("8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d", false))
        .Returns(Task.FromResult(cos));

      var ca = await GetConsensusActivationFromFileAsync("SignedConsensusActivationJSON_NotValidSignature_EnforceAtHeightChanged.txt");

      //act
      var validator = new ConsensusActivationValidator(
        ca,
        "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
        trustListRepositoryMock.Object,
        courtOrderRepositoryMock.Object);

      bool isValid = await validator.IsValidAsync();

      //assert
      Assert.IsFalse(isValid);
      Assert.AreEqual(1, validator.Errors.Count());
      Assert.AreEqual("Digital signature applied to consensus activation is invalid", validator.Errors.First());
    }

    [TestMethod]
    public async Task ConsensusActivationValidator_UnfreezeEnforceAtHeightNotValidAsync()
    {
      //arrange
      var trustListRepositoryMock = new Mock<ITrustListRepository>();
      trustListRepositoryMock.Setup(x => x.IsPublicKeyTrustedAsync(It.IsAny<string>())).ReturnsAsync(true);

      var courtOrderRepositoryMock = new Mock<ICourtOrderRepository>();
      var cos = new[] {
        new Domain.Models.CourtOrder
        {
          CourtOrderId = "id1",
          CourtOrderHash = "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
          DocumentType = DocumentType.UnfreezeOrder,
          FreezeCourtOrderId = "id0",
          FreezeCourtOrderHash = "freezeOrderHash"
        }
      }.Select(s => s);
      courtOrderRepositoryMock
        .Setup(x => x.GetCourtOrdersAsync("8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d", false))
        .Returns(Task.FromResult(cos));
      cos = new[] {
        new Domain.Models.CourtOrder
        {
          Type = CourtOrderType.Freeze,
          CourtOrderId = "id0",
          CourtOrderHash = "freezeOrderHash",
          EnforceAtHeight = 637820 + 1, // consensus freeze at height is bigger than unfreeze at height from consensus activation document
          Status = CourtOrderStatus.FreezeConsensus
        }
      }.Select(s => s);
      courtOrderRepositoryMock
        .Setup(x => x.GetCourtOrdersAsync("freezeOrderHash", false))
        .Returns(Task.FromResult(cos));

      var ca = await GetConsensusActivationFromFileAsync("SignedConsensusActivationJSON_Valid.txt");

      //act
      var validator = new ConsensusActivationValidator(
        ca,
        "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
        trustListRepositoryMock.Object,
        courtOrderRepositoryMock.Object);

      bool isValid = await validator.IsValidAsync();

      //assert
      Assert.IsFalse(isValid);
      Assert.AreEqual(1, validator.Errors.Count());
      Assert.IsTrue(validator.Errors.First().StartsWith("EnforceAtHeight for an unfreeze order must be greater"));
    }

    [TestMethod]
    public async Task ConsensusActivationValidator_AcceptanceSignatureNotValidAsync()
    {
      //arrange
      var trustListRepositoryMock = new Mock<ITrustListRepository>();
      trustListRepositoryMock.Setup(x => x.IsPublicKeyTrustedAsync(It.IsAny<string>())).ReturnsAsync(true);

      var courtOrderRepositoryMock = new Mock<ICourtOrderRepository>();
      var cos = new[] {
        new Domain.Models.CourtOrder
        {
          CourtOrderId = "id0",
          CourtOrderHash = "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
          DocumentType = DocumentType.FreezeOrder
        }
      }.Select(s => s);
      courtOrderRepositoryMock
        .Setup(x => x.GetCourtOrdersAsync("8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d", false))
        .Returns(Task.FromResult(cos));

      var ca = await GetConsensusActivationFromFileAsync("SignedConsensusActivationJSON_NotValidSignature_AcceptanceCourtOrderHashChanged.txt");

      //act
      var validator = new ConsensusActivationValidator(
        ca,
        "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
        trustListRepositoryMock.Object,
        courtOrderRepositoryMock.Object);

      bool isValid = await validator.IsValidAsync();

      //assert
      Assert.IsFalse(isValid);
      Assert.AreEqual(2, validator.Errors.Count());
      Assert.AreEqual("Digital signature applied to consensus activation acceptance [1] is invalid", validator.Errors.First());
    }

    [TestMethod]
    public async Task ConsensusActivationValidator_AcceptanceCourtOrderHashNotValidAsync()
    {
      //arrange
      var trustListRepositoryMock = new Mock<ITrustListRepository>();
      trustListRepositoryMock.Setup(x => x.IsPublicKeyTrustedAsync(It.IsAny<string>())).ReturnsAsync(true);

      var courtOrderRepositoryMock = new Mock<ICourtOrderRepository>();
      var cos = new[] {
        new Domain.Models.CourtOrder
        {
          CourtOrderId = "id0",
          CourtOrderHash = "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
          DocumentType = DocumentType.FreezeOrder
        }
      }.Select(s => s);
      courtOrderRepositoryMock
        .Setup(x => x.GetCourtOrdersAsync("8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d", false))
        .Returns(Task.FromResult(cos));

      var ca = await GetConsensusActivationFromFileAsync("SignedConsensusActivationJSON_NotValidPayload_AcceptanceCourtOrderHashChanged.txt");

      //act
      var validator = new ConsensusActivationValidator(
        ca,
        "8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d",
        trustListRepositoryMock.Object,
        courtOrderRepositoryMock.Object);

      bool isValid = await validator.IsValidAsync();

      //assert
      Assert.IsFalse(isValid);
      Assert.AreEqual(1, validator.Errors.Count());
      Assert.AreEqual("CourtOrderHash in consensus activation acceptance [0] does not match one in consensus activation", validator.Errors.First());
    }

    private async Task<Domain.Models.ConsensusActivation> GetConsensusActivationFromFileAsync(string fileName)
    {
      string response = File.ReadAllText($@"Mocks/Data/{fileName}");
      var restClientMock = new Mock<Common.IRestClient>();
      restClientMock
        .Setup(x => x.RequestAsync(HttpMethod.Get, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()))
        .Returns(Task.FromResult(response));

      var legalEntity = new Infrastructure.ExternalServices.LegalEntity(restClientMock.Object, null, NBitcoin.Network.RegTest.ToString(), null);
      var cts = new CancellationTokenSource();
      return await legalEntity.GetConsensusActivationAsync("", cts.Token);
    }
  }
}
