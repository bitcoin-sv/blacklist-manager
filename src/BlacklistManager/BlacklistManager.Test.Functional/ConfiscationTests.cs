// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Domain.Models;
using BlacklistManager.Test.Functional.Server;
using Common.SmartEnums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class ConfiscationTests : TestBase
  {
    const string FeeAddress = "miaWUnUREiJbJayUWDKZ2aWNCUwQHv3dit";

    string FreezeOrderHash;
    string DummyTxHash1;
    string DummyTxHash2;
    string DummyTxHash3;
    string DummyTxHash4;

    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(mockedServices: true);

      DummyTxHash1 = Common.SignatureTools.GetSigDoubleHash("aaa", "UTF-8");
      DummyTxHash2 = Common.SignatureTools.GetSigDoubleHash("bbb", "UTF-8");
      DummyTxHash3 = Common.SignatureTools.GetSigDoubleHash("ccc", "UTF-8");
      DummyTxHash4 = Common.SignatureTools.GetSigDoubleHash("ddd", "UTF-8");
      FreezeOrderHash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, DummyTxHash1, 0),
        (1000, DummyTxHash2, 1),
        (1000, DummyTxHash4, 1));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      await CourtOrders.SetCourtOrderStatusAsync(FreezeOrderHash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();
    }

    [TestCleanup]
    public async Task TestCleanupAsync()
    {
      await base.CleanupAsync();
    }

    private async Task<string> SuccessfullySubmitConfiscationOrderAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>
      {
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[0], confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[1], confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
      };

      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument,
        DocumentType = DocumentType.ConfiscationEnvelope,
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      return confiscationCOHash;
    }

    [TestMethod]
    public async Task SuccesfullConfiscationCOAsync()
    {
      var confiscationCOHash = await SuccessfullySubmitConfiscationOrderAsync();

      var confTxList = await CourtOrderRepository.GetConfiscationTransactionsForWhiteListAsync(confiscationCOHash, 1);
      Assert.AreEqual(2, confTxList.Count());

      await CourtOrders.SetCourtOrderStatusAsync(confiscationCOHash, CourtOrderStatus.ConfiscationConsensus, 100);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      var co = await CourtOrderRepository.GetCourtOrdersAsync(confiscationCOHash, false);
      Assert.AreEqual(CourtOrderStatus.ConfiscationConsensus, co.Single().Status);

      var confTxs = await CourtOrderRepository.GetConfiscationTransactionsStatusAsync(confiscationCOHash);
      Assert.IsTrue(confTxs.All(x => x.EnforceAtHeight == 100));
    }

    [TestMethod]
    public async Task FailedCOE_EmptyOrderAsync()
    {
      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Parameter cannot be null 'order'.", responseString.Detail);
    }

    [TestMethod]
    public async Task FailedCOE_EmptyTxsAsync()
    {
      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = SignWithTestKey("{}")
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Parameter cannot be null 'txs'.", responseString.Detail);
    }

    [TestMethod]
    public async Task FailedCOE_MissingDocumentTypeAsync()
    {
      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = SignWithTestKey("{}"),
        ConfiscationTxDocument = new ()
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Invalid document type name ''", responseString.Detail);
    }
    
    [TestMethod]
    public async Task FailedCOE_NoFundsAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint),
        ConfiscationTxDocument = new ConfiscationTxDocumentViewModel() { DocumentType = DocumentType.ConfiscationTxDocument }
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Referenced funds marked for confiscation are missing.", responseString.Detail);
    }
    
    [TestMethod]
    public async Task FailedCOE_MissingFundsAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash3, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint),
        ConfiscationTxDocument = new ConfiscationTxDocumentViewModel() { DocumentType = DocumentType.ConfiscationTxDocument }
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Referenced funds marked for confiscation are missing.", responseString.Detail);
    }

    [TestMethod]
    public async Task FailedCOE_DestinationMissingAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Destination address on court order cannot be null.", responseString.Detail);
    }

    [TestMethod]
    public async Task FailedCOE_TxsMissingAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Confiscation transactions list is empty.", responseString.Detail);
    }

    [TestMethod]
    public async Task FailedCOE_TxSpendsNonFrozenFundsAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>
      {
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash3, Vout = 0 } }, confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash3, Vout = 1 } }, confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
      };


      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.IsTrue(responseString.Detail.Contains("Funds are trying to be spent that are not frozen"));
    }

    [TestMethod]
    public async Task FailedCOE_DoubleSpendDetectedAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>
      {
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } }, confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } }, confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
      };


      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.IsTrue(responseString.Detail.Contains("double spend detected"));
    }

    [TestMethod]
    public async Task FailedCOE_TxReferencesInvalidCOAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>
      {
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[0], DummyTxHash1, TEST_ADDRESS, FeeAddress, 900) },
      };


      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.IsTrue(responseString.Detail.Contains("is referencing court order with hash"));
    }

    [TestMethod]
    public async Task FailedCOE_Confiscating2InvalidAddressAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = "mxwRSiefLFnfA1QevQ6VWLHT1oURuHHz3L", Amount = 1500 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>
      {
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[0], confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[1], confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
      };


      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.IsTrue(responseString.Detail.Contains("does not match the address on court order"));
    }

    [TestMethod]
    public async Task FailedCOE_NotAllFundsConfiscatedAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 2000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>
      {
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[0], confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
      };


      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual(@"Not all funds marked for confiscation are being confiscated.", responseString.Detail);
    }

    [TestMethod]
    public async Task FailedCOE_SumOfConfiscatedFundsIsInvalidAsync()
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } },
                                                           new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash2, Vout = 1 } }},
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = 1000 },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>
      {
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[0], confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
        new ConfiscationTxViewModel { Hex = CreateConfiscationTx(courtOrder.Funds[1], confiscationCOHash, TEST_ADDRESS, FeeAddress, 900) },
      };


      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        DocumentType = DocumentType.ConfiscationEnvelope,
        ConfiscationCourtOrder = payload,
        ConfiscationTxDocument = coTxDocument
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Sum of confiscated value on confiscation transactions is greater than the confiscation amount on court order.", responseString.Detail);
    }

    [TestMethod]
    public async Task UnfreezeRejectedWithConfiscationOrderAsync()
    {
      await SuccessfullySubmitConfiscationOrderAsync();
      var unfreezeOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.UnfreezeOrder,
        CourtOrderId = "Unfreeze1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } } },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      var signed = SignWithTestKey(unfreezeOrder);
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Unfreeze order rejected. 1 funds that are about to be unfrozen are marked for confiscation.", responseString.Detail);

      // Clear all funds so we simulate unfreeze order with all funds
      unfreezeOrder.Funds.Clear();
      signed = SignWithTestKey(unfreezeOrder);
      response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Error while trying to store the court order to database. Unfreeze order rejected. 2 funds that are about to be unfrozen are marked for confiscation.", responseString.Detail);
    }

    [TestMethod]
    public async Task UnfreezeAcceptedAfterConfiscationOrderCancelledAsync()
    {
      var confiscationCOHash = await SuccessfullySubmitConfiscationOrderAsync();
      var unfreezeOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.UnfreezeOrder,
        CourtOrderId = "Unfreeze1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = FreezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } } },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      var signedUnfreezeOrder = SignWithTestKey(unfreezeOrder);
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signedUnfreezeOrder));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Unfreeze order rejected. 1 funds that are about to be unfrozen are marked for confiscation.", responseString.Detail);

      var cancelOrder = new CancelConfiscationOrderViewModel
      {
        DocumentType = DocumentType.CancelConfiscationOrder,
        ConfiscationOrderHash = confiscationCOHash
      };
      var signed = SignWithTestKey(cancelOrder);
      response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

      //After the confiscation order has been canceled, unfreeze is allowed
      response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signedUnfreezeOrder));
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }

    [TestMethod]
    public async Task FailedToCancelWithNotTrustedKeyAsync()
    {
      var confiscationCOHash = await SuccessfullySubmitConfiscationOrderAsync();

      var key = new Key();
      // create 3rdKey
      var create = new TrustListItemViewModelCreate
      {
        Id = key.PubKey.ToHex(),
        Trusted = true,
      };
      var content = Utils.JsonToStringContent(JsonSerializer.Serialize(create));
      var response = await Client.PostAsync(BlacklistManagerServer.ApiTrustListUrl, content);
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

      var cancelOrder = new CancelConfiscationOrderViewModel
      {
        DocumentType = DocumentType.CancelConfiscationOrder,
        ConfiscationOrderHash = confiscationCOHash
      };
      var payload = JsonSerializer.Serialize(cancelOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      var network = Network.GetNetwork(Configuration["AppSettings:BitcoinNetwork"]);
      var signed = Common.SignatureTools.CreateJSONWithBitcoinSignature(payload, key.GetWif(network).ToWif(), network, true);

      response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual("Key that was used to sign cancellation order does not belong to the trust chain which was used to sign referenced order.", responseString.Detail);
    }

    [TestMethod]
    public async Task FailedToCancelActivatedConfiscationOrderAsync()
    {
      var confiscationCOHash = await SuccessfullySubmitConfiscationOrderAsync();
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      await CourtOrders.SetCourtOrderStatusAsync(confiscationCOHash, CourtOrderStatus.ConfiscationConsensus, 100);
      await BackgroundJobs.WaitForConsensusActivationAsync();

      var cancelOrder = new CancelConfiscationOrderViewModel
      {
        DocumentType = DocumentType.CancelConfiscationOrder,
        ConfiscationOrderHash = confiscationCOHash
      };

      var signed = SignWithTestKey(cancelOrder);
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = JsonSerializer.Deserialize<ProblemDetails>(await response.Content.ReadAsStringAsync());
      Assert.AreEqual($"Confiscation order with hash {confiscationCOHash} must be policy enforced for cancellation.", responseString.Detail);
    }

    [TestMethod]
    public async Task SuccessfullyUnfreezeSecondFreezeOrderWithoutConfiscationAsync()
    {
      await UnfreezeRejectedWithConfiscationOrderAsync();

      var freezeOrderHash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, DummyTxHash1, 0),
        (1000, DummyTxHash2, 1));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      var unfreezeOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.UnfreezeOrder,
        CourtOrderId = "Unfreeze2",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = freezeOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund> { new CourtOrderViewModelCreate.Fund { Value = 1000, TxOut = new API.Rest.ViewModels.TxOut { TxId = DummyTxHash1, Vout = 0 } } },
        Blockchain = $"BSV-{Network.RegTest.Name}"
      };

      var signedUnfreezeOrder = SignWithTestKey(unfreezeOrder);
      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signedUnfreezeOrder));
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
    }
  }
}
