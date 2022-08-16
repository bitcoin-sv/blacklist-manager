// Copyright (c) 2020 Bitcoin Association

using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using static Common.BitcoinRpc.Requests.RpcFrozenFunds.RpcFund;

namespace BlacklistManager.Domain.Models
{
  public class Fund
  {
    public Fund(TxOut txOut)
    {
      TxOut = txOut;
      Status = FundStatus.Imported;
      EnforceAtHeight = new EnforceAtHeightList();
      Reason = string.Empty;
    }

    public Fund(TxOut txOut, string reason) : this(txOut)
    {
      Reason = reason;
    }

    public Fund(string txId, Int64 vout, FundStatus fundStatus) : this(txId, vout, (int)fundStatus)
    {
    }

    public Fund(string txid, Int64 vout, Int32 status)
    {
      TxOut = new TxOut(txid, vout);
      Status = (FundStatus)status;
      EnforceAtHeight = new EnforceAtHeightList();
    }

    public Fund(FundStateToPropagate stateToPropagate)
    {
      TxOut = stateToPropagate.TxOut;
      Status = stateToPropagate.Status;
      EnforceAtHeight = stateToPropagate.EnforceAtHeight;
    }

    public TxOut TxOut { get; private set; }

    public EnforceAtHeightList EnforceAtHeight { get; private set; }
    
    public bool PolicyExpiresWithConsensus => !EnforceAtHeight.ContainsIsPolicyFrozen;

    public string Reason { get; private set; }

    /// <summary>
    /// Effective status as calculated by taking into account all court orders
    /// </summary>
    public FundStatus Status { get; private set; }

    private bool? isValid = null;
    public bool IsValid()
    {
      if (!isValid.HasValue)
      {
        Validate();
      }
      return isValid.Value;
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
      var validator = new FundValidator(this);
      validationMessages = validator.Validate();
      isValid = !validationMessages.Any();
    }

    public override string ToString()
    {
      return $"TxOut=({TxOut}), Status={Status}, EnforceAtHeight=({EnforceAtHeight})";
    }
  }

  public class FundEqualityComparerByTxOut : IEqualityComparer<Fund>
  {
    public bool Equals([AllowNull] Fund x, [AllowNull] Fund y)
    {
      if ((x == null && y != null) || (x != null && y == null))
      {
        return false;
      }
      if (x == null && y == null)
      {
        return true;
      }
      if (object.ReferenceEquals(x,y))
      {
        return true;
      }
      return x.TxOut.Equals(y.TxOut);
    }

    public int GetHashCode([DisallowNull] Fund obj)
    {
      return obj.TxOut.GetHashCode();
    }
  }

  public class EnforceAtHeight
  {
    public EnforceAtHeight(string courtOrderHash, Int32 startEnforceAtHeight, Int32 stopEnforceAtHeight, bool hasUnfreezeOrder)
    {
      CourtOrderHash = courtOrderHash;
      StartEnforceAtHeight = startEnforceAtHeight;
      StopEnforceAtHeight = stopEnforceAtHeight;
      HasUnfreezeOrder = hasUnfreezeOrder;

      AssertStatus();
    }

    private void AssertStatus()
    {
      int c = 0;
      if (IsPolicyFrozen)
      {
        c++;
      }
      if (IsConsensusFrozen)
      {
        c++;
      }
      if (IsSpendable)
      {
        c++;
      }

      if (c != 1)
      {
        throw new BadRequestException($"Exactly one should be true: {IsPolicyFrozen} {IsConsensusFrozen} {IsSpendable}");
      }

      if (StopEnforceAtHeight != -1 && HasUnfreezeOrder == false)
      {
        throw new BadRequestException($"If stopEnforceAtHeight is set then hasUnfreezeOrder must be true");
      }
    }

    public string CourtOrderHash { get; private set; }
    public int StartEnforceAtHeight { get; private set; }
    public int StopEnforceAtHeight { get; private set; }
    public bool HasUnfreezeOrder { get; private set; }

    public bool IsPolicyFrozen => StartEnforceAtHeight == -1 && StopEnforceAtHeight == -1 && HasUnfreezeOrder == false;
    public bool IsConsensusFrozen => StartEnforceAtHeight != -1;
    public bool IsSpendable => 
      (StartEnforceAtHeight == -1 && StopEnforceAtHeight == -1 && HasUnfreezeOrder == true)
      || (StartEnforceAtHeight == -1 && StopEnforceAtHeight != -1);

    public override string ToString()
    {
      return $"{CourtOrderHash}, {StartEnforceAtHeight}, {StopEnforceAtHeight}, {HasUnfreezeOrder}";
    }
  }

  public class EnforceAtHeightList
  {
    private readonly List<EnforceAtHeight> list = new List<EnforceAtHeight>();

    public IReadOnlyCollection<EnforceAtHeight> List => list;

    public void Add(EnforceAtHeight enforceAtHeight)
    {
      list.Add(enforceAtHeight);
    }

    public bool Contains(EnforceAtHeight enforceAtHeight)
    {
      if (enforceAtHeight == null)
      {
        return true;
      }
      return list.Any(e => e.CourtOrderHash == enforceAtHeight.CourtOrderHash);
    }

    public bool ContainsIsPolicyFrozen => list.Any(l => l.IsPolicyFrozen);
    public bool ContainsIsConsensusFrozen => list.Any(l => l.IsConsensusFrozen);
    public bool ContainsIsSpendable => list.Any(l => l.IsSpendable);

    public override string ToString()
    {
      return string.Join(";", list.Select(s => $"{s.CourtOrderHash}, {s.StartEnforceAtHeight}, {s.StopEnforceAtHeight}, {s.HasUnfreezeOrder}"));
    }

    public string ToStringShort()
    {
      return string.Join(";", 
        list.Select(s => $"{s.StartEnforceAtHeight},{s.StopEnforceAtHeight},{(s.StartEnforceAtHeight + s.StopEnforceAtHeight == -2 ? s.HasUnfreezeOrder.ToString() : string.Empty)}"));
    }

    public bool IsEmpty => !list.Any();

    /// <summary>
    /// Returns consolidated enforceAtHeight collection for bitcoind RPC calls
    /// </summary>
    public IEnumerable<RpcEnforceAtHeight> GetConsolidatedList()
    {
      if (!list.Any())
      {
        return null;
      }

      var r = new List<RpcEnforceAtHeight>();

      foreach (var eah in list)
      {
        // possible optimization: code is not handling 'gaps & island'. See gaps & island optimization techniques if this is identified as performance gain spot

        if (eah.StartEnforceAtHeight == -1 && eah.StopEnforceAtHeight == -1)
        {
          continue;
        }
        r.Add(new RpcEnforceAtHeight(eah.StartEnforceAtHeight, eah.StopEnforceAtHeight));
      }
      return r;
    }

    /// <summary>
    /// Returns true if interval lists match.    
    /// </summary>
    /// <remarks>
    /// interval list A has same intervals as interval list B
    /// interval A matches interval B if start and stop are the same.
    /// </remarks>
    public static bool AreSameIntervals(EnforceAtHeightList eahList1, EnforceAtHeightList eahList2)
    {
      if (object.ReferenceEquals(eahList1, eahList2))
      {
        return true;
      }

      if (!eahList1.List.Any() && !eahList2.List.Any())
      {
        return true;
      }

      if ((eahList1.List.Any() && !eahList2.List.Any()) || (!eahList1.List.Any() && eahList2.List.Any()))
      {
        return false;
      }

      bool areSame = AreSameIntervalsInter(eahList1, eahList2);
      if (areSame == false)
      {
        return false;
      }
      return AreSameIntervalsInter(eahList2, eahList1);
    }

    private static bool AreSameIntervalsInter(EnforceAtHeightList eahList1, EnforceAtHeightList eahList2)
    {
      foreach (var eah1 in eahList1.List)
      {
        bool contains = false;
        foreach (var eah2 in eahList2.List)
        {
          if (eah1.StartEnforceAtHeight == eah2.StartEnforceAtHeight && eah1.StopEnforceAtHeight == eah2.StopEnforceAtHeight)
          {
            contains = true;
            break;
          }
        }
        if (!contains)
        {
          return false;
        }
      }

      return true;
    }
  }

  public class FundStateToPropagate : DomainModelBase
  {
    public FundStateToPropagate(Int64 fundstateid, Int64 fundId, string txId, Int64 vout, Int32 fundstatus, Int32 fundstatusprevious, Int32 nodeid) : base(fundstateid)
    {
      FundId = fundId;
      Status = (FundStatus)fundstatus;
      StatusPrevious = (FundStatus)fundstatusprevious;
      NodeId = nodeid;
      TxOut = new TxOut(txId, vout);
      EnforceAtHeight = new EnforceAtHeightList();
      EnforceAtHeightPrevious = new EnforceAtHeightList();
    }

    public long FundId { get; private set; }
    public FundStatus Status { get; private set; }
    public FundStatus StatusPrevious { get; private set; }
    public int NodeId { get; private set; }
    public TxOut TxOut { get; private set; }
    public string Key => $"{Id}|{NodeId}";

    public EnforceAtHeightList EnforceAtHeight { get; private set; }
    public EnforceAtHeightList EnforceAtHeightPrevious { get; private set; }

    public override string ToString()
    {
      return $"NodeId={NodeId},FundId={FundId},FundStateId={Id},{TxOut},EnforceAtHeight={EnforceAtHeight}";
    }
  }

  public class FundStatePropagated
  {
    public FundStatePropagated(FundStateToPropagate stateToPropagate, Node node, DateTime propagatedAt)
    {
      StateToPropagate = stateToPropagate;
      Node = node;
      PropagatedAt = propagatedAt;
    }

    public FundStateToPropagate StateToPropagate { get; private set; }
    public Node Node { get; private set; }

    public DateTime PropagatedAt { get; private set; }
  }
}
