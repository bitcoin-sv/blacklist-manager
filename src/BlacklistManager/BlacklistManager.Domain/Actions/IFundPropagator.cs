// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public interface IFundPropagator
  {
    Task<FundPropagationResult> PropagateFundsStateAsync(CancellationToken cancellationToken);
  }
}
