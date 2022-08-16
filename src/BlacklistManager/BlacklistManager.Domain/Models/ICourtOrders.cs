// Copyright (c) 2020 Bitcoin Association

using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Models
{
  public interface ICourtOrders
  {
    Task<CourtOrderActivationResult> ActivateCourtOrdersAsync(CancellationToken cancellationToken);
    Task ActivateCourtOrderAsync(CourtOrder courtOrder);
    Task<bool> ActivateCourtOrderAsync(string courtOrderHash);

    /// <summary>
    /// Imports court order into database. 
    /// If requested adds reference to legal entity endpoint.
    /// If requested start background jobs.
    /// </summary>
    /// <returns>false if order already imported</returns>
    Task<bool> ImportCourtOrderAsync(CourtOrder courtOrder, string signedCourtOrder, int? legalEntityEndpointId, bool onSuccessStartBackgroundJobs = true);

    Task<ProcessConsensusActivationResult> ProcessConsensusActivationsAsync(CancellationToken cancellationToken);
    Task<FundPropagationResult> PropagateFundsStateAsync(CancellationToken cancellationToken);    
    Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus status, int? enforceAtHeight);    
    Task<CheckReferencedCourtOrderResult> CheckReferencedCourtOrderAsync(CourtOrder courtOrder);
  }
}