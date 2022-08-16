// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading;

namespace BlacklistManager.Domain.Models
{
  public interface IFundPropagatorFactory
  {
    FundPropagator Create(IEnumerable<Node> nodes, CancellationToken cancellationToken);
  }
}