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
using BlacklistManager.Test.Functional.ViewModels;
using Common.SmartEnums;

namespace BlacklistManager.Test.Functional
{
  public static class Utils
  {
    public const string PrivateKey = "cVwyYsavaTKRrDHkqKmnHxHxGbWWCMxQhBiSA97SwBcT8f17zo1G";
    public const string PublicKey = "0340d9c71c2ad42765bdef30f0d072d20c3362e9e3bd8676798a9c6677c1d799e6";

    public static async Task<string> SubmitFreezeOrderAsync(HttpClient client, params (string TxId, long vOut)[] txOuts)
    {
      return await SubmitFreezeOrderAsync(client, "somecourtorderid", txOuts);
    }

    public static async Task<string> SubmitFreezeOrderAsync(HttpClient client, string courtOrderId, params (string TxId, long vOut)[] txOuts)
    {
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.FreezeOrder,
        courtOrderId,
        null,
        null,
        null,
        null,
        new List<BMAPI.TxOut>(
          txOuts.Select(x => new BMAPI.TxOut(x.TxId, x.vOut))),
        out string courtOrderHash);

      var response = await client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
      return courtOrderHash;
    }

    public static async Task<string> SubmitUnfreezeOrderAsync(HttpClient client, string freezeCourtOrderHash, params (string TxId, long vOut)[] txOuts)
    {
      return await SubmitUnfreezeOrderAsync(client, "somecourtorderid", freezeCourtOrderHash, txOuts);
    }

    public static async Task<string> SubmitUnfreezeOrderAsync(HttpClient client, string freezeCourtOrderId, string freezeCourtOrderHash, params (string TxId, long vOut)[] txOuts)
    {
      var reqContent = Utils.CreateProcessCourtOrderRequestContent(
        DocumentType.UnfreezeOrder,
        "somecourtorderid",
        null,
        null,
        freezeCourtOrderId,
        freezeCourtOrderHash,
        new List<BMAPI.TxOut>(
          txOuts.Select(x => new BMAPI.TxOut(x.TxId, x.vOut))),
        out string courtOrderHash);

      var response = await client.PostAsync(BlacklistManagerServer.Post.ProcessCourtOrder, reqContent);
      Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
      return courtOrderHash;
    }

    public static StringContent CreateProcessCourtOrderRequestContent(
      DocumentType documentType,
      IEnumerable<BMAPI.TxOut> funds,
      out string orderHash)
    {
      return CreateProcessCourtOrderRequestContent(documentType, "somecourtorderid", null, null, null, null, funds, out orderHash);
    }

    public static StringContent CreateProcessCourtOrderRequestContent(
      DocumentType documentType,
      IEnumerable<BMAPI.TxOut> funds,
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
      IEnumerable<BMAPI.TxOut> funds,
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
        Funds = new List<CourtOrderViewModelCreate.Fund>()
      };
      foreach (var fund in funds)
      {
        courtOrder.Funds.Add(new CourtOrderViewModelCreate.Fund() { TxOut = fund });
      }

      string payload = JsonSerializer.Serialize(courtOrder, Common.SerializerOptions.SerializeOptions);
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

    public static async Task<CourtOrderQuery> QueryCourtOrderAsync(HttpClient client, string courtOrder, bool includeFunds)
    {
      var response = await client.GetAsync(BlacklistManagerServer.Get.GetCourtOrder(courtOrder, includeFunds));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var json = await response.Content.ReadAsStringAsync();
      var c = JsonSerializer.Deserialize<CourtOrderQuery>(json);
      Assert.IsNotNull(c);
      return c;
    }

    public static async Task<IEnumerable<CourtOrderQuery>> QueryCourtOrdersAsync(HttpClient client, bool includeFunds)
    {
      var response = await client.GetAsync(BlacklistManagerServer.Get.GetCourtOrder(null, includeFunds));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var json = await response.Content.ReadAsStringAsync();
      var c = JsonSerializer.Deserialize<CourtOrderQuery[]>(json);
      Assert.IsNotNull(c);
      return c;
    }

    public static async Task<CourtOrderQuery.Fund> QueryFundAsync(HttpClient client, string txId, long vOut)
    {
      var response = await client.GetAsync(BlacklistManagerServer.Get.GetTxOut(txId, vOut));
      Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
      var txOutJson = await response.Content.ReadAsStringAsync();
      var txOut = JsonSerializer.Deserialize<CourtOrderQuery.Fund>(txOutJson);
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

    public static void AreEqual(string expected, CourtOrderQuery.Fund fund)
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
