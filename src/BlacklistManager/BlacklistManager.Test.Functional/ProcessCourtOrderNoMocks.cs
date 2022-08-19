// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Test.Functional.Server;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using BMDomain = BlacklistManager.Domain.Models;
using BMAPI = BlacklistManager.API.Rest.ViewModels;
using Microsoft.AspNetCore.Mvc;
using DocumentType = Common.SmartEnums.DocumentType;
using BlacklistManager.API.Rest.ViewModels;

namespace BlacklistManager.Test.Functional
{
  /// <summary>
  /// To run tests all external services are needed up & running.
  /// 
  /// Tests call only public BM API methods
  /// </summary>
  [TestClass]
  public class ProcessCourtOrderNoMocks : TestBase
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync();
    }

    [TestCleanup]
    public async Task TestCleanupAsync()
    {
      await base.CleanupAsync();
    }

    [TestMethod]
    public async Task ProcessCourtOrder_InvalidCourtOrder_NoFunds_ShouldReturnBadRequestAsync()
    {
      //arrange
      // see base.Initialize()

      //act
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.FreezeOrder,
        new List<(long, BMAPI.TxOut)>(),
        out string courtOrderHash);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var pd = JsonSerializer.Deserialize<ProblemDetails>(responseContent);
      Assert.IsTrue(pd.Detail.Contains("Non empty 'funds' is required"), "Missing correct validation message");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_InvalidCourtOrder_ValidToNotUTC_ShouldReturnBadRequestAsync()
    {
      //arrange
      // see base.Initialize()

      //act

      // payload is deliberately hard coded to avoid date serialization dependency on system local settings
      #region developers hint
      // use this code to create new req if needed. 
      // validTo has to be something like "...+2:00"
      //
      //var reqContent = Utils.CreateProcessCourtOrderRequestContent(
      //  BMDomain.CourtOrder.DocumentTypeEnum.FreezeOrder,
      //  new List<BMAPI.TxOut>() { new BMAPI.TxOut("A", 1) },
      //  DateTime.Today, //will not work if machine local settings are UTC time
      //  out string courtOrderHash);
      #endregion

      var payload = "{\"blockchain\":\"BSV-RegTest\", \"courtOrderHash\":null,\"documentType\":\"freezeOrder\",\"validTo\":\"2020-05-11T00:12:11+02:00\",\"validFrom\":null,\"funds\":[{\"txOut\":{\"txId\":\"A\",\"vout\":1,\"status\":null}}],\"courtOrderId\":\"somecourtorderid\",\"freezeCourtOrderId\":null,\"freezeCourtOrderHash\":null}";
      string signed = SignatureTools.CreateJSONWithBitcoinSignature(payload, Utils.PrivateKey, NBitcoin.Network.RegTest, true);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var pd = JsonSerializer.Deserialize<ProblemDetails>(responseContent);
      Assert.IsTrue(pd.Detail.Contains("'validTo' must be UTC time"), "Missing correct validation message");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_InvalidCourtOrder_ValidFromQtValidTo_ShouldReturnBadRequestAsync()
    {
      //arrange
      // see base.Initialize()

      //act      
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.FreezeOrder,
        new List<(long, BMAPI.TxOut)>() { (1000, new BMAPI.TxOut("A", 1)) },
        DateTime.UtcNow.AddSeconds(1),
        DateTime.UtcNow,
        out string _);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var pd = JsonSerializer.Deserialize<ProblemDetails>(responseContent);
      Assert.IsTrue(pd.Detail.Contains("'validFrom' is greater then 'validTo'"), "Missing correct validation message");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_InvalidCourtOrder_TxOutVoutIsNull_ShouldReturnBadRequestAsync()
    {
      //arrange
      // see base.Initialize()

      //act      
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.FreezeOrder,
        new List<(long, BMAPI.TxOut)>() { (1000, new BMAPI.TxOut("A", null)) },
        DateTime.UtcNow,
        DateTime.UtcNow,
        out string courtOrderHash);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var pd = JsonSerializer.Deserialize<ProblemDetails>(responseContent);
      Assert.IsTrue(pd.Detail.Contains("'vout' is required"), "Missing correct validation message");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_NoPayload_ShouldReturnBadRequestAsync()
    {
      //arrange
      var jsonEnvelope = new JsonEnvelope()
      {
        Payload = null,
        Encoding = "UTF-8",
        Mimetype = "application/json",
        PublicKey = "04d0de0aaeaefad02b8bdc8a01a1b8b11c696bd3d66a2c5f10780d95b7df42645cd85228a6fb29940e858e7e55842ae2bd115d1ed7cc0e82d934e929c97648cb0a",
        Signature = "304402201a271fa1807c3e010196d7b1f249fde5dea007c68bb44ec005a400a2bedde32502207c4b225ead678847cc52818708ffdc4579b5703a2f6e03200ad629813c887cdf"
      };

      var content = jsonEnvelope.ToJson();

      //act
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(content));
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.AreEqual("Payload", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task ProcessCourtOrder_NoMimetype_ShouldReturnBadRequestAsync()
    {
      var coJSON = JsonSerializer.Serialize(new CourtOrderViewModelCreate(), SerializerOptions.SerializeOptionsNoPrettyPrint);
      var jsonSig = SignatureTools.CreateJSONWithBitcoinSignature(coJSON, Utils.PrivateKey, NBitcoin.Network.RegTest);

      var sig = JsonEnvelope.ToObject(jsonSig);
      sig.Mimetype = null;

      var content = sig.ToJson();

      //act
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(content));
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.IsTrue(vpd.Errors.Any(x => x.Key == "Mimetype"));
    }

    [TestMethod]
    public async Task ProcessCourtOrder_WrongMimetype_ShouldReturnBadRequestAsync()
    {
      //arrange
      var jsonEnvelope = new JsonEnvelope()
      {
        Payload = "{\"courtOrderHash\":null,\"documentType\":\"freezeOrder\",\"validTo\":\"2020-05-11T00:12:11+00:00\",\"validFrom\":null,\"funds\":[{\"txOut\":{\"txId\":\"A\",\"vout\":1,\"status\":null}}],\"courtOrderId\":\"somecourtorderid\",\"freezeCourtOrderId\":null,\"freezeCourtOrderHash\":null}",
        Encoding = "UTF-8",
        Mimetype = "application/xml",
        PublicKey = "04d0de0aaeaefad02b8bdc8a01a1b8b11c696bd3d66a2c5f10780d95b7df42645cd85228a6fb29940e858e7e55842ae2bd115d1ed7cc0e82d934e929c97648cb0a",
        Signature = "304402201a271fa1807c3e010196d7b1f249fde5dea007c68bb44ec005a400a2bedde32502207c4b225ead678847cc52818708ffdc4579b5703a2f6e03200ad629813c887cdf"
      };

      var content = jsonEnvelope.ToJson();

      //act
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(content));
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.AreEqual("Mimetype", vpd.Errors.First().Key);
    }

    [TestMethod]
    public async Task ProcessCourtOrder_2x_ShouldReturn409Async()
    {
      //act
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.FreezeOrder,
        new List<(long, BMAPI.TxOut)>() { (1000, new BMAPI.TxOut("A", 1)) },
        out string courtOrderHash);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      var responseContent = await response.Content.ReadAsStringAsync();

      var response2 = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      var responseContent2 = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
      Assert.AreEqual(HttpStatusCode.Conflict, response2.StatusCode);
      Assert.AreEqual(responseContent, responseContent2);
    }

    [TestMethod]
    public async Task ProcessCourtOrder_UnfreezeOrderNoReferencedCourtOrder_ShouldReturnBadRequestAsync()
    {
      //act
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.UnfreezeOrder,
        "mylittlehash",
        null,null,
        "somenotexistingcourtorderid", "somenotexistingcourtorderhash",
        new List<(long, BMAPI.TxOut)>() {
          (1000, new BMAPI.TxOut("A", 1)) ,
          (1000, new BMAPI.TxOut("C", 1)) },
        out string courtOrderHash);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.IsTrue(responseContent.Contains("Referenced court order does not exist"), "Error message should contain referenced court order problem");
    }

    [TestMethod]
    public async Task ProcessCourtOrder_UnfreezeOrderUnreferencedFunds_ShouldReturnBadRequestAsync()
    {
      //arrange
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 1),
        (1000, "B", 1));

      var order2Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "C", 1),
        (1000, "D", 1));

      //act
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.UnfreezeOrder,
        "somecourtorderid2",
        null,null,
        "somecourtorderid",order1Hash,
        new List<(long, BMAPI.TxOut)>() {
          (1000, new BMAPI.TxOut("A", 1)) ,
          (1000, new BMAPI.TxOut("C", 1)) },
        out string courtOrderHash);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      Assert.IsTrue(responseContent.Contains(new BMDomain.TxOut("C", 1).ToString()), "Error message should contain unreferenced fund C/1");
    }
  }
}
