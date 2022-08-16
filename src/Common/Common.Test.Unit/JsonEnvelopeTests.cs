// Copyright (c) 2020 Bitcoin Association

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace Common.Test.Unit
{
  [TestClass]
  public class JsonEnvelopeTest
  {
    [TestMethod]
    public void TestVerifySampleJson()
    {
      // Sample from JSON Envelope Spec
      var document = @"{
      ""payload"": ""{\""name\"":\""simon\"",\""colour\"":\""blue\""}"",
      ""signature"": ""30450221008209b19ffe2182d859ce36fdeff5ded4b3f70ad77e0e8715238a539db97c1282022043b1a5b260271b7c833ca7c37d1490f21b7bd029dbb8970570c7fdc3df5c93ab"",
      ""publicKey"": ""02b01c0c23ff7ff35f774e6d3b3491a123afb6c98965054e024d2320f7dbd25d8a"",
      ""encoding"": ""UTF-8"",
      ""mimetype"": ""application/json""
      }";

      Assert.IsTrue(SignatureTools.VerifyJsonEnvelope(document));
    }

    [TestMethod]
    public void TestVerifySampleImage()
    {
      // Sample from JSON Envelope Spec
      var  document  = @"{
      ""payload"": ""/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAYEBQYFBAYGBQYHBwYIChAKCgkJChQODwwQFxQYGBcUFhYaHSUfGhsjHBYWICwgIyYnKSopGR8tMC0oMCUoKSj/2wBDAQcHBwoIChMKChMoGhYaKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCgoKCj/wgARCAAKAAoDASIAAhEBAxEB/8QAFwAAAwEAAAAAAAAAAAAAAAAAAQMEB//EABUBAQEAAAAAAAAAAAAAAAAAAAAC/9oADAMBAAIQAxAAAAGjQS5H/8QAGBABAQEBAQAAAAAAAAAAAAAAAgMEAAX/2gAIAQEAAQUC9HbYaJJKTmE+/8QAFhEBAQEAAAAAAAAAAAAAAAAAAgAR/9oACAEDAQE/AScv/8QAFBEBAAAAAAAAAAAAAAAAAAAAAP/aAAgBAgEBPwF//8QAHxAAAgECBwAAAAAAAAAAAAAAAQIDABEEEBMUITFR/9oACAEBAAY/AsUdwY2jI04/aQslmI5oMyKWHRtl/8QAGhABAAMAAwAAAAAAAAAAAAAAAQARMRBBUf/aAAgBAQABPyFOB4HPtd3KY4ovGpvYACnH/9oADAMBAAIAAwAAABDb/8QAFhEBAQEAAAAAAAAAAAAAAAAAAREA/9oACAEDAQE/EESrbv/EABYRAQEBAAAAAAAAAAAAAAAAAAEAEf/aAAgBAgEBPxBdv//EABsQAQACAgMAAAAAAAAAAAAAAAEAIRARgaHB/9oACAEBAAE/EDyaVPB5UlLVgU4Z1DdcVNmP/9k="",
      ""signature"": ""3045022100ebfde614a67d6f69c321664683b557a2eb605d7aa9357230684f49c1da4ccbef02203ab72beb9ffe1af76cb60b852b950baa2355c32ceb99715158e7e2d31a194f1d"",
      ""publicKey"": ""02aaee936deeb6d8296aa11d3134c624a2d8e72581ce49c73237f0359e4cf11949"",
      ""encoding"": ""base64"",
      ""mimetype"": ""image/jpeg""
      }";
      Assert.IsTrue(SignatureTools.VerifyJsonEnvelope(document));

    }

    [TestMethod]
    public void TestAndVerifyLegacyJSON()
    {
      var jsonTestPayload = File.ReadAllText("./TestData/LegacyJSONEnvelopeTestDocument.json");
      Assert.IsTrue(SignatureTools.VerifyJsonEnvelope(jsonTestPayload));
    }

    [TestMethod]
    public void TestVerifyJSON()
    {
      var jsonTestPayload = File.ReadAllText("./TestData/JSONEnvelopeTestDocument.json");
      Assert.IsTrue(SignatureTools.VerifyJsonEnvelope(jsonTestPayload));
    }

    [TestMethod]
    public void TestVerifyJSONBitcoinSignature()
    {
      //var jsonTestPayload = File.ReadAllText("./TestData/JSONEnvelopeBitcoinTestDocument.json");
      //Assert.IsTrue(SignatureTools.VerifyJsonEnvelope(jsonTestPayload));

      var key = new NBitcoin.Key();
      var wif = key.GetWif(NBitcoin.Network.Main);
      var t = "{\"name\":\"simon\",\"colour\":\"blue\"}";
      var jsonEnvelope = SignatureTools.CreateJSONWithBitcoinSignature(t, wif.ToWif(), NBitcoin.Network.Main);
      File.WriteAllText("./TestData/JSONEnvelopeBitcoinTestDocument.json", jsonEnvelope);

      jsonEnvelope = SignatureTools.CreateJSonSignature(t, wif.ToWif());
      File.WriteAllText("./TestData/JSONEnvelopeTestDocument.json", jsonEnvelope);
    }
  }
}
