// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Text.Json;

namespace Common.SmartEnums
{
  public class PurposeType : Enumeration
  {
    public static readonly PurposeType LegalEntityCommunication = new PurposeType(1, "legalEntityCommunication");

    public PurposeType(int id, string name) : base(id, name) { }

    public static explicit operator PurposeType(int id)
    {
      var purposeType = GetAll<PurposeType>().SingleOrDefault(x => x.Id == id);
      if (purposeType == null)
        throw new InvalidOperationException($"Invalid purpose type id '{id}'");
      return purposeType;
    }

    public static explicit operator PurposeType(string name)
    {
      var purposeType = GetAll<PurposeType>().SingleOrDefault(x => x.Name == name);
      if (purposeType == null)
        throw new InvalidOperationException($"Invalid purpose type name '{name}'");
      return purposeType;
    }

  }
  public class PurposeTypeConverterMicrosoft : System.Text.Json.Serialization.JsonConverter<PurposeType>
  {
    public override PurposeType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => (PurposeType)reader.GetString();

    public override void Write(Utf8JsonWriter writer, PurposeType value, JsonSerializerOptions options) => writer.WriteStringValue(value.Name);
  }

  public class PurposeTypeConverterNewtonsoft : Newtonsoft.Json.JsonConverter<PurposeType>
  {
    public override PurposeType ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, PurposeType existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
      string s = (string)reader.Value;
      return (PurposeType)s;
    }

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, PurposeType value, Newtonsoft.Json.JsonSerializer serializer)
    {
      writer.WriteValue(value.Name);
    }
  }
}
