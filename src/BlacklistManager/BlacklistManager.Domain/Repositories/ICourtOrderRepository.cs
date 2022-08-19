// Copyright (c) 2020 Bitcoin Association

using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using BlacklistManager.Domain.Models;
using Common.Bitcoin;
using NBitcoin;

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
    public Task<long?> InsertCourtOrderAsync(CourtOrder courtOrder, string signedCourtOrder, int? legalEntityEndpointId, string signedByKey);

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

    public Task InsertFundStateNodeAsync(IEnumerable<FundStatePropagated> fundStateNodeList);

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
    /// Get court orders that need to be submitted to legal entities for acceptances
    /// </summary>
    /// <returns>list of court orders for which acceptances must be sent out</returns>
    public Task<IEnumerable<CourtOrderWithAcceptance>> GetCourtOrdersToSendAcceptancesAsync(int maxRetryCount);

    /// <summary>
    /// Updates status of court order acceptances 
    /// </summary>
    /// <param name="internalCourtOrderId"></param>
    /// <param name="legalEntityEndpointId"></param>
    /// <param name="signedCOAcceptance"></param>
    /// <param name="coAcceptanceSubmitedAt"></param>
    /// <param name="error"></param>
    /// <returns></returns>
    public Task<int> SetCourtOrderAcceptanceStatusAsync(int courtOrderAcceptanceId, string signedCOAcceptance, DateTime? coAcceptanceSubmitedAt, string error, int? retryCount);

    /// <summary>
    /// Get pending consensus activations
    /// </summary>
    public Task<IEnumerable<PendingConsensusActivation>> GetPendingConsensusActivationsAsync(int maxRetryCount, int rangeToCheck);

    public Task InsertConsensusActivationAsync(ConsensusActivation consensusActivation, long internalCourtOrderId, int legalEntityEndpointId, bool isCaValid, Network network, int? retryCount);
    public Task UpdateLegalEntityEndpointErrorAsync(int legalEntityEndpointId, string error);

    public Task<IEnumerable<Fund>> GetFundsAsync();

    public Task<IEnumerable<CourtOrder>> GetCourtOrdersWaitingForConfiscationAsync(int courtOrderStatus, long minEAT, long maxEAT);

    public Task<IEnumerable<CourtOrder>> GetCourtOrderForConfiscationTxWhiteListAsync();

    public Task<IEnumerable<TransactionToSend>> GetConfiscationTransactionsForWhiteListAsync(string courtOrderHash, int nodeId);

    public Task<IEnumerable<TransactionToSend>> GetConfiscationTransactionsAsync(int minEAT, int heightForSubmit, bool forceResend);

    public Task SetTransactionErrorsAsync(SendRawTransactionsResult[] transactionsResults);

    public Task<bool> InsertConfiscationTransactionsAsync(long internalCourtOrderId, IReadOnlyCollection<(string TxId, int? EnforceAtHeight, byte[] Body)> confiscationTransactions);

    public Task<IEnumerable<TransactionStatus>> GetConfiscationTransactionsStatusAsync(string courtOrderHash);

    public Task<IEnumerable<CourtOrderAcceptance>> GetCourtOrderAcceptancesAsync(long internalCourtOrderId, IDbTransaction transaction = null);

    public Task InsertValidationErrorsAsync(int legalEntityEndpointId, string courtOrderhash, string errorData, string lastError);

    public Task<IEnumerable<ValidationError>> GetValidationErrorsAsync(int maxRetryCount);

    public Task MarkValidationErrorSuccessfulAsync(int legalEntityEndpointId, string courtOrderhash);

    public Task<IEnumerable<CourtOrderQuery>> QueryCourtOrdersAsync(string courtOrderHash, bool includeFunds);

    public Task<FundQuery> QueryFundByTxOutAsync(string txId, long vout);

    public Task InsertWhitelistedNodeInfoAsync(string courtOrderHash, int nodeId);

    public Task CancelConfiscationOrderAsync(string courtOrderHash);

    public Task<(string ActivationHash, int EnforceAtHeight)[]> GetUnprocessedConsensusActivationsAsync();

    public Task<IEnumerable<ValidationError>> GetFailedCourtOrdersAsync();

    public Task MarkCourtOrderSuccessfullyProccesedAsync(int legalEntityEndpointId, string courtOrderhash);

    public Task<int> GetNumberOfSignedDocumentsAsync(string signedByKey);
  }
}
