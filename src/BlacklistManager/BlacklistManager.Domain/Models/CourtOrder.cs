// Copyright (c) 2020 Bitcoin Association

using Common;
using Common.SmartEnums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  public class CourtOrder
  {
    public CourtOrder(
      string courtOrderId, string courtOrderHash, DocumentType documentType)      
    {
      funds = new List<Fund>();
      Status = CourtOrderStatus.Imported;

      CourtOrderId = courtOrderId;
      CourtOrderHash = courtOrderHash;
      DocumentType = (DocumentType)documentType;
      Type = ToCourtOrderType(DocumentType); 
    }

    public CourtOrder(
      string courtOrderId, string courtOrderHash, DocumentType documentType, 
      DateTime? validFrom, DateTime? validTo)
      : this(courtOrderId, courtOrderHash, documentType)
    {
      ValidFrom = validFrom;
      ValidTo = validTo;
    }

    public CourtOrder(
      string courtOrderId, string courtOrderHash, DocumentType documentType, 
      DateTime? validFrom, DateTime? validTo,
      string freezeCourtOrderId,
      string frezeCourtOrderHash)
      : this(courtOrderId, courtOrderHash, documentType, validFrom, validTo)
    {
      FreezeCourtOrderId = freezeCourtOrderId;
      FreezeCourtOrderHash = frezeCourtOrderHash;
    }

    public CourtOrder(
      Int32 courtOrderType, string courtOrderId, DateTime? validFrom, DateTime? validTo,
      string courtOrderHash, Int32? enforceAtHeight, Int32 courtOrderStatus,
      string freezeCourtOrderId, string freezeCourtOrderHash)
      : this(courtOrderId, courtOrderHash, ToDocumentType(courtOrderType), validFrom, validTo,freezeCourtOrderId, freezeCourtOrderHash)
    {
      Status = (CourtOrderStatus)courtOrderStatus;
      EnforceAtHeight = enforceAtHeight;
    }

    public static CourtOrderType ToCourtOrderType(string documentType)
    {
      return documentType switch
      {
        _ when documentType == DocumentType.FreezeOrder => CourtOrderType.Freeze,
        _ when documentType == DocumentType.UnfreezeOrder => CourtOrderType.Unfreeze,
        _ => throw new BadRequestException($"Unknown DocumentType '{documentType}'")
      };
    }

    public static DocumentType ToDocumentType(int courtOrderTypeId)
    {
      return ToDocumentType((CourtOrderType)courtOrderTypeId);
    }

    public static DocumentType ToDocumentType(CourtOrderType type)
    {

      return type switch
      {
        CourtOrderType.Freeze => DocumentType.FreezeOrder,
        CourtOrderType.Unfreeze => DocumentType.UnfreezeOrder,
        _ => throw new BadRequestException($"Unknown CourtOrderType '{type}'")
      };
    }

    public string CourtOrderId { get; private set; }
    public DocumentType DocumentType { get; private set; }

    public string CourtOrderHash { get; private set; }

    public int? EnforceAtHeight { get; private set; }

    public void SetCourtOrderHash(string hash)
    {
      CourtOrderHash = hash;
    }

    public CourtOrderStatus Status { get; private set; }

    public string FreezeCourtOrderId { get; private set; }
    public string FreezeCourtOrderHash { get; private set; }

    public CourtOrderType Type { get; private set; }
    
    /// <summary>
    /// Get imported court order active status
    /// </summary>
    public CourtOrderStatus GetActiveStatus()
    {
      if (Status == CourtOrderStatus.Imported)
      {
        return Type switch
        {
          CourtOrderType.Freeze => CourtOrderStatus.FreezePolicy,
          CourtOrderType.Unfreeze => CourtOrderStatus.UnfreezeNoConsensusYet,
          _ => throw new Exception($"Unable to activate court order '{CourtOrderHash}'. Unknown courtOrderType '{Type}'"),
        };
      }
      return Status;
    }

    public bool IsStatusChangeValid(CourtOrderStatus newStatus)
    {
      switch (newStatus)
      {
        case CourtOrderStatus.FreezePolicy:
          if (Status != CourtOrderStatus.Imported)
          {
            return false;
          }
          break;
        case CourtOrderStatus.FreezeConsensus:
          if (Status != CourtOrderStatus.FreezePolicy)
          {
            return false;
          }
          break;
        case CourtOrderStatus.UnfreezeNoConsensusYet:
          if (Status != CourtOrderStatus.Imported)
          {
            return false;
          }
          break;
        case CourtOrderStatus.UnfreezeConsensus:
          if (Status != CourtOrderStatus.UnfreezeNoConsensusYet)
          {
            return false;
          }
          break;
        case CourtOrderStatus.Imported:
          return false;
        default:
          throw new InvalidOperationException($"Unknown court order status '{newStatus}'");
      }

      return true;
    }

    public DateTime? ValidTo { get; private set; }
    public DateTime? ValidFrom { get; private set; } // for unfreezeorder

    private readonly List<Fund> funds;
    public IReadOnlyCollection<Fund> Funds => funds;

    private bool? isValid = null;
    public bool IsValid
    {
      get
      {
        if (!isValid.HasValue)
        {
          Validate();
        }
        return isValid.Value;
      }
    }

    private IEnumerable<string> validationMessages = Enumerable.Empty<string>();
    public IEnumerable<string> ValidationMessages
    {
      get
      {
        if (!isValid.HasValue)
        {
          Validate();
        }
        return validationMessages;
      }
    }

    private void Validate()
    {
      var validator = new CourtOrderValidator(this);
      validationMessages = validator.Validate();
      isValid = !validationMessages.Any();
    }

    public void AddFund(TxOut txOut)
    {
      funds.Add(new Fund(txOut));
    }
    public void AddFunds(IEnumerable<TxOut> txOuts)
    {
      foreach (var txOut in txOuts)
      {
        funds.Add(new Fund(txOut));
      }
    }

    public void AddFunds(IEnumerable<Fund> newFunds)
    {
      funds.AddRange(newFunds);
    }
  }
}
