﻿// Copyright (c) 2020 Bitcoin Association

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;

namespace BlacklistManager.Test.Unit
{
  [TestClass]
  public class LegalEntityExternalService
  {
    [TestMethod]
    public async Task LegalEntity_GetConsensusActivation_ShouldSucceedAsync()
    {
      //arrange
      string response = File.ReadAllText(@"Mocks/Data/SignedConsensusActivationJSON_Valid.txt");
      var restClientMock = new Mock<Domain.ExternalServices.IRestClient>();
      restClientMock
        .Setup(x => x.RequestAsync(HttpMethod.Get, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
        .Returns(Task.FromResult(response));

      var legalEntity = new Infrastructure.ExternalServices.LegalEntity(restClientMock.Object);

      //act
      var ca = await legalEntity.GetConsensusActivationAsync("");

      //assert
      Assert.AreEqual("8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d", ca.CourtOrderHash);
      Assert.AreEqual(637820, ca.EnforceAtHeight);
      Assert.AreEqual(2, ca.Acceptances.Count);
      Assert.AreEqual("8e99e733c1607127c3bc4d6b279a97f3ba06e11d423984bbdeb79bc912451d4d", ca.Acceptances.First().CourtOrderHash);
    }

    [TestMethod]
    public async Task LegalEntity_GetConsensusActivation_NoEnforceAtHeight_ShouldThrowExceptionAsync()
    {
      //arrange
      string response = File.ReadAllText(@"Mocks/Data/SignedConsensusActivationJSON_NotValidPayload_NoEnforceAtHeight.txt");
      var restClientMock = new Mock<Domain.ExternalServices.IRestClient>();
      restClientMock
        .Setup(x => x.RequestAsync(HttpMethod.Get, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<TimeSpan?>()))
        .Returns(Task.FromResult(response));

      var legalEntity = new Infrastructure.ExternalServices.LegalEntity(restClientMock.Object);

      //act & assert
      try
      {
        await legalEntity.GetConsensusActivationAsync("");
      }
      catch (ValidationException ex)
      {
        Assert.IsTrue(ex.Message.Contains("EnforceAtHeight field is required"));
        return;
      }
      Assert.IsTrue(false, "Should get ValidationException");
    }
  }
}
