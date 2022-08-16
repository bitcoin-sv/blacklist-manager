// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Common;
using NBitcoin;

namespace BlacklistManager.Domain.Actions
{
  public interface IDomainAction
  {
    public Task<ProcessCourtOrderResult> ProcessCourtOrderAsync(string signedCourtOrder, CourtOrder courtOrder, int? legalEntityEndpointId, bool onSuccessStartBackgroundJobs = true);

    /// <summary>
    /// Validates the signature and processes court order. 
    /// </summary>
    /// <param name="signedCourtOrder">Signed json envelope, containing court order in payload field</param>
    /// <param name="courtOrder">court order extracted from signedCourtOrder.payload</param>
    /// <returns></returns>
    public Task<ProcessCourtOrderResult> ProcessSignedCourtOrderAsync(JsonEnvelope signedCourtOrder, CourtOrder courtOrder, int? legalEntityEndpointId = null, bool onSuccessStartBackgroundJobs = true);

    /// <summary>
    /// Set court Order status and propagate fund state changes to nodes
    /// </summary>
    public Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus status, int? enforceAtHeight);

    Task<Node> CreateNodeAsync(Node node);
    Task<bool> UpdateNodeAsync(Node node);
    IEnumerable<Node> GetNodes();
    Node GetNode(string id);
    public int DeleteNode(string id);

    Task<LegalEntityEndpoint> CreateLegalEntityEndpointAsync(string baseUrl, string apiKey);
    Task<IEnumerable<LegalEntityEndpoint>> GetLegalEntityEndpointAsync();
    Task<LegalEntityEndpoint> GetLegalEntityEndpointAsync(int id);
    Task<bool> UpdateLegalEntityEndpointAsync(int id, string baseUrl, string apiKey);
    Task<bool> DisableLegalEntityEndpointAsync(int id);
    Task<bool> EnableLegalEntityEndpointAsync(int id);
    Task<bool> ResetLegalEntityEndpointAsync(int id);
    public Task CreateInitialSignerKeyAsync(Network network);

  }
}
