// Copyright (c) 2020 Bitcoin Association

using NBitcoin.Crypto;
using Sodium;
using System.Text;
using System.Linq;
using System;

namespace Common
{
  public static class EncryptionTools
  {
    const int NONCE_BYTES = 24;
    private static byte[] GetEncryptionKeys(string key)
    {
      var keyHash = Hashes.SHA256(Encoding.UTF8.GetBytes(key));

      return keyHash;
    }

    public static byte[] Encrypt(string text, string key)
    {
      var keyHash = GetEncryptionKeys(key);
      var nonce = SecretBox.GenerateNonce();
      var encryptedText = SecretBox.Create(Encoding.UTF8.GetBytes(text), nonce, keyHash);
      return nonce.Concat(encryptedText).ToArray();
    }

    public static string Decrypt(byte[] encrypted, string key)
    {
      var keyHash = GetEncryptionKeys(key);
      byte[] nonce = new byte[NONCE_BYTES];
      byte[] encryptedText = new byte[encrypted.Length - NONCE_BYTES];
      Array.Copy(encrypted, nonce, NONCE_BYTES);
      Array.Copy(encrypted, NONCE_BYTES, encryptedText, 0, encrypted.Length - NONCE_BYTES);

      return Encoding.UTF8.GetString(SecretBox.Open(encryptedText, nonce, keyHash));
    }
  }
}
