// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using BlacklistManager.Test.Functional.Server;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional
{
  /// <summary>
  /// Testing of txOutController
  /// </summary>

  [TestClass]
  public class TxOutRest : TestBase
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
    public async Task TxOut_TxId_NotCaseSensitiveAsync()
    {
      //arrange
      // see base.Initialize()

      // freeze for A,99 
      var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 99));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      var fund = await Utils.QueryFundAsync(Client, "A", 99);
      Assert.IsNotNull(fund);

      fund = await Utils.QueryFundAsync(Client, "a", 99);
      Assert.IsNotNull(fund);

      await Nodes.CreateNodeAsync(new BlacklistManager.Domain.Models.Node("node1", 0, "mocked", "mocked", null));
      await BackgroundJobs.WaitAllAsync();

      BitcoindFactory.AssertEqualAndClear(
        "node1:addToPolicy/a,99||"
      );
    }

      [TestMethod]
    public async Task TxOut_ShouldReturnInputValuesAsync()
    {
      //arrange
      // see base.Initialize()

    // freeze for A,99 
    var order1Hash = await Utils.SubmitFreezeOrderAsync(Client,
        (1000, "A", 99));      
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      var fund = await Utils.QueryFundAsync(Client, "A", 99);
      AssertExtension.AreEqual($"a,99|{order1Hash},,,", fund);

      // unfreeze for A,99
      var order2Hash = await Utils.SubmitUnfreezeOrderAsync(Client, order1Hash,
        (1000, "A", 99));
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      fund = await Utils.QueryFundAsync(Client, "A", 99);
      AssertExtension.AreEqual($"a,99|{order1Hash},{order2Hash},,", fund);

      // consensus for freeze order1
      await CourtOrders.SetCourtOrderStatusAsync(order1Hash, CourtOrderStatus.FreezeConsensus, 100);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      // consensus for unfreeze order1
      await CourtOrders.SetCourtOrderStatusAsync(order2Hash, CourtOrderStatus.UnfreezeConsensus, 200);
      await BackgroundJobs.WaitForCourtOrderProcessingAsync();

      fund = await Utils.QueryFundAsync(Client, "A", 99);
      AssertExtension.AreEqual($"a,99|{order1Hash},{order2Hash},100,200", fund);
    }

    [TestMethod]
    public async Task TxOut_NotExist_ShouldReturnNotFoundAsync()
    {
      //arrange
      // see base.Initialize()

      //act
      var response = await Client.GetAsync(BlacklistManagerServer.Get.GetTxOut("A", 99));

      //assert 
      Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task TxOut_NegativeVout_ShouldReturnBadRequestAsync()
    {
      //arrange
      // see base.Initialize()

      //act
      var response = await Client.GetAsync(BlacklistManagerServer.Get.GetTxOut("A", -1));
      var responseContent = await response.Content.ReadAsStringAsync();

      //assert 
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);

      var vpd = JsonSerializer.Deserialize<ValidationProblemDetails>(responseContent);
      Assert.AreEqual(1, vpd.Errors.Count());
      Assert.AreEqual("Vout", vpd.Errors.First().Key);
    }
  }
}
