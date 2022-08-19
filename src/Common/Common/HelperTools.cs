// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Common.BitcoinRpcClient;
using Npgsql;

namespace Common
{
  public static class HelperTools
  {
    const int BufferChunkSize = 1024 * 1024;
    public static async Task<byte[]> HexStringToByteArrayAsync(Stream stream)
    {
      IList<byte> outputBuffer = new List<byte>();
      using var strReader = new StreamReader(stream);
      do
      {
        var chBuffer = new char[BufferChunkSize];
        var readSize = await strReader.ReadBlockAsync(chBuffer, 0, BufferChunkSize);
        for (int i = 0; i < (readSize / 2);  i++)
        {
          var hexChar = new char[] { chBuffer[i * 2], chBuffer[i * 2 + 1] };
          var byteVal = int.Parse(hexChar, NumberStyles.AllowHexSpecifier);
          if (byteVal < Byte.MinValue || byteVal > Byte.MaxValue)
            throw new OverflowException($"Byte value exceeds limits 0-255");
          outputBuffer.Add((byte)byteVal);
        }
      }
      while (!strReader.EndOfStream);

      return outputBuffer.ToArray();
    }

    // https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/26304129#26304129
    public static byte[] HexStringToByteArray(string input)
    {
      var outputLength = input.Length / 2;
      var output = new byte[outputLength];
      for (var i = 0; i < outputLength; i++)
        output[i] = Convert.ToByte(input.Substring(i * 2, 2), 16);
      return output;
    }

    // https://stackoverflow.com/questions/311165/how-do-you-convert-a-byte-array-to-a-hexadecimal-string-and-vice-versa/14333437#14333437
    // changed the formula to (87 + b + (((b - 10) >> 31) & -39) to get lowercase characters
    public static string ByteToHexString(byte[] bytes)
    {
      char[] c = new char[bytes.Length * 2];
      int b;
      for (int i = 0; i < bytes.Length; i++)
      {
        b = bytes[i] >> 4;
        c[i * 2] = (char)(87 + b + (((b - 10) >> 31) & -39));
        b = bytes[i] & 0xF;
        c[i * 2 + 1] = (char)(87 + b + (((b - 10) >> 31) & -39));
      }
      return new string(c);
    }

    public static Block ParseByteStreamToBlock(RPCBitcoinStreamReader streamReader)
    {
      // Create or own MemoryStream, so that we support bigger blocks
      BitcoinStream s = new BitcoinStream(streamReader, false);
      s.MaxArraySize = unchecked((int)uint.MaxValue); // NBitcoin internally casts to uint when comparing

      var block = Consensus.Main.ConsensusFactory.CreateBlock();
      block.ReadWrite(s);
      streamReader.Close();
      return block;
    }

    public static string ScriptPubKeyHexToHash(string hex)
    {
      return AddressUtils.GetScriptHashHex(HexStringToByteArray(hex));
    }

    public static DateTime GetEpochTime(long dateValue)
    {
      var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      return epoch.Add(TimeSpan.FromSeconds(dateValue));
    }

    public static BitcoinNetwork GetBitcoinNetwork(string bitcoinNetwork)
    {
      switch (bitcoinNetwork)
      {
        case "TestNet":
          return BitcoinNetwork.Testnet;
        case "Main":
          return BitcoinNetwork.Mainnet;
        case "RegTest":
          return BitcoinNetwork.Regtest;
        case "STN":
          return BitcoinNetwork.STN;
        default:
          throw new Exception($"BitcoinNetwork '{bitcoinNetwork}' not supported.");
      }
    }

    public async static Task ExecuteWithRetriesAsync(int noOfRetries, string errorMessage, Func<Task> methodToExecute, int sleepTimeBetweenRetries = 1000)
    {
      try
      {
        do
        {
          noOfRetries--;
          try
          {
            await methodToExecute();
            return;
          }
          catch
          {
            Thread.Sleep(sleepTimeBetweenRetries);
            if (noOfRetries == 0)
            {
              throw;
            }
          }
        }
        while (noOfRetries > 0);
      }
      catch (Exception ex)
      {
        if (!string.IsNullOrEmpty(errorMessage))
          throw new Exception(errorMessage, ex);
        throw;
      }
    }

    public static string JSONSerializeNewtonsoft(object value)
    {
      DefaultContractResolver contractResolver = new DefaultContractResolver
      {
        NamingStrategy = new CamelCaseNamingStrategy()
      };

      JsonSerializerSettings serializeSettings =
        new JsonSerializerSettings
        {
          ContractResolver = contractResolver,
          Formatting = Formatting.Indented,
          NullValueHandling = NullValueHandling.Ignore,
          Converters = new List<JsonConverter> { new DocumentTypeConverterNewtonsoft(), new PurposeTypeConverterNewtonsoft() }
        };

      return Newtonsoft.Json.JsonConvert.SerializeObject(value, serializeSettings);
    }

    public static T JSONDeserializeNewtonsoft<T>(string value)
    {
      DefaultContractResolver contractResolver = new DefaultContractResolver
      {
        NamingStrategy = new CamelCaseNamingStrategy()
      };

      JsonSerializerSettings serializeSettings =
        new JsonSerializerSettings
        {
          ContractResolver = contractResolver,
          Formatting = Formatting.Indented,
          NullValueHandling = NullValueHandling.Ignore,
          Converters = new List<JsonConverter> { new DocumentTypeConverterNewtonsoft(), new PurposeTypeConverterNewtonsoft() }
        };

      return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(value, serializeSettings);
    }

    public static string CreateTestSignature(string privateKeyWif)
    {
      DateTime signedDate = DateTime.UtcNow;

      string testJson = $"This is test document. UTC time is '{signedDate.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.CreateSpecificCulture("en-US"))}'.";
      return SignatureTools.CreateJSonSignature(testJson, privateKeyWif);
    }

    public static string GetPublicKey(string privateKeyWif, Network network)
    {
      var key = Key.Parse(privateKeyWif, network);
      return key.PubKey.ToHex();
    }

    public static bool IsValidEmail(string email)
    {
      if (string.IsNullOrWhiteSpace(email))
        return false;

      try
      {
        // Normalize the domain
        email = Regex.Replace(email, @"(@)(.+)$", DomainMapper,
                              RegexOptions.None, TimeSpan.FromMilliseconds(200));

        // Examines the domain part of the email and normalizes it.
        string DomainMapper(Match match)
        {
          // Use IdnMapping class to convert Unicode domain names.
          var idn = new IdnMapping();

          // Pull out and process domain name (throws ArgumentException on invalid)
          string domainName = idn.GetAscii(match.Groups[2].Value);

          return match.Groups[1].Value + domainName;
        }
      }
      catch (RegexMatchTimeoutException)
      {
        return false;
      }
      catch (ArgumentException)
      {
        return false;
      }

      try
      {
        return Regex.IsMatch(email,
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(250));
      }
      catch (RegexMatchTimeoutException)
      {
        return false;
      }
    }

    public static string ConvertFromBase64ToHex(string base64String)
    {
      return ByteToHexString(Convert.FromBase64String(base64String));
    }

    public static bool TryConvertFromBase64ToHex(string base64String, out string hexString)
    {
      try
      {
        hexString = ByteToHexString(Convert.FromBase64String(base64String));
        return true;
      }
      catch (Exception)
      {
        hexString = String.Empty;
        return false;
      }
    }

    public static string ConvertFromHexToBase64(string hexString)
    {
      return Convert.ToBase64String(HexStringToByteArray(hexString));
    }

    public static void ShuffleArray<T>(T[] array)
    {
      int n = array.Length;
      while (n > 1)
      {
        int i = System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, n--);
        T temp = array[n];
        array[n] = array[i];
        array[i] = temp;
      }
    }

    public static async Task<NpgsqlConnection> OpenNpgSQLConnectionAsync(string connectionString)
    {
      var connection = new NpgsqlConnection(connectionString);
      await RetryUtils.ExecuteWithRetriesAsync(3, null, () => connection.OpenAsync());

      return connection;
    }
  }
}
