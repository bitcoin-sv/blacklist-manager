// Copyright (c) 2020 Bitcoin Association

using System.Collections.Generic;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public interface IQueryAction
  {
    Task<IEnumerable<CourtOrderQuery>> QueryCourtOrdersAsync(string courtOrderHash, bool includeFunds);
    Task<CourtOrderQuery.Fund> QueryFundByTxOutAsync(string txId, long vout);
  }
}