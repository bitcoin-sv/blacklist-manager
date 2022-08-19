// Copyright (c) 2020 Bitcoin Association

using System.Linq;
using Common.BitcoinRpcClient.Requests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading.Tasks;
using static Common.BitcoinRpcClient.Requests.RpcFrozenFunds;
using NBitcoin;
using BlacklistManager.Domain.Models;
using BlacklistManager.API.Rest.ViewModels;
using Common.SmartEnums;
using System.Text.Json;
using BlacklistManager.Test.Functional.Server;
using System.Net;

namespace BlacklistManager.Test.Functional
{
  //[TestClass]
  public class NodeTest : TestBaseWithBitcoind
  {
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await base.TestInitializeAsync();
    }

    [TestCleanup]
    public async Task TestingCleanupAsync()
    {
      TestCleanup();
      await CleanupAsync();
    }

    [TestMethod]
    public async Task AddPolicyBlacklistAsync()
    {
      var fundList = new List<RpcFund> {
      new RpcFund
      {
        TxOut = new RpcFund.RpcTxOut
        {
          TxId = "19e8d6172493b899bdadfd1e012e042a708b0844b388a18f6903586e9747a709",
          Vout = 0
        }
      }};

      var frozenFunds = new RpcFrozenFunds { Funds = fundList};

      await _rpcClient0.AddToPolicyBlacklistAsync(frozenFunds, null);

      var queryList = await _rpcClient0.QueryBlacklistAsync();
      Assert.AreEqual(1, queryList.Funds.Length);
      Assert.AreEqual("19e8d6172493b899bdadfd1e012e042a708b0844b388a18f6903586e9747a709", queryList.Funds.First().TxOut.TxId);
    }

    [TestMethod]
    public async Task AddConsensusBlacklistAsync()
    {
      var fundList = new List<RpcFund> {
      new RpcFund
      {
        TxOut = new RpcFund.RpcTxOut
        {
          TxId = "19e8d6172493b899bdadfd1e012e042a708b0844b388a18f6903586e9747a709",
          Vout = 0
        },
        EnforceAtHeight = new List<RpcFund.RpcEnforceAtHeight>()
        {
          new RpcFund.RpcEnforceAtHeight(100, 200)
        },
        PolicyExpiresWithConsensus = true
      }};

      var frozenFunds = new RpcFrozenFunds { Funds = fundList };

      await _rpcClient0.AddToConsensusBlacklistAsync(frozenFunds, null);

      var queryList = await _rpcClient0.QueryBlacklistAsync();
      Assert.AreEqual(1, queryList.Funds.Length);
      var queryFund = queryList.Funds.First();
      Assert.AreEqual("19e8d6172493b899bdadfd1e012e042a708b0844b388a18f6903586e9747a709", queryFund.TxOut.TxId);
      Assert.AreEqual(100, queryFund.EnforceAtHeight.Single().Start);
      Assert.AreEqual(200, queryFund.EnforceAtHeight.Single().Stop);
    }

    [TestMethod]
    public async Task ClearBlacklistAsync()
    {
      await AddConsensusBlacklistAsync();

      await _rpcClient0.ClearBlacklistsAsync(new RpcClearBlacklist(true, null));

      var queryList = await _rpcClient0.QueryBlacklistAsync();
      Assert.AreEqual(0, queryList.Funds.Length);
    }

    [TestMethod]
    public async Task RemoveFromBlacklistAsync()
    {
      await AddPolicyBlacklistAsync();

      var fundList = new List<RpcFund> {
      new RpcFund
      {
        TxOut = new RpcFund.RpcTxOut
        {
          TxId = "19e8d6172493b899bdadfd1e012e042a708b0844b388a18f6903586e9747a709",
          Vout = 0
        }
      }};

      var frozenFunds = new RpcFrozenFunds { Funds = fundList };

      await _rpcClient0.RemoveFromPolicyBlacklistAsync(frozenFunds);

      var queryList = await _rpcClient0.QueryBlacklistAsync();
      Assert.AreEqual(0, queryList.Funds.Length);
    }

    [TestMethod]
    public async Task ConfiscationFailedForNonWhitelistedFundsAsync()
    {
      await SetupChainAsync(_rpcClient0);
      var coins = await GetCoinsAsync(_rpcClient0, 10);
      Money coinValue2Spend = Money.Satoshis(10000);
      long amount2ConfiscatePerTx = coinValue2Spend.Satoshi - 1000;

      var txList = new List<(string txHex, string txId)>();
      var txOuts = new List<(long Value, string TxId, long vOut)>();
      foreach(var coin in coins)
      {
        var tx = CreateNewTransaction(coin, coinValue2Spend);
        txList.Add(tx);
        txOuts.Add((coinValue2Spend, tx.txId, 0));
      }
      var funds = txOuts.Select(x => new CourtOrderViewModelCreate.Fund { TxOut = new API.Rest.ViewModels.TxOut { TxId = x.TxId, Vout = x.vOut }, Value = x.Value }).ToList();

      await _rpcClient0.SendRawTransactionsAsync(txList.Select(x => (x.txHex, false, false, false)).ToArray());
      await _rpcClient0.GenerateAsync(1);

      var freezeOrderHash = await Utils.SubmitFreezeOrderAsync(Client, txOuts.ToArray());

      await CourtOrders.SetCourtOrderStatusAsync(freezeOrderHash, CourtOrderStatus.FreezeConsensus, 100);

      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = DocumentType.ConfiscationOrder,
        CourtOrderId = "Confiscation1",
        FreezeCourtOrderId = "somecourtorderid",
        FreezeCourtOrderHash = freezeOrderHash,
        Funds = funds,
        Destination = new ConfiscationDestinationVM() { Address = TEST_ADDRESS, Amount = amount2ConfiscatePerTx * funds.Count }
      };

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      string signedCO = SignWithTestKey(courtOrder);
      var confiscationCOHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      var confiscationTxs = new List<ConfiscationTxViewModel>();
      foreach(var fund in funds)
      {
        confiscationTxs.Add(new ConfiscationTxViewModel { Hex = CreateConfiscationTx(fund, confiscationCOHash, TEST_ADDRESS, FEE_ADDRESS, amount2ConfiscatePerTx) });
      }

      var coTxDocument = new ConfiscationTxDocumentViewModel
      {
        ConfiscationCourtOrderHash = confiscationCOHash,
        ConfiscationCourtOrderId = "Confiscation1",
        ConfiscationTxs = confiscationTxs,
        DocumentType = DocumentType.ConfiscationTxDocument
      };

      var confiscationEnvelope = new ConfiscationEnvelopeViewModel
      {
        ConfiscationCourtOrder = signedCO,
        ConfiscationTxDocument = coTxDocument,
        DocumentType = DocumentType.ConfiscationEnvelope,
      };

      var signed = SignWithTestKey(confiscationEnvelope);

      var response = await Client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, Utils.JsonToStringContent(signed));
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);

      var blockCount = (int)await _node0.RpcClient.GetBlockCountAsync();
      await CourtOrders.SetCourtOrderStatusAsync(confiscationCOHash, CourtOrderStatus.ConfiscationConsensus, blockCount + 1);
      await _node0.RpcClient.GenerateAsync(1);
      await Task.Delay(1000);

      var confiscationErrors = await CourtOrderRepository.GetConfiscationTransactionsStatusAsync(confiscationCOHash);
      Assert.AreEqual(10, confiscationErrors.Count());
      Assert.IsTrue(confiscationErrors.All(x => x.LastError == "bad-txns-inputs-frozen"));
    }
  }
}
