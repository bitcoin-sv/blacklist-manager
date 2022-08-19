// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using Common;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public interface ICourtOrders
  {
    Task<CourtOrderActivationResult> ActivateCourtOrdersAsync(CancellationToken cancellationToken);

    Task<ProcessConsensusActivationResult> GetConsensusActivationsAsync(CancellationToken cancellationToken);

    Task<ProcessConsensusActivationResult> ActivateConsensusActivationsAsync(CancellationToken cancellationToken);

    Task ProcessGetCourtOrdersAsync(IEnumerable<LegalEntityEndpoint> ntEndpoints, CancellationToken cancellationToken);

    Task<(bool Successfull, int NoOfProcessed)> ProcessCourtOrderAcceptancesAsync(Node node, CancellationToken cancellationToken);

    Task<ProcessCourtOrderResult> ProcessSignedCourtOrderAsync<T>(JsonEnvelope signedCourtOrder, T order, int? legalEntityEndpointId = null);

    Task<ProcessCourtOrderResult> ProcessCourtOrderAsync(JsonEnvelope signedCourtOrder, CourtOrder courtOrder, int? legalEntityEndpointId);

    Task CheckAndResendProcessingErrorsToLEsAsync(CancellationToken cancellationToken);

    Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus status, int? enforceAtHeight);
  
    Task<ProcessCourtOrderResult> CancelConfiscationOrderAsync(JsonEnvelope jsonEnvelope, string courtOrderHash);

    Task<bool> ProcessFailedCourtOrdersAsync(CancellationToken token);
  }
}