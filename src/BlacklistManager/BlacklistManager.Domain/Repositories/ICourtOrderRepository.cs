// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BlacklistManager.Domain.Models;

namespace BlacklistManager.Domain.Repositories
{
  /// <summary>
  /// Repository of court orders. No internal keys are exposed.
  /// CourtOrderHash is used for identifying court orders
  /// </summary>
  public interface ICourtOrderRepository
  {
    /// <summary>
    /// Insert court order and it funds.
    /// false is returned if court order with the same hash exists
    /// if funds are already inserted by other court order, they are not duplicated
    /// </summary>
    /// <param name="order"></param>
    public Task<bool> InsertCourtOrderAsync(CourtOrder courtOrder, string signedCourtOrder, int? legalEntityEndpointId);

    /// <summary>
    /// Set the status for given court order
    /// </summary>
    /// <param name="courtOrderHash"></param>
    /// <param name="courtOrderStatus"></param>
    public Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus courtOrderStatus, int? enforceAtHeight);

    /// <summary>
    /// Get list of fund states for funds that have at least one state that needs propagation to bitcoin nodes
    /// </summary>
    /// <returns>fund state list for each node chronologically ordered by fundStateId (state change date) </returns>
    public Task<IEnumerable<FundStateToPropagate>> GetFundStateToPropagateAsync();

    public void InsertFundStateNode(IEnumerable<FundStatePropagated> fundStateNodeList);

    /// <summary>
    /// Get court orders 
    /// </summary>
    /// <param name="courtOrderhash">optional. When null, all court orders are returned.</param>
    /// <param name="includeFunds">When true funds are returned too</param>
    /// <returns></returns>
    public Task<IEnumerable<CourtOrder>> GetCourtOrdersAsync(string courtOrderhash, bool includeFunds);    
    
    /// <summary>
    /// Get imported court orders that are not yet processed
    /// </summary>
    /// <returns>returns list of court order hashes</returns>
    public Task<IEnumerable<string>> GetCourtOrdersToActivateAsync();        
    
    /// <summary>
    /// Get courtorders that need to be submited to legal entities for acceptances
    /// </summary>
    /// <returns>list of court orders for which acceptances must be sent out</returns>
    public Task<IEnumerable<CourtOrderWithAcceptance>> GetCourtOrdersToSendAcceptancesAsync();

    /// <summary>
    /// Updates status of court order acceptances 
    /// </summary>
    /// <param name="internalCourtOrderId"></param>
    /// <param name="legalEntityEndpointId"></param>
    /// <param name="signedCOAcceptance"></param>
    /// <param name="coAcceptanceSubmitedAt"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public Task<int> SetCourtOrderAcceptanceStatusAsync(int courtOrderAcceptanceId, string signedCOAcceptance, DateTime? coAcceptanceSubmitedAt, string error);

    /// <summary>
    /// Get pending consensus activations
    /// </summary>
    public Task<IEnumerable<PendingConsensusActivation>> GetPendingConsensusActivationsAsync();

    public Task InsertConsensusActivationAsync(ConsensusActivation consensusActivation, long internalCourtOrderId, int legalEntityEndpointId, bool isCaValid);
    public Task UpdateLegalEntityEndpointErrorAsync(int legalEntityEndpointId, string error);

    public IEnumerable<Fund> GetFunds();
  }
}
