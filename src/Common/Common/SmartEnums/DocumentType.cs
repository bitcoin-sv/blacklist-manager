// Copyright (c) 2020 Bitcoin Association

using System;
using System.Linq;
using System.Text.Json;

namespace Common.SmartEnums
{
  public class DocumentType : Enumeration
  {
    public static readonly DocumentType FreezeOrder = new DocumentType(1, "freezeOrder");
    public static readonly DocumentType UnfreezeOrder = new DocumentType(2, "unfreezeOrder");
    public static readonly DocumentType DelegatedKeys = new DocumentType(3, "delegatedKeys");
    public static readonly DocumentType CourtOrderAcceptance = new DocumentType(4, "courtOrderAcceptance");
    public static readonly DocumentType ConsensusActivation = new DocumentType(5, "consensusActivation");
    public static readonly DocumentType ConfiscationOrder = new DocumentType(6, "confiscationOrder");
    public static readonly DocumentType ConfiscationTxDocument = new DocumentType(7, "confiscationTxDocument");
    public static readonly DocumentType ConfiscationEnvelope = new DocumentType(8, "confiscationEnvelope");
    public static readonly DocumentType CancelConfiscationOrder = new DocumentType(9, "cancelConfiscationOrder");

    public DocumentType(int id, string name) : base(id, name) { }

    public static explicit operator DocumentType(int id)
    {
      var docType = GetAll<DocumentType>().SingleOrDefault(x => x.Id == id);
      if (docType == null)
        throw new InvalidOperationException($"Invalid document type id '{id}'");
      return docType;
    }

    public static explicit operator DocumentType(string name)
    {
      var docType = GetAll<DocumentType>().SingleOrDefault(x => x.Name== name);
      if (docType == null)
        throw new InvalidOperationException($"Invalid document type name '{name}'");
      return docType;
    }

    public static explicit operator DocumentType(CourtOrderType courtOrderType)
    {
      if (courtOrderType == CourtOrderType.Freeze)
      {
        return DocumentType.FreezeOrder;
      }
      else if (courtOrderType == CourtOrderType.Unfreeze)
      {
        return DocumentType.UnfreezeOrder;
      }
      else if (courtOrderType == CourtOrderType.Confiscation)
      {
        return DocumentType.ConfiscationOrder;
      }
      else
      {
        throw new InvalidOperationException($"Conversion from CourtOrderType ({courtOrderType}) to DocumentType not supported.");
      }
    }
  }

  public class DocumentTypeConverterMicrosoft : System.Text.Json.Serialization.JsonConverter<DocumentType>
  {
    public override DocumentType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => (DocumentType)reader.GetString();

    public override void Write(Utf8JsonWriter writer, DocumentType value, JsonSerializerOptions options) => writer.WriteStringValue(value.Name);
  }

  public class DocumentTypeConverterNewtonsoft : Newtonsoft.Json.JsonConverter<DocumentType>
  {
    public override DocumentType ReadJson(Newtonsoft.Json.JsonReader reader, Type objectType, DocumentType existingValue, bool hasExistingValue, Newtonsoft.Json.JsonSerializer serializer)
    {
      string s = (string)reader.Value;
      return (DocumentType)s;
    }

    public override void WriteJson(Newtonsoft.Json.JsonWriter writer, DocumentType value, Newtonsoft.Json.JsonSerializer serializer)
    {
      writer.WriteValue(value.Name);
    }
  }
}
