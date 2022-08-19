// Copyright (c) 2020 Bitcoin Association

using Common;
using Common.SmartEnums;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BlacklistManager.Domain.Models
{
  public class CourtOrder
  {
    public string CourtOrderId { get; init; }

    private readonly DocumentType documentType;
    public DocumentType DocumentType
    {
      get
      {
        return documentType;
      }

      init
      {
        documentType = value;
        Type = (CourtOrderType)value.Name;
      }
    }

    public string Blockchain { get; set; }

    public string CourtOrderHash { get; init; }

    public int? EnforceAtHeight { get; init; }

    public CourtOrderStatus Status { get; init; } = CourtOrderStatus.Imported;

    public string FreezeCourtOrderId { get; init; }
    public string FreezeCourtOrderHash { get; init; }

    public CourtOrderType Type { get; init; }

    public ConfiscationDestination Destination { get; init; }

    public DateTime? ValidTo { get; init; }
    public DateTime? ValidFrom { get; init; } // for unfreezeorder

    public string SignedByKey { get; init; }

    public DateTime SignedDate { get; init; }

    private readonly List<Fund> funds = new();
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

    /// <summary>
    /// Get imported court order active status
    /// </summary>
    public CourtOrderStatus GetActiveStatus()
    {
      if (Status == CourtOrderStatus.Imported)
      {
        if (Type == CourtOrderType.Freeze)
        {
          return CourtOrderStatus.FreezePolicy;
        }
        else if (Type == CourtOrderType.Unfreeze)
        {
          return CourtOrderStatus.UnfreezeNoConsensusYet;
        }
        else if (Type == CourtOrderType.Confiscation)
        {
          return CourtOrderStatus.ConfiscationPolicy;
        }
        throw new InvalidOperationException($"Unable to activate court order '{CourtOrderHash}'. Unknown courtOrderType '{Type}'");
      }
      return Status;
    }

    public bool IsStatusChangeValid(CourtOrderStatus newStatus)
    {
      switch (newStatus)
      {
        case CourtOrderStatus.ConfiscationPolicy:
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
        case CourtOrderStatus.ConfiscationConsensus:
          if (Status != CourtOrderStatus.ConfiscationPolicy)
          {
            return false;
          }
          break;
        case CourtOrderStatus.ConfiscationConsensusWhitelisted:
          if (Status != CourtOrderStatus.ConfiscationConsensus)
          {
            return false;
          }
          break;
        case CourtOrderStatus.ConfiscationCancelled:
          if (Status != CourtOrderStatus.ConfiscationPolicy)
          {
            return false;
          }
          break;
        default:
          throw new InvalidOperationException($"Unknown court order status '{newStatus}'");
      }

      return true;
    }

    public bool DoNetworksMatch(string network, out string error)
    {
      error = null;
      if (String.IsNullOrEmpty(Blockchain))
      {
        error = "Blockchain parameter on Court order is not set.";
        return false;
      }
      var coNetwork = Blockchain.Split('-');
      if (coNetwork.Length != 2)
      {
        error = "Blockchain data is not in valid format.";
        return false;
      }
      if (coNetwork[1] != network)
      {
        error = "Blacklist managers network doesn't match the network specified on Court order.";
        return false;
      }
      return true;
    }


    private void Validate()
    {
      var validator = new CourtOrderValidator(this);
      validationMessages = validator.Validate();
      isValid = !validationMessages.Any();
    }

    public void AddFund(TxOut txOut, long value)
    {
      funds.Add(new Fund(txOut, value));
    }
    public void AddFunds(IEnumerable<(TxOut txOut, long value)> funds)
    {
      foreach (var fund in funds)
      {
        this.funds.Add(new Fund(fund.txOut, fund.value));
      }
    }

    public void AddFunds(IEnumerable<Fund> newFunds)
    {
      funds.AddRange(newFunds);
    }
  }
}
