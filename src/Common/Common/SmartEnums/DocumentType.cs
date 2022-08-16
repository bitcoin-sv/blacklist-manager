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

    public DocumentType(int id, string name) : base(id, name) { }

    public static bool operator !=(DocumentType a, DocumentType b)
    {
      if ((ReferenceEquals(a, null) && !ReferenceEquals(b, null)) ||
          (!ReferenceEquals(a, null) && ReferenceEquals(b, null)))
      {
        return true;
      }

      if (!ReferenceEquals(a, null) && !ReferenceEquals(b, null))
      {
        return a.Id != b.Id;
      }
      return false;
    }
    public static bool operator ==(DocumentType a, DocumentType b)
    {
      if (ReferenceEquals(a, null) && ReferenceEquals(b, null))
      {
        return true;
      }

      if (!ReferenceEquals(a, null) && !ReferenceEquals(b, null))
      {
        return a.Id == b.Id;
      }

      return false;
    }

    public static implicit operator string(DocumentType documentType) => documentType.Name;

    public static implicit operator int(DocumentType documentType) => documentType.Id;

    public static explicit operator DocumentType(int id)
    {
      var docType = GetAll<DocumentType>().SingleOrDefault(x => x.Id == id);
      if (docType == null)
        throw new Exception($"Invalid document type id '{id}'");
      return docType;
    }

    public static explicit operator DocumentType(string name)
    {
      var docType = GetAll<DocumentType>().SingleOrDefault(x => x.Name== name);
      if (docType == null)
        throw new Exception($"Invalid document type name '{name}'");
      return docType;
    }

    public override bool Equals(object obj)
    {
      return this.Id == ((DocumentType)obj).Id;
    }

    public override int GetHashCode()
    {
      return Id.GetHashCode();
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
