// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public interface IConfiscationTxProcessing
  {
    Task<bool> SubmitWhitelistTxIdsAsync(Node[] nodes, CancellationToken cancellationToken);
    Task<bool> SendConfiscationTransactionsAsync(Node node, CancellationToken cancellationToken, long? forceResendLength = null);
    Task<bool> ConfiscationsInBlockCheckAsync(Node node, CancellationToken cancellationToken);
  }
}
