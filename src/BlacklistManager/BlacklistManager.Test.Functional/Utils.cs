// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using BlacklistManager.Test.Functional.Server;
using BMAPI = BlacklistManager.API.Rest.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;
using BlacklistManager.Domain.Models;
using BlacklistManager.API.Rest.ViewModels;
using Common.SmartEnums;

namespace BlacklistManager.Test.Functional
{
  public static class Utils
  {
    public const string PrivateKey = "cNpxQaWe36eHdfU3fo2jHVkWXVt5CakPDrZSYguoZiRHSz9rq8nF";
    public const string PublicKey = "027ae06a5b3fe1de495fa9d4e738e48810b8b06fa6c959a5305426f78f42b48f8c";
    public const string TestAddress = "msRNSw5hHA1W1jXXadxMDMQCErX1X8whTk";

    public static async Task<string> SubmitFreezeOrderAsync(HttpClient client, params (long Value, string TxId, long vOut)[] txOuts)
    {
      return await SubmitOrderAsync(client, DocumentType.FreezeOrder, "somecourtorderid", null, null, txOuts);
    }

    public static async Task<string> SubmitFreezeOrderAsync(HttpClient client, string courtOrderId, params (long Value, string TxId, long vOut)[] txOuts)
    {
      return await SubmitOrderAsync(client, DocumentType.FreezeOrder, courtOrderId, null, null, txOuts);
    }

    public static async Task<string> SubmitOrderAsync(HttpClient client, DocumentType docType, string courtOrderId, string freezeCOId, string freezeCOHash, params (long Value, string TxId, long vOut)[] txOuts)
    {
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        docType,
        courtOrderId,
        null,
        null,
        freezeCOId,
        freezeCOHash,
        new List<(long, BMAPI.TxOut)>(
          txOuts.Select(x => (x.Value, new BMAPI.TxOut(x.TxId, x.vOut)))),
        out string courtOrderHash);

      var response = await client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      return courtOrderHash;
    }

    public static async Task<string> SubmitUnfreezeOrderAsync(HttpClient client, string freezeCourtOrderHash, params (long Value, string TxId, long vOut)[] txOuts)
    {
      return await SubmitOrderAsync(client, DocumentType.UnfreezeOrder, "somecourtorderid", "somecourtorderid", freezeCourtOrderHash, txOuts);
    }

    public static StringContent CreateProcessCourtOrderRequestContent(
      DocumentType documentType,
      IEnumerable<(long Value, BMAPI.TxOut TxOut)> funds,
      out string orderHash)
    {
      return CreateProcessCourtOrderRequestContent(documentType, "somecourtorderid", null, null, null, null, funds, out orderHash);
    }

    public static StringContent CreateProcessCourtOrderRequestContent(
      DocumentType documentType,
      IEnumerable<(long, BMAPI.TxOut)> funds,
      DateTime? validFrom,
      DateTime? validTo,
      out string orderHash)
    {
      return CreateProcessCourtOrderRequestContent(documentType, "somecourtorderid", validFrom, validTo, null, null, funds, out orderHash);
    }

    public static StringContent CreateProcessCourtOrderRequestContent(
      DocumentType documentType,
      string courtOrderId,
      DateTime? validFrom,
      DateTime? validTo,
      string freezeCourtOrderId,
      string freezeCourtOrderHash,
      IEnumerable<(long Value, BMAPI.TxOut TxOut)> funds,
      out string orderHash)
    {
      var courtOrder = new CourtOrderViewModelCreate
      {
        DocumentType = documentType,
        ValidFrom = validFrom,
        ValidTo = validTo,
        CourtOrderId = courtOrderId,
        FreezeCourtOrderId = freezeCourtOrderId,
        FreezeCourtOrderHash = freezeCourtOrderHash,
        Funds = new List<CourtOrderViewModelCreate.Fund>(),
        Blockchain = $"BSV-{NBitcoin.Network.RegTest.Name}"
      };
      foreach (var fund in funds)
      {
        courtOrder.Funds.Add(new CourtOrderViewModelCreate.Fund() { TxOut = fund.TxOut, Value = fund.Value });
      }

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptionsNoPrettyPrint);
      string signed = Common.SignatureTools.CreateJSONWithBitcoinSignature(payload, PrivateKey, NBitcoin.Network.RegTest, true);

      orderHash = Common.SignatureTools.GetSigDoubleHash(payload, "UTF-8");

      return JsonToStringContent(signed);
    }

    public static StringContent JsonToStringContent(string json)
    {
      return new StringContent(json, Encoding.UTF8, "application/json");
    }

    public static void WaitUntil(Func<bool> predicate)
    {
      for (int i = 0; i < 100; i++)
      {
        if (predicate())
        {
          return;
        }

        Thread.Sleep(100);  // see also BackgroundJobsMock.WaitForPropagation()
      }

      throw new Exception("Timeout - WaitUntil did not complete in allocated time");
    }

    public static async Task<CourtOrderQueryViewModel> QueryCourtOrderAsync(HttpClient client, string courtOrder, bool includeFunds)
    {
      var response = await client.GetAsync(BlacklistManagerServer.Get.GetCourtOrder(courtOrder, includeFunds));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var json = await response.Content.ReadAsStringAsync();
      var c = JsonSerializer.Deserialize<CourtOrderQueryViewModel>(json);
      Assert.IsNotNull(c);
      return c;
    }

    public static async Task<IEnumerable<CourtOrderQueryViewModel>> QueryCourtOrdersAsync(HttpClient client, bool includeFunds)
    {
      var response = await client.GetAsync(BlacklistManagerServer.Get.GetCourtOrder(null, includeFunds));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var json = await response.Content.ReadAsStringAsync();
      var c = JsonSerializer.Deserialize<CourtOrderQueryViewModel[]>(json);
      Assert.IsNotNull(c);
      return c;
    }

    public static async Task<FundViewModel> QueryFundAsync(HttpClient client, string txId, long vOut)
    {
      var response = await client.GetAsync(BlacklistManagerServer.Get.GetTxOut(txId, vOut));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var txOutJson = await response.Content.ReadAsStringAsync();
      var txOut = JsonSerializer.Deserialize<FundViewModel>(txOutJson);
      Assert.IsNotNull(txOut);
      return txOut;
    }
  }

  public static class AssertExtension
  {
    public static void AreEqual(string expected, FundStateToPropagate fsp)
    {
      var eah = string.Join(";", 
        fsp.EnforceAtHeight.List
          .OrderBy(e => e.CourtOrderHash)
          .Select(e => $"{e.CourtOrderHash},{e.StartEnforceAtHeight},{e.StopEnforceAtHeight},{e.HasUnfreezeOrder}"));
      
      var eahPrevious = string.Join(";",
        fsp.EnforceAtHeightPrevious.List
          .OrderBy(e => e.CourtOrderHash)
          .Select(e => $"{e.CourtOrderHash},{e.StartEnforceAtHeight},{e.StopEnforceAtHeight},{e.HasUnfreezeOrder}"));

      Assert.AreEqual(
        expected,
        $"{fsp.NodeId}|{fsp.TxOut.TxId},{fsp.TxOut.Vout}|{eah}|{eahPrevious}",
        "Wrong propagation data");
    }

    public static void AreEqual(string expected, Fund f)
    {
      var eah = string.Join(";", 
        f.EnforceAtHeight.List
          .OrderBy(e => e.CourtOrderHash)
          .Select(e => $"{e.CourtOrderHash},{e.StartEnforceAtHeight},{e.StopEnforceAtHeight},{e.HasUnfreezeOrder}"));

      Assert.AreEqual(
        expected,
        $"{f.TxOut.TxId},{f.TxOut.Vout}|{eah}",
        "Wrong fund data");
    }

    public static void AreEqual(IEnumerable<Fund> first, IEnumerable<Fund> second)
    {
      // Fund does not implement all interfaced required by CollectionAssert.AreEqual, so we just compare string representation
      CollectionAssert.AreEquivalent(
        first.Select(x => x.ToString()).ToArray(),
        second.Select(x => x.ToString()).ToArray()
      );
    }

    public static void AreEqual(string expected, FundViewModel fund)
    {
      var eah = string.Join(";",
        fund.EnforceAtHeight
          .OrderBy(e => e.CourtOrderHash).ThenBy(e => e.CourtOrderHashUnfreeze)
          .Select(e => $"{e.CourtOrderHash},{e.CourtOrderHashUnfreeze},{e.StartEnforceAtHeight},{e.StopEnforceAtHeight}"));
      
      Assert.AreEqual(
        expected,
        $"{fund.TxOut.TxId},{fund.TxOut.Vout}|{eah}",
        "Wrong fund data");
    }
  }
}
