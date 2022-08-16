// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.ExternalServices;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading;

namespace BlacklistManager.Domain.Models
{
  public class FundPropagatorFactory : IFundPropagatorFactory
  {
    private readonly IBitcoindFactory bitcoindFactory;
    private readonly ILoggerFactory logger;

    public FundPropagatorFactory(
      IBitcoindFactory bitcoindFactory,
      ILoggerFactory logger)
    {
      this.bitcoindFactory = bitcoindFactory;
      this.logger = logger;
    }

    public FundPropagator Create(IEnumerable<Node> nodes, CancellationToken cancellationToken)
    {
      return new FundPropagator(bitcoindFactory, nodes, cancellationToken, logger);
    }
  }
}
