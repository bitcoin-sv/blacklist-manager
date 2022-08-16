// Copyright (c) 2020 Bitcoin Association

using System;
using System.Text.Json.Serialization;

namespace BlacklistManager.API.Rest.ViewModels
{
  public class MinerKeyViewModelGet
  {
    public MinerKeyViewModelGet() { }
    public MinerKeyViewModelGet(Domain.Models.DelegatingKey domainDelegatingKey)
    {
      MinerId = domainDelegatingKey.DelegatingKeyId;
      PublicKeyAddress = domainDelegatingKey.PublicKeyAddress;
      PublicKey = domainDelegatingKey.PublicKey;
      DataToSign = domainDelegatingKey.DataToSign;
      SignedPayload = domainDelegatingKey.SignedDelegatedKeyJSON;
      CreatedAt = domainDelegatingKey.CreatedAt;
      ValidatedAt = domainDelegatingKey.ValidatedAt;
      DelegatedPublicKey = domainDelegatingKey.DelegatedPublicKey;
    }
    [JsonPropertyName("minerId")]
    public int MinerId { get; set; }
    [JsonPropertyName("publicKeyAddress")]
    public string PublicKeyAddress { get; set; }
    [JsonPropertyName("publicKey")]
    public string PublicKey { get; set; }
    [JsonPropertyName("dataToSign")]
    public string DataToSign { get; set; }
    [JsonPropertyName("signedPayload")]
    public string SignedPayload { get; set; }
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }
    [JsonPropertyName("validatedAt")]
    public DateTime? ValidatedAt { get; set; }
    [JsonPropertyName("delegatedPublicKey")]
    public string DelegatedPublicKey { get; set; }
  }
}
