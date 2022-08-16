// Copyright (c) 2020 Bitcoin Association

using System;

namespace BlacklistManager.Domain.Models
{
  public class TxOut : IEquatable<TxOut>
  {
    public TxOut(string txId, long vout)
    {
      TxId = txId.ToLower();
      Vout = vout;
    }

    /// <summary>
    /// Id (hex string) of transaction that created output
    /// </summary>
    public string TxId { get; private set; }

    /// <summary>
    /// Output index
    /// </summary>
    public long Vout { get; private set; }

    public override string ToString()
    {
      return $"TxId={TxId},vOut={Vout}";
    }

    #region equality operators

    public override bool Equals(object obj)
    {
      if (obj == null)
      {
        return false;
      }

      return this.Equals(obj as TxOut);
    }

    public bool Equals(TxOut other)
    {
      if (other is null)
      {
        return false;
      }

      if (object.ReferenceEquals(this, other))
      {
        return true;
      }

      if (this.GetType() != other.GetType())
      {
        return false;
      }

      return TxId == other.TxId && Vout == other.Vout;
    }

    public override int GetHashCode()
    {
      return TxId.GetHashCode() ^ Vout.GetHashCode();
    }

    public static bool operator ==(TxOut lhs, TxOut rhs)
    {
      if (lhs is null)
      {
        if (rhs is null)
        {
          // null == null = true.
          return true;
        }

        // Only the left side is null.
        return false;
      }
      // Equals handles case of null on right side.
      return lhs.Equals(rhs);
    }

    public static bool operator !=(TxOut lhs, TxOut rhs)
    {
      return !(lhs == rhs);
    }
    #endregion
  }
}
