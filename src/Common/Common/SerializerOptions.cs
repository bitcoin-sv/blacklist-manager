// Copyright (c) 2020 Bitcoin Association

using Common.SmartEnums;
using System.Text.Json;

namespace Common
{
  public static class SerializerOptions
  {
    public static JsonSerializerOptions SerializeOptionsNoPrettyPrint
    {
      get
      {
        var options = new JsonSerializerOptions
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          // \u0022 -> \"
          Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new DocumentTypeConverterMicrosoft());
        options.Converters.Add(new PurposeTypeConverterMicrosoft());
        return options;
      }
    }

    public static JsonSerializerOptions SerializeOptions
    {
      get
      {
        var options = new JsonSerializerOptions
        {
          PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
          WriteIndented = true,
          // \u0022 -> \"
          Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.Converters.Add(new DocumentTypeConverterMicrosoft());
        options.Converters.Add(new PurposeTypeConverterMicrosoft());
        return options;
      }
    }
  }
}
