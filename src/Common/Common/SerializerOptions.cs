// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common
{
  public static class SerializerOptions
  {
    private static JsonSerializerOptions _optionWithoutPrettyPrint;
    /// <summary>
    /// This option should be used if JSON result will be used as a string payload in some other JSON document
    /// </summary>
    public static JsonSerializerOptions SerializeOptionsNoPrettyPrint
    {
      get
      {
        if (_optionWithoutPrettyPrint == null)
        {
          _optionWithoutPrettyPrint = new JsonSerializerOptions
          {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault,
            // \u0022 -> \"
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
          };
          _optionWithoutPrettyPrint.Converters.Add(new DocumentTypeConverterMicrosoft());
          _optionWithoutPrettyPrint.Converters.Add(new PurposeTypeConverterMicrosoft());
        }
        return _optionWithoutPrettyPrint;
      }
    }

    private static JsonSerializerOptions _optionWithPrettyPrint;
    /// <summary>
    /// This option should be used if JSON is to be returned by some API
    /// </summary>
    public static JsonSerializerOptions SerializeOptions
    {
      get
      {
        if (_optionWithPrettyPrint == null)
        {
          _optionWithPrettyPrint = new JsonSerializerOptions
          {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            // \u0022 -> \"
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
          };
          _optionWithPrettyPrint.Converters.Add(new DocumentTypeConverterMicrosoft());
          _optionWithPrettyPrint.Converters.Add(new PurposeTypeConverterMicrosoft());
        }
        return _optionWithPrettyPrint;
      }
    }
  }

  public class NullableBoolOrIntConverter : JsonConverter<int?>
  {
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
      return reader.TokenType switch
      {
        JsonTokenType.True => 1,
        JsonTokenType.False => 0,
        JsonTokenType.Number => reader.GetInt32(),
        JsonTokenType.Null => null,
        _ => throw new JsonException($"Invalid value for property at position {reader.TokenStartIndex}"),
      };
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
      if (value == null)
      {
        if (options.DefaultIgnoreCondition == JsonIgnoreCondition.WhenWritingNull)
        {
          writer.WriteNullValue();
          return;
        }
        return;
      }

      writer.WriteNumberValue(value.Value);
    }
  }
}
