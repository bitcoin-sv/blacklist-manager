// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public class ConfiscationTx
  {
    public string Hex { get; set; }

    public string GetHexStartingPart()
    {
      return Hex.Substring(0, Hex.Length > 20 ? Hex.Length - 1 : 19);
    }
  }
}
