// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.API.Rest.ViewModels;
using BlacklistManager.Infrastructure.Repositories;
using BlacklistManager.Test.Functional.Server;
using Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NBitcoin;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;

namespace BlacklistManager.Test.Functional
{
  [TestClass]
  public class SigningKeyRest : TestBase
  {
    private Network BitcoinNetwork;
    [TestInitialize]
    public async Task TestInitializeAsync()
    {
      await InitializeAsync(mockedServices: true);
      BitcoinNetwork = Network.GetNetwork(Configuration["AppSettings:BitcoinNetwork"]);
    }

    [TestCleanup]
    public void TestCleanup()
    {
      base.Cleanup();
    }

    [TestMethod]
    public async Task ImportSignerKeyWithoutKeyReturns400Async()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;

      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(new SignerKeyViewModelCreate()), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetMinerKeysWithInvalidIdReturns404Async()
    {
      string uri = $"{BlacklistManagerServer.ApiSigningKeyEndpointUrl}/minerKey?minerId=10000000";

      var response = await client.GetAsync(uri);

      Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetSignerKeysWithInvalidIdReturns404Async()
    {
      string uri = $"{BlacklistManagerServer.ApiSigningKeyEndpointUrl}?signerId=10000000";

      var response = await client.GetAsync(uri);

      Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task ImportSignerKeyAsync()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new NBitcoin.Key().GetBitcoinSecret(BitcoinNetwork).ToWif(), 
        DelegationRequired = true
      };
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();

      response = await client.GetAsync(uri);

      Assert.IsTrue(response.IsSuccessStatusCode);
      var responseContent = await response.Content.ReadAsStringAsync();
      var signerGet = JsonConvert.DeserializeObject<IEnumerable<SignerKeyViewModelGet>>(responseContent);
      Assert.IsTrue(signerGet.Any(x => x.SignerId == int.Parse(id)));
    }

    [TestMethod]
    public async Task ImportAndValidateMinerKeyWithJSONSignatureAsync()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new Key().GetBitcoinSecret(BitcoinNetwork).ToWif(), 
        DelegationRequired = true
      };
      // Import signer key (privateKey/delegatedKey)
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();

      var minerPrivateKey = new Key();
      var minerKeyCreate = new MinerKeyViewModelCreate
      {
        PublicKey = minerPrivateKey.PubKey.ToHex()
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);

      // Sign the payload
      var jsonEnvelopeString = SignatureTools.CreateJSonSignature(minerKey.DataToSign, minerPrivateKey.GetBitcoinSecret(BitcoinNetwork).ToWif());
      var jsonEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(jsonEnvelopeString, SerializerOptions.SerializeOptions);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = jsonEnvelope.Signature,
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);

      string getUri = $"{uri}/minerKey?minerId={minerKey.MinerId}";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<MinerKeyViewModelGet>>(minerKeyResponse).SingleOrDefault();

      Assert.IsNotNull(minerKeyFromGet);
      Assert.AreEqual(minerKeyFromGet.MinerId, minerKey.MinerId);
      Assert.IsTrue(minerKeyFromGet.ValidatedAt.HasValue);
      Assert.IsNotNull(minerKeyFromGet.SignedPayload);
      Assert.IsNotNull(minerKeyFromGet.PublicKey);
      Assert.IsNotNull(minerKeyFromGet.CreatedAt);

      var signatureJson = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(minerKeyFromGet.SignedPayload, SerializerOptions.SerializeOptions);
      Assert.IsNotNull(signatureJson);
      Assert.IsNotNull(signatureJson.Payload);

      getUri = $"{uri}?signerId={id}";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      var signerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<SignerKeyViewModelGet>>(minerKeyResponse).SingleOrDefault();
      Assert.IsNotNull(signerKeyFromGet);
      Assert.IsTrue(signerKeyFromGet.IsActive);
    }

    [TestMethod]
    public async Task ImportAndValidateMinerKeyWithBitcoinSignatureAsync()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new NBitcoin.Key().GetBitcoinSecret(BitcoinNetwork).ToWif(),
        DelegationRequired = true
      };
      // Import signer key (privateKey/delegatedKey)
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();

      var minerPrivateKey = new NBitcoin.Key();
      var minerKeyCreate = new MinerKeyViewModelCreate
      {
        PublicKeyAddress = minerPrivateKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, BitcoinNetwork).ToString() 
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);

      // Sign the payload
      var envelopeString = SignatureTools.CreateJSONWithBitcoinSignature(minerKey.DataToSign, minerPrivateKey.GetBitcoinSecret(BitcoinNetwork).ToWif(), BitcoinNetwork);
      var bitcoinSignatureEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(envelopeString, SerializerOptions.SerializeOptions);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = HelperTools.ConvertFromHexToBase64(bitcoinSignatureEnvelope.Signature),
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));
      //var r = await response.Content.ReadAsStringAsync();

      Assert.IsTrue(response.IsSuccessStatusCode);

      string getUri = $"{uri}/minerKey?minerId={minerKey.MinerId}";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<MinerKeyViewModelGet>>(minerKeyResponse).SingleOrDefault();

      Assert.IsNotNull(minerKeyFromGet);
      Assert.AreEqual(minerKeyFromGet.MinerId, minerKey.MinerId);
      Assert.IsTrue(minerKeyFromGet.ValidatedAt.HasValue);
      Assert.IsNotNull(minerKeyFromGet.SignedPayload);
      Assert.IsNotNull(minerKeyFromGet.PublicKeyAddress);
      Assert.IsNotNull(minerKeyFromGet.CreatedAt);

      var signatureJson = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(minerKeyFromGet.SignedPayload, SerializerOptions.SerializeOptions);
      Assert.IsNotNull(signatureJson);
      Assert.IsNotNull(signatureJson.Payload);

      getUri = $"{uri}?signerId={id}";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      var signerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<SignerKeyViewModelGet>>(minerKeyResponse).SingleOrDefault();
      Assert.IsNotNull(signerKeyFromGet);
      Assert.IsTrue(signerKeyFromGet.IsActive);
    }

    [TestMethod]
    public async Task ImportMinerKeyMissingPKAndPKAddressReturns400Async()
    {
      string postUri = $"{BlacklistManagerServer.ApiSigningKeyEndpointUrl}/1/minerKey";
      var response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(new MinerKeyViewModelCreate()), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(!response.IsSuccessStatusCode);
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = await response.Content.ReadAsStringAsync();

      var problem = JsonConvert.DeserializeObject<ProblemDetails>(responseString);
      Assert.AreEqual(problem.Detail, "'publicKey' or 'publicKeyAddress' must be set.");
    }

    [TestMethod]
    public async Task ImportInvalidPublicKeyReturns400Async()
    {
      string postUri = $"{BlacklistManagerServer.ApiSigningKeyEndpointUrl}/1/minerKey";
      var response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(new MinerKeyViewModelCreate { PublicKey = "1"}), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(!response.IsSuccessStatusCode);
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = await response.Content.ReadAsStringAsync();

      var problem = JsonConvert.DeserializeObject<ProblemDetails>(responseString);
      Assert.AreEqual("Invalid 'publicKey'", problem.Detail);
    }

    [TestMethod]
    public async Task ImportInvalidPublicKeyAddressReturns400Async()
    {
      string postUri = $"{BlacklistManagerServer.ApiSigningKeyEndpointUrl}/1/minerKey";
      var response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(new MinerKeyViewModelCreate { PublicKeyAddress = "1" }), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(!response.IsSuccessStatusCode);
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = await response.Content.ReadAsStringAsync();

      var problem = JsonConvert.DeserializeObject<ProblemDetails>(responseString);
      Assert.AreEqual(problem.Detail, "Invalid 'publicKeyAddress'");
    }

    [TestMethod]
    public async Task ImportAndValidateReturns400ForInvalidAddressAsync()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new NBitcoin.Key().GetBitcoinSecret(BitcoinNetwork).ToWif(),
        DelegationRequired = true
      };
      // Import signer key (privateKey/delegatedKey)
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();

      var minerPrivateKey = new NBitcoin.Key();
      var minerKeyCreate = new MinerKeyViewModelCreate
      {
        PublicKeyAddress = minerPrivateKey.PubKey.GetAddress(ScriptPubKeyType.Legacy, BitcoinNetwork).ToString()
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);

      // Sign the payload with invalid Key
      var otherPrivateKey = new NBitcoin.Key();

      var envelopeString = SignatureTools.CreateJSONWithBitcoinSignature(minerKey.DataToSign, otherPrivateKey.GetBitcoinSecret(BitcoinNetwork).ToWif(), BitcoinNetwork);
      var bitcoinSignatureEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(envelopeString, SerializerOptions.SerializeOptions);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = HelperTools.ConvertFromHexToBase64(bitcoinSignatureEnvelope.Signature),
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = await response.Content.ReadAsStringAsync();
      var problem = JsonConvert.DeserializeObject<ProblemDetails>(responseString);
      Assert.AreEqual("Signature is invalid.", problem.Detail);
    }

    [TestMethod]
    public async Task ImportAndValidateReturns400ForInvalidPublicKeyAsync()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new NBitcoin.Key().GetBitcoinSecret(BitcoinNetwork).ToWif(),
        DelegationRequired = true
      };
      // Import signer key (privateKey/delegatedKey)
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();

      // Create new key to generate false public key
      var otherPrivateKey = new NBitcoin.Key();

      var minerPrivateKey = new NBitcoin.Key();
      var minerKeyCreate = new MinerKeyViewModelCreate
      {
        PublicKey = otherPrivateKey.PubKey.ToHex()
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);


      var jsonEnvelopeString = SignatureTools.CreateJSonSignature(minerKey.DataToSign, minerPrivateKey.GetBitcoinSecret(BitcoinNetwork).ToWif());
      var jsonEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(jsonEnvelopeString, SerializerOptions.SerializeOptions);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = jsonEnvelope.Signature,
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = await response.Content.ReadAsStringAsync();
      var problem = JsonConvert.DeserializeObject<ProblemDetails>(responseString);
      Assert.AreEqual("Signature is not valid.", problem.Detail);
    }

    [TestMethod]
    public async Task ImportAndValidateReturns400ForInvalidJsonSignatureAsync()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new NBitcoin.Key().GetBitcoinSecret(BitcoinNetwork).ToWif(),
        DelegationRequired = true
      };
      // Import signer key (privateKey/delegatedKey)
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();

      // Sign the payload with invalid Key
      var otherPrivateKey = new NBitcoin.Key();

      var minerPrivateKey = new NBitcoin.Key();
      var minerKeyCreate = new MinerKeyViewModelCreate
      {
        PublicKey = minerPrivateKey.PubKey.ToHex()
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);


      var jsonEnvelopeString = SignatureTools.CreateJSonSignature(minerKey.DataToSign, otherPrivateKey.GetBitcoinSecret(BitcoinNetwork).ToWif());
      var jsonEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(jsonEnvelopeString, SerializerOptions.SerializeOptions);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = jsonEnvelope.Signature,
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
      var responseString = await response.Content.ReadAsStringAsync();
      var problem = JsonConvert.DeserializeObject<ProblemDetails>(responseString);
      Assert.AreEqual("Signature is not valid.", problem.Detail);
    }

    [TestMethod]
    public async Task Import2Active1InactiveDelegatingKeysAsync()
    {
      string uri = BlacklistManagerServer.ApiSigningKeyEndpointUrl;
      var signerKey = new SignerKeyViewModelCreate
      {
        PrivateKey = new NBitcoin.Key().GetBitcoinSecret(BitcoinNetwork).ToWif(),
        DelegationRequired = true
      };
      // Import signer key (privateKey/delegatedKey)
      var response = await client.PostAsync(uri, new StringContent(JsonConvert.SerializeObject(signerKey), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      var id = await response.Content.ReadAsStringAsync();


      #region 1st key that will not be signed
      //Create minerkey that will not be signed
      var minerPrivateKey1 = new NBitcoin.Key();
      var minerKeyCreate1 = new MinerKeyViewModelCreate
      {
        PublicKeyAddress = minerPrivateKey1.PubKey.GetAddress(ScriptPubKeyType.Legacy, BitcoinNetwork).ToString()
      };

      // Import miner key (publicKey/delegatingKey)
      string postUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate1), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      #endregion

      #region 2nd key that will be signed but will not yet activate the key
      // Create 2nd miner key that will be signed
      var minerPrivateKey2 = new NBitcoin.Key();
      var minerKeyCreate2 = new MinerKeyViewModelCreate
      {
        PublicKeyAddress = minerPrivateKey2.PubKey.GetAddress(ScriptPubKeyType.Legacy, BitcoinNetwork).ToString()
      };

      // Import miner key (publicKey/delegatingKey)
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate2), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);

      // Sign the payload
      var envelopeString = SignatureTools.CreateJSONWithBitcoinSignature(minerKey.DataToSign, minerPrivateKey2.GetBitcoinSecret(BitcoinNetwork).ToWif(), BitcoinNetwork);
      var bitcoinSignatureEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(envelopeString, SerializerOptions.SerializeOptions);

      var minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = false,
        Signature = HelperTools.ConvertFromHexToBase64(bitcoinSignatureEnvelope.Signature),
      };

      // Post signature for delegatingKey
      string putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      #endregion

      string getUri = $"{uri}/minerKey";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      var minerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<MinerKeyViewModelGet>>(minerKeyResponse);

      Assert.AreEqual(2, minerKeyFromGet.Count());
      Assert.AreEqual(1, minerKeyFromGet.Count(x => x.ValidatedAt.HasValue));

      // Check that signerKey (delegatedKey) is not active yet
      getUri = $"{uri}?signerId={id}";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      var signerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<SignerKeyViewModelGet>>(minerKeyResponse).SingleOrDefault();
      Assert.IsNotNull(signerKeyFromGet);
      Assert.IsFalse(signerKeyFromGet.IsActive);

      #region 3rd key that will be signed and will activate the key
      // Create 2nd miner key that will be signed
      var minerPrivateKey3 = new NBitcoin.Key();
      var minerKeyCreate3 = new MinerKeyViewModelCreate
      {
        PublicKeyAddress = minerPrivateKey3.PubKey.GetAddress(ScriptPubKeyType.Legacy, BitcoinNetwork).ToString()
      };

      // Import miner key (publicKey/delegatingKey)
      response = await client.PostAsync(postUri, new StringContent(JsonConvert.SerializeObject(minerKeyCreate3), Encoding.UTF8, MediaTypeNames.Application.Json));
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      minerKey = JsonConvert.DeserializeObject<MinerKeyViewModelGet>(minerKeyResponse);

      // Sign the payload
      envelopeString = SignatureTools.CreateJSONWithBitcoinSignature(minerKey.DataToSign, minerPrivateKey3.GetBitcoinSecret(BitcoinNetwork).ToWif(), BitcoinNetwork);
      bitcoinSignatureEnvelope = System.Text.Json.JsonSerializer.Deserialize<JsonEnvelope>(envelopeString, SerializerOptions.SerializeOptions);

      minerKeyUpdate = new MinerKeyViewModelUpdate
      {
        Id = minerKey.MinerId,
        ActivateKey = true,
        Signature = HelperTools.ConvertFromHexToBase64(bitcoinSignatureEnvelope.Signature),
      };

      // Post signature for delegatingKey
      putUri = $"{uri}/{int.Parse(id)}/minerKey";
      response = await client.PutAsync(putUri, new StringContent(JsonConvert.SerializeObject(minerKeyUpdate), Encoding.UTF8, MediaTypeNames.Application.Json));

      Assert.IsTrue(response.IsSuccessStatusCode);
      #endregion

      getUri = $"{uri}/minerKey";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      minerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<MinerKeyViewModelGet>>(minerKeyResponse);

      Assert.AreEqual(3, minerKeyFromGet.Count());
      Assert.AreEqual(2, minerKeyFromGet.Count(x => x.ValidatedAt.HasValue));

      // Check that signerKey (delegatedKey) is active 
      getUri = $"{uri}?signerId={id}";
      response = await client.GetAsync(getUri);
      Assert.IsTrue(response.IsSuccessStatusCode);
      minerKeyResponse = await response.Content.ReadAsStringAsync();
      signerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<SignerKeyViewModelGet>>(minerKeyResponse).SingleOrDefault();
      Assert.IsNotNull(signerKeyFromGet);
      Assert.IsTrue(signerKeyFromGet.IsActive);
    }

    [TestMethod]
    public async Task ReplaceActiveSignerKeyAsync()
    {
      CourtOrderRepositoryPostgres.EmptyRepository(Configuration["BlacklistManagerConnectionStrings:DBConnectionString"]);
      await ImportAndValidateMinerKeyWithBitcoinSignatureAsync();
      await ImportAndValidateMinerKeyWithBitcoinSignatureAsync();

      var response = await client.GetAsync(BlacklistManagerServer.ApiSigningKeyEndpointUrl);
      Assert.IsTrue(response.IsSuccessStatusCode);
      var minerKeyResponse = await response.Content.ReadAsStringAsync();
      var signerKeyFromGet = JsonConvert.DeserializeObject<IEnumerable<SignerKeyViewModelGet>>(minerKeyResponse);
      Assert.AreEqual(2, signerKeyFromGet.Count());
      Assert.AreEqual(1, signerKeyFromGet.Count(x => x.IsActive));
    }
  }
}
