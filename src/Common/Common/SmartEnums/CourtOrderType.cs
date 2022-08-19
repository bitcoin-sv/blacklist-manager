using System;
using System.Linq;

namespace Common.SmartEnums
{
  public class CourtOrderType : Enumeration
  {
    public static readonly CourtOrderType Freeze = new(1, "freezeOrder");
    public static readonly CourtOrderType Unfreeze = new(2, "unfreezeOrder");
    public static readonly CourtOrderType Confiscation = new(3, "confiscationOrder");

    public CourtOrderType(int id, string name) : base(id, name) { }

    public static explicit operator CourtOrderType(int id)
    {
      var docType = GetAll<CourtOrderType>().SingleOrDefault(x => x.Id == id);
      if (docType == null)
        throw new InvalidOperationException($"Invalid court order type id '{id}'");
      return docType;
    }

    public static explicit operator CourtOrderType(string name)
    {
      var docType = GetAll<CourtOrderType>().SingleOrDefault(x => x.Name == name);
      if (docType == null)
        throw new InvalidOperationException($"Invalid court order type name '{name}'");
      return docType;
    }

    public DocumentType ToDocumentType()
    {
      if (Id == CourtOrderType.Freeze.Id)
      {
        return DocumentType.FreezeOrder;
      }
      else if (Id == CourtOrderType.Unfreeze.Id)
      {
        return DocumentType.UnfreezeOrder;
      }
      else if (Id == CourtOrderType.Confiscation.Id)
      {
        return DocumentType.ConfiscationEnvelope;
      }
      throw new InvalidOperationException($"Invalid court order type '{Id}'");
    }
  }
}
