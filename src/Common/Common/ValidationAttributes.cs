// Copyright (c) 2020 Bitcoin Association

using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using static Common.Consts;

namespace Common
{
  [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
  public class StringRangeValueAttribute : ValidationAttribute
  {
    string[] _allowedValues;
    public StringRangeValueAttribute(string[] values)
    {
      _allowedValues = values;
    }

    public override bool IsValid(object value)
    {
      return value != null && value.GetType() == typeof(string) && _allowedValues.Any(x => x == (string)value);
    }
  }

  public class BitcoinNetworkValueAttribute : StringRangeValueAttribute
  {
    public BitcoinNetworkValueAttribute() : base(NBitcoin.Network.GetNetworks().Select(x => x.Name).ToArray())
    {
      ErrorMessage = "Invalid value for Bitcoin network";
    }
  }

  public class BlockchainValueAttribute : StringRangeValueAttribute
  {
    public BlockchainValueAttribute() : base(BlockChainType.ValidTypes().ToArray())
    {
      ErrorMessage = "Invalid value for Blockchain";
    }
  }
}
