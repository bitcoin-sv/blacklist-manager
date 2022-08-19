// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using BlacklistManager.Domain.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using Dapper;
using System.Threading.Tasks;
using System.Data;
using Common;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Microsoft.Extensions.Configuration;
using Common.Bitcoin;
using Common.SmartEnums;

namespace BlacklistManager.Infrastructure.Repositories
{
  public class CourtOrderRepositoryPostgres : ICourtOrderRepository
  {
    private readonly string _connectionString;
    private readonly ILogger<CourtOrderRepositoryPostgres> _logger;

    public CourtOrderRepositoryPostgres(
      IConfiguration configuration,
      ILogger<CourtOrderRepositoryPostgres> logger)
    {
      _connectionString = configuration["BlacklistManagerConnectionStrings:DBConnectionString"];
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private class QueryFreezeCourtOrderbyHashResult
    {
      public long InternalCourtOrderId { get; set; }
    }

    private async Task<QueryFreezeCourtOrderbyHashResult> QueryFreezeCourtOrderbyHashAsync(IDbTransaction transaction, string courtOrderHash)
    {
      string cmdText =
        "SELECT internalCourtOrderId, courtOrderId FROM CourtOrder WHERE courtOrderHash=@courtOrderHash AND courtOrderType=@courtOrderType";
      var result = (await transaction.Connection.QueryAsync<QueryFreezeCourtOrderbyHashResult>(
        cmdText,
        new
        {
          courtOrderHash,
          courtOrderType = CourtOrderType.Freeze.Id
        }, transaction)).ToArray();

      if (result.Length == 0)
      {
        return null;
      }

      if (result.Length == 1)
      {
        return result[0];
      }

      // Should not happen - there is unique constraint in database:
      throw new BadRequestException($"Internal error: multiple freeze court order with hash '{courtOrderHash}' found.");
    }

    public async Task<long?> InsertCourtOrderAsync(CourtOrder courtOrder, string signedCourtOrder, int? legalEntityEndpointId, string signedByKey)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      using var transaction = await connection.BeginTransactionAsync();
      QueryFreezeCourtOrderbyHashResult referenceFreezeOrder = await QueryFreezeCourtOrderbyHashAsync(transaction, courtOrder.FreezeCourtOrderHash);

      string cmdText = @"
INSERT INTO CourtOrder 
(courtOrderId, courtOrderHash, courtOrderType, courtOrderStatus, freezeCourtOrderId, freezeCourtOrderHash, freezeInternalCourtOrderId, validFrom, validTo, signedCourtOrderJson, destinationAddress, destinationAmount, signedByKey, signedDate)
VALUES (@courtOrderId, @courtOrderHash, @courtOrderType, @courtOrderStatus, @freezeCourtOrderId, @freezeCourtOrderHash, @freezeInternalCourtOrderId, @validFrom, @validTo, @signedCourtOrder, @destinationAddress, @destinationAmount, @signedByKey, @signedDate)
ON CONFLICT (courtOrderHash) DO NOTHING
RETURNING internalCourtOrderId";

      long? internalCourtOrderId = await connection.ExecuteScalarAsync<long?>(cmdText,
        new
        {
          courtOrderId = courtOrder.CourtOrderId,
          courtOrderHash = courtOrder.CourtOrderHash,
          courtOrderType = (int)courtOrder.Type,
          courtOrderStatus = (int)courtOrder.Status,
          freezeCourtOrderId = courtOrder.FreezeCourtOrderId,
          freezeCourtOrderHash = courtOrder.FreezeCourtOrderHash,
          freezeInternalCourtOrderId = referenceFreezeOrder?.InternalCourtOrderId,
          validFrom = courtOrder.ValidFrom,
          validTo = courtOrder.ValidTo,
          signedCourtOrder,
          destinationAddress = courtOrder.Destination?.Address,
          destinationAmount = courtOrder.Destination?.Amount,
          signedByKey,
          signedDate = courtOrder.SignedDate
        },
        transaction);

      if (internalCourtOrderId.HasValue)
      {
        if (courtOrder.Type == CourtOrderType.Unfreeze && !courtOrder.Funds.Any())
        {
          await InsertFundsFromFreezeOrderAsync(transaction, internalCourtOrderId.Value, courtOrder.FreezeCourtOrderHash);
        }
        else
        {
          await InsertFundsAsync(transaction, internalCourtOrderId.Value, courtOrder.Funds, courtOrder.Type);
        }
        await InsertCourtOrderAcceptanceStatusAsync(transaction, internalCourtOrderId.Value, legalEntityEndpointId);
      }

      await transaction.CommitAsync();

      return internalCourtOrderId;
    }

    /// <summary>
    /// Set court order and funds status.
    /// Database trigger writes status changes in CourtOrderState and FundState tables.
    /// </summary>
    public async Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus courtOrderStatus, int? enforceAtHeight)
    {
      _logger.LogTrace("SetCourtOrderStatusAsync start");

      if (courtOrderStatus == CourtOrderStatus.FreezeConsensus ||
          courtOrderStatus == CourtOrderStatus.UnfreezeConsensus ||
          courtOrderStatus == CourtOrderStatus.ConfiscationConsensus ||
          courtOrderStatus == CourtOrderStatus.ConfiscationConsensusWhitelisted)
      {
        if (!enforceAtHeight.HasValue)
        {
          throw new ArgumentNullException(nameof(enforceAtHeight), "Parameter should not be null for consensus level status");
        }
      }
      else
      {
        if (enforceAtHeight.HasValue)
        {
          throw new ArgumentException("Parameter should be null for policy level status", nameof(enforceAtHeight));
        }
      }

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      using var transaction = connection.BeginTransaction();
      string cmdSetStatus = @"
UPDATE CourtOrder
SET courtOrderStatus = @courtOrderStatus, enforceAtHeight = @enforceAtHeight
WHERE courtOrderHash = @courtOrderHash
RETURNING internalCourtOrderId
";

      var internalCourtOrderIdsEnum = await connection.QueryAsync<long?>(cmdSetStatus,
        new
        {
          courtOrderStatus = (int)courtOrderStatus,
          courtOrderHash = courtOrderHash,
          enforceAtHeight
        },
        transaction);

      var internalCourtOrderIds = internalCourtOrderIdsEnum.ToArray();

      if (internalCourtOrderIds.Length != 1)
      {
        throw new BadRequestException(
          $"SetCourtOrderStatus: exactly 1 record should be affected but there were '{internalCourtOrderIds.Length}' for order with hash '{courtOrderHash}'");
      }

      var internalCourtOrderId = internalCourtOrderIds[0];
      if (!internalCourtOrderId.HasValue)
      {
        throw new BadRequestException(
          $"SetCourtOrderStatus error: Got NULL internalCourtOrderId for order with hash '{courtOrderHash}'  ");
      }

      if (courtOrderStatus == CourtOrderStatus.ConfiscationConsensus)
      {
        var cmdText = @"
UPDATE ConfiscationTransaction
SET enforceAtHeight = @enforceAtHeight
WHERE internalCourtOrderId = @internalCourtOrderId
      AND rewardTransaction = false;
";

        await connection.ExecuteAsync(cmdText,
          new
          {
            enforceAtHeight = enforceAtHeight,
            internalCourtOrderId
          },
          transaction);
      }

      await SetFundProcessedStatusAsync(transaction, courtOrderHash);
      await transaction.CommitAsync();
      _logger.LogTrace("SetCourtOrderStatusAsync end");
    }

    public async Task SetFundProcessedStatusAsync(IDbTransaction transaction, string courtOrderHash)
    {
      // order by in fundToUpdate is for tests where predictable order of update is required 
      // ProcessCourtOrder_TestRpcCallsForMultipleOrders, step #8 could split removeFromPolicy in two calls
      string cmdUpdateFundStatus = @"
CREATE TEMP TABLE fundToUpdate AS 
SELECT 
  fc.fundId, MAX(fs.fundStateId) maxFundStateId
FROM fundWithCourtOrder fc
INNER JOIN fundstate fs ON fs.fundid = fc.fundid 
WHERE 
  fc.courtOrderHash = @courtOrderHash
GROUP BY fc.fundId
ORDER BY fc.fundId;

DELETE FROM 
  fundEnforceAtHeight 
WHERE 
  fundId IN (SELECT fundId FROM fundToUpdate);

INSERT INTO fundEnforceAtHeight (fundId, internalCourtOrderId, startEnforceAtHeight, stopEnforceAtHeight, hasUnfreezeOrder, hasConfiscationOrder) 
SELECT DISTINCT 
  fundId, 
  internalCourtOrderId,
  COALESCE(startEnforceAtHeight,-1), 
  COALESCE(stopEnforceAtHeight,-1),
  CASE WHEN hasUnfreezeOrder > 0 THEN true ELSE false END,
  CASE WHEN hasConfiscationOrder > 0 THEN true ELSE false END
FROM
  fundWithCourtOrderPivot
WHERE 
  fundId IN (SELECT fundId FROM fundToUpdate);

INSERT INTO fundState (fundId, fundStatus, fundStateIdPrevious, fundStatusPrevious, changedAt) 
SELECT f.fundid, @processed, fu.maxFundStateId, f.fundstatus, now() at time zone 'utc'
FROM fund f
INNER JOIN fundToUpdate fu ON f.fundid = fu.fundid;

UPDATE fund
  SET fundStatus=@processed
FROM
  fundToUpdate
WHERE
  fundToUpdate.fundId=fund.fundId;

INSERT INTO fundStateEnforceAtHeight (fundStateId, internalCourtOrderId, startEnforceAtHeight, stopEnforceAtHeight, hasUnfreezeOrder)
SELECT 
  fs.fundStateId, 
  fe.internalCourtOrderId, 
  fe.startEnforceAtHeight, 
  fe.stopEnforceAtHeight, 
  fe.hasUnfreezeOrder
FROM fundEnforceAtHeight fe
INNER JOIN (SELECT MAX(fundstateid) fundStateId, fundid FROM fundstate GROUP BY fundid ) fs ON fe.fundid = fs.fundid
WHERE 
  fe.fundId IN (SELECT fundId FROM fundToUpdate);";

      int rowsAffected = await transaction.Connection.ExecuteAsync(cmdUpdateFundStatus, new { courtOrderHash, processed = FundStatus.Processed });

      _logger.LogDebug($"SetFundProcessedStatusAsync: {rowsAffected} updated");
    }

    public async Task<IEnumerable<Fund>> GetFundsAsync()
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();
      string cmdText = @"
SELECT 
  txid, vout, value, fundstatus as status,
  courtOrderHash, startEnforceAtHeight, stopEnforceAtHeight, hasUnfreezeOrder
FROM 
  fund f
  JOIN fundEnforceAtHeight fh ON fh.fundId=f.fundId
  JOIN courtOrder co ON co.internalCourtOrderId=fh.internalCourtOrderId
ORDER BY
  f.fundId
";
      var allCourtOrderFunds = new Dictionary<string, Fund>();

      _ = await connection.QueryAsync<Fund, EnforceAtHeight, Fund>(cmdText,
        (f, eah) =>
        {
          if (!allCourtOrderFunds.TryGetValue(f.TxOut.ToString(), out Fund fEntity))
          {
            fEntity = f;
            allCourtOrderFunds.Add(f.TxOut.ToString(), fEntity);
          }

          fEntity.EnforceAtHeight.AddOnlyUnique(eah);
          return fEntity;
        },
        transaction,
        splitOn: "courtOrderHash");

      return allCourtOrderFunds.Values;
    }

    public async Task<bool> InsertConfiscationTransactionsAsync(long internalCourtOrderId, IReadOnlyCollection<(string TxId, int? EnforceAtHeight, byte[] Body)> confiscationTransactions)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      var insertCount = await InsertTransactionsAsync(transaction, internalCourtOrderId, confiscationTransactions, false);

      await transaction.CommitAsync();

      return insertCount;
    }

    private async Task<bool> InsertTransactionsAsync(NpgsqlTransaction transaction, long internalCourtOrderId, IReadOnlyCollection<(string TxId, int? EnforceAtHeight, byte[] Body)> confiscationTransactions, bool rewardTransactions)
    {
      ulong insertCount;
      using (var txImporter = transaction.Connection.BeginBinaryImport("COPY ConfiscationTransaction (internalCourtOrderId, transactionId, transactionBody, enforceAtHeight, rewardTransaction) FROM STDIN (FORMAT BINARY)"))
      {
        foreach (var (TxId, EnforceAtHeight, Body) in confiscationTransactions)
        {
          txImporter.StartRow();

          txImporter.Write(internalCourtOrderId, NpgsqlTypes.NpgsqlDbType.Bigint);
          txImporter.Write(TxId, NpgsqlTypes.NpgsqlDbType.Varchar);
          txImporter.Write(Body, NpgsqlTypes.NpgsqlDbType.Bytea);
          txImporter.Write((object)EnforceAtHeight ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Integer);
          txImporter.Write(rewardTransactions, NpgsqlTypes.NpgsqlDbType.Boolean);
        }
        insertCount = await txImporter.CompleteAsync();
      }

      return insertCount > 0;
    }

    private async Task InsertFundsFromFreezeOrderAsync(IDbTransaction transaction, long internalCourtOrderId, string freezeCourtOrderHash)
    {
      string confiscatedFundsCmd = @"
SELECT COUNT(*)
FROM fundenforceatheight feah  
INNER JOIN courtorder c ON feah.internalcourtorderid = c.internalcourtorderid 
WHERE c.courtorderhash = @freezeCourtOrderHash
      AND feah.hasconfiscationorder 
";
      var confiscatedFundsCount = await transaction.Connection.ExecuteScalarAsync<int>(confiscatedFundsCmd, new { freezeCourtOrderHash });
      if (confiscatedFundsCount > 0)
      {
        throw new InvalidOrderException($"Unfreeze order rejected. {confiscatedFundsCount} funds that are about to be unfrozen are marked for confiscation.");
      }

      string insertFunds = @"
INSERT INTO courtOrderFund 
  (fundId, internalCourtorderId)
SELECT
  cof.fundId, @internalCourtOrderId
FROM
  courtOrder co
  JOIN courtOrderFund cof ON co.internalCourtOrderId=cof.internalCourtOrderId
WHERE
  co.courtOrderHash = @courtOrderHash
";
      await transaction.Connection.ExecuteAsync(insertFunds,
        new
        {
          courtOrderHash = freezeCourtOrderHash,
          internalCourtOrderId
        },
        transaction
      );
    }

    private async Task InsertFundsAsync(NpgsqlTransaction transaction, long internalCourtOrderId,
                                        IReadOnlyCollection<Fund> courtOrderFunds, CourtOrderType courtOrderType)
    {
      string cmdCreateFundsTemp = @"
CREATE TEMPORARY TABLE funds_temp (
fundId BIGINT DEFAULT nextval('fund_fundid_seq'),
txId VARCHAR(256), 
vOut BIGINT, 
value BIGINT,
fundStatus INT,
internalCourtOrderId BIGINT,
hasConfiscationOrder BOOL,
exists BOOL) ON COMMIT DROP;
";

      await transaction.Connection.ExecuteAsync(cmdCreateFundsTemp);

      using (var fundImporter = transaction.Connection.BeginBinaryImport(@"COPY funds_temp (txId, vOut, value, fundStatus, internalCourtOrderId, hasConfiscationOrder, exists) FROM STDIN (FORMAT BINARY)"))
      {
        foreach (var fund in courtOrderFunds)
        {
          fundImporter.StartRow();

          fundImporter.Write(fund.TxOut.TxId, NpgsqlTypes.NpgsqlDbType.Varchar);
          fundImporter.Write(fund.TxOut.Vout, NpgsqlTypes.NpgsqlDbType.Bigint);
          fundImporter.Write(fund.Value, NpgsqlTypes.NpgsqlDbType.Bigint);
          fundImporter.Write((int)FundStatus.Imported, NpgsqlTypes.NpgsqlDbType.Integer);
          fundImporter.Write(internalCourtOrderId, NpgsqlTypes.NpgsqlDbType.Bigint);
          fundImporter.Write(courtOrderType == CourtOrderType.Confiscation, NpgsqlTypes.NpgsqlDbType.Boolean);
          fundImporter.Write(false, NpgsqlTypes.NpgsqlDbType.Boolean);
        }
        await fundImporter.CompleteAsync();
      }

      if (courtOrderType == CourtOrderType.Unfreeze)
      {
        string cmdCheckForConfiscatedFunds = @"
SELECT COUNT(*)
FROM fund f
INNER JOIN funds_temp ft ON f.txId = ft.txId AND f.vout = ft.vout
INNER JOIN fundEnforceAtHeight feah ON f.fundId = feah.fundId
INNER JOIN courtOrder co ON feah.internalCourtOrderId = co.freezeInternalCourtOrderId
WHERE co.internalCourtOrderId = @internalCourtOrderId
      AND feah.hasConfiscationOrder;
";

        var confiscatedFundsCount = await transaction.Connection.ExecuteScalarAsync<int>(cmdCheckForConfiscatedFunds, new { internalCourtOrderId });
        if (confiscatedFundsCount > 0)
        {
          throw new InvalidOperationException($"Unfreeze order rejected. {confiscatedFundsCount} funds that are about to be unfrozen are marked for confiscation.");
        }
      }

      string cmdInsertFunds = @"
-- Update temporary table with existing fundId's for funds that are already in DB
UPDATE funds_temp SET fundId = f.fundId, exists = true
FROM (SELECT fundid, txid, vout FROM fund) f
WHERE 
  f.txid = funds_temp.txid AND f.vout = funds_temp.vout;

INSERT INTO fund (fundId, txid, vout, value, fundStatus) 
SELECT 
  fundId, 
  txId, 
  vOut, 
  value,
  fundStatus
FROM funds_temp
WHERE 
  exists = false;

INSERT INTO courtorderfund (fundid, internalcourtorderid) 
SELECT 
  fundId, 
  internalCourtOrderId
FROM funds_temp
ON CONFLICT(fundid, internalcourtorderid) DO NOTHING;

INSERT INTO fundState (fundId, fundStatus, changedAt) 
SELECT 
  ft.fundid, 
  ft.fundStatus, 
  now() at time zone 'utc'
FROM funds_temp ft
WHERE 
  ft.exists = false;";

      await transaction.Connection.ExecuteAsync(cmdInsertFunds);
    }

    public async Task<IEnumerable<FundStateToPropagate>> GetFundStateToPropagateAsync()
    {
      _logger.LogTrace("GetFundStateToPropagateAsync start");

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();
      string cmdText = @"
SELECT 
  fs.fundstateid, fs.fundid, f.txid, f.vout, fs.fundstatus, fs.fundstatusprevious, n.nodeid,
  co.courtOrderHash, startEnforceAtHeight, stopEnforceAtHeight, hasUnfreezeOrder
FROM 
  fundState fs CROSS JOIN node n
  JOIN fund f ON f.fundid = fs.fundid
  JOIN fundStateEnforceAtHeight fsh ON fsh.fundStateId=fs.fundStateId
  JOIN courtOrder co ON co.internalCourtOrderId=fsh.internalCourtOrderId
  LEFT JOIN fundStateNode fsn ON fsn.fundstateid=fs.fundstateid AND fsn.nodeid=n.nodeid
WHERE 
  fs.fundStatus IN (@processed)  
  AND n.nodeStatus = @connected
  AND fsn.fundstateid is null
ORDER BY
  n.nodeid, fs.fundstateid
";
      var allFundsToBePropagated = new Dictionary<string, FundStateToPropagate>();

      _ = await connection.QueryAsync<FundStateToPropagate, EnforceAtHeight, FundStateToPropagate>(cmdText, (fsp, eah) =>
      {
        if (!allFundsToBePropagated.TryGetValue(fsp.Key, out FundStateToPropagate fspEntity))
        {
          fspEntity = fsp;
          allFundsToBePropagated.Add(fsp.Key, fspEntity);
        }

        fspEntity.EnforceAtHeight.AddOnlyUnique(eah);
        return fspEntity;
      },
      param: new
      {
        processed = (int)FundStatus.Processed,
        connected = (int)NodeStatus.Connected
      },
      transaction,
      splitOn: "courtOrderHash");


      // add fundStateIdPrevious
      cmdText = @"
SELECT 
  fs.fundstateid, fs.fundid, f.txid, f.vout, fs.fundstatus, fs.fundstatusprevious, n.nodeid,
  co.courtOrderHash, startEnforceAtHeight, stopEnforceAtHeight, hasUnfreezeOrder
FROM 
  fundState fs CROSS JOIN node n
  JOIN fund f ON f.fundid = fs.fundid
  LEFT JOIN fundStateEnforceAtHeight fsh ON fsh.fundStateId=fs.fundStateIdPrevious
  LEFT JOIN courtOrder co ON co.internalCourtOrderId=fsh.internalCourtOrderId
  LEFT JOIN fundStateNode fsn ON fsn.fundstateid=fs.fundstateid AND fsn.nodeid=n.nodeid
WHERE 
  fs.fundStatus IN (@processed)  
  AND n.nodeStatus = @connected
  AND fsn.fundstateid is null
ORDER BY
  n.nodeid, fs.fundstateid
";

      _ = await connection.QueryAsync<FundStateToPropagate, EnforceAtHeight, FundStateToPropagate>(cmdText, (fsp, eah) =>
      {
        if (!allFundsToBePropagated.TryGetValue(fsp.Key, out FundStateToPropagate fspEntity))
        {
          throw new BadRequestException($"There should be no new fundStateId to propagate. New key is '{fsp.Key}'");
        }

        fspEntity.EnforceAtHeightPrevious.AddOnlyUnique(eah);
        return fspEntity;
      },
      param: new
      {
        processed = (int)FundStatus.Processed,
        connected = (int)NodeStatus.Connected
      },
      transaction,
      splitOn: "courtOrderHash");

      _logger.LogTrace("GetFundStateToPropagateAsync end");
      return allFundsToBePropagated.Values;
    }

    public async Task InsertFundStateNodeAsync(IEnumerable<FundStatePropagated> fundStateNodeList)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      using (var writer =
        transaction.Connection.BeginBinaryImport(
          "COPY fundstatenode (fundstateid, nodeid, propagatedat) FROM STDIN (FORMAT BINARY)"))
      {
        foreach (var fundStateNode in fundStateNodeList)
        {
          writer.StartRow();
          writer.Write(fundStateNode.StateToPropagate.Id, NpgsqlTypes.NpgsqlDbType.Bigint);
          writer.Write(fundStateNode.Node.Id, NpgsqlTypes.NpgsqlDbType.Integer);
          writer.Write(fundStateNode.PropagatedAt, NpgsqlTypes.NpgsqlDbType.TimestampTz);
        }

        await writer.CompleteAsync();
      }
      await transaction.CommitAsync();
    }


    class FundWithCourtOrder
    {
      public string CourtOrderHash { get; set; }

      public string Txid { get; set; }
      public long Vout { get; set; }
      public long Value { get; set; }
      public FundStatus FundStatus { get; set; }

    }

    private async Task<IEnumerable<CourtOrder>> GetCourtOrderListAsync(NpgsqlTransaction transaction, string courtOrderhash)
    {
      string cmdText = @"
SELECT  
 courtOrderType ""Type"", courtOrderId, validFrom, validTo, courtOrderHash, enforceAtHeight, courtOrderStatus Status, freezeCourtOrderId, freezeCourtOrderHash, signedByKey
FROM 
  CourtOrder co 
WHERE @courtOrderHash IS NULL OR  courtOrderHash = @courtOrderHash";

      var courtOrders = await transaction.Connection.QueryAsync<CourtOrder>(cmdText,
        new
        {
          courtOrderHash = courtOrderhash,
          freezePolicy = (int)CourtOrderStatus.FreezePolicy,
          freezeConsensus = (int)CourtOrderStatus.FreezeConsensus,
          unfreezeConsensus = (int)CourtOrderStatus.UnfreezeConsensus,
          imported = (int)CourtOrderStatus.Imported
        });

      return courtOrders.ToArray();
    }

    public async Task<IEnumerable<CourtOrder>> GetCourtOrdersAsync(string courtOrderhash, bool includeFunds)
    {
      _logger.LogTrace("GetCourtOrders start");
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();
      var courtOrders = await GetCourtOrderListAsync(transaction, courtOrderhash);

      if (includeFunds)
      {
        string cmdFunds = @"
SELECT
  txid, vout, value, fundStatus, courtOrderHash 
FROM 
  fund
  INNER JOIN CourtOrderFund ON CourtOrderFund.fundId = Fund.FundID
  INNER JOIN CourtOrder     ON CourtOrder.internalCourtOrderId = CourtOrderFund.internalCourtOrderId  
WHERE
  @courtOrderHash IS NULL OR  courtOrderHash = @courtOrderHash
ORDER BY
  courtOrderHash
";

        var funds = await connection.QueryAsync<FundWithCourtOrder>(cmdFunds,
          new
          {
            courtOrderHash = courtOrderhash
          }
        );

        var fundsGrouped = funds.GroupBy(x => x.CourtOrderHash).ToArray();


        var coDictionary = courtOrders.ToDictionary(x => x.CourtOrderHash);
        foreach (var fundsByCo in fundsGrouped)
        {
          coDictionary[fundsByCo.Key].AddFunds(
            fundsByCo.Select(x =>
              new Fund(x.Txid, x.Vout, x.Value, x.FundStatus))
            );
        }
      }

      _logger.LogTrace("GetCourtOrders end");
      return courtOrders;
    }

    public async Task<IEnumerable<string>> GetCourtOrdersToActivateAsync()
    {
      _logger.LogTrace("GetCourtOrdersToActivateAsync start");
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = @"
SELECT courtOrderHash 
FROM courtOrder 
WHERE courtOrderStatus = @status
ORDER BY signedDate
";
      var courtOrders = await connection
        .QueryAsync<string>(cmdText,
          new
          {
            status = (int)CourtOrderStatus.Imported
          });

      _logger.LogTrace("GetCourtOrdersToActivateAsync end");
      return courtOrders.ToArray();
    }

    public async Task<(string ActivationHash, int EnforceAtHeight)[]> GetUnprocessedConsensusActivationsAsync()
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = @$"
SELECT c.courtorderhash, ca.enforceatheight
FROM consensusactivation ca
INNER JOIN courtorder c ON c.internalcourtorderid = ca.internalcourtorderid
INNER JOIN consensusactivationlegalentityendpoint coale ON coale.consensusactivationid = ca.consensusactivationid
WHERE coale.lasterror IS NULL
  AND c.courtorderstatus IN ({(int)CourtOrderStatus.FreezePolicy}, {(int)CourtOrderStatus.UnfreezeNoConsensusYet}, {(int)CourtOrderStatus.ConfiscationPolicy})
ORDER BY ca.signeddate;
";

      var result = await connection.QueryAsync<(string, int)>(cmdText);
      return result.ToArray();
    }

    public async Task<IEnumerable<PendingConsensusActivation>> GetPendingConsensusActivationsAsync(int maxRetryCount, int rangeToCheck)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = $@"
-- all with no consensus activation
SELECT 
  c.internalCourtOrderId,
  c.courtOrderHash,
  c.courtOrderType as courtOrderTypeId,
  l.legalEntityEndpointId,
  l.baseUrl as legalEntityEndpointUrl,
  l.apiKey as legalEntityEndpointApiKey,
  0 as retryCount
FROM 
  courtOrder c
  JOIN courtOrderAcceptance coa ON coa.internalCourtOrderId=c.internalCourtOrderId
  JOIN legalEntityEndpoint l ON l.legalEntityEndpointId=coa.legalEntityEndpointId
  LEFT JOIN consensusActivation ca ON ca.internalCourtOrderId=c.internalCourtOrderId
WHERE 
  ca.consensusActivationId is null
  AND c.courtorderstatus IN ({(int)CourtOrderStatus.FreezePolicy}, {(int)CourtOrderStatus.UnfreezeNoConsensusYet}, {(int)CourtOrderStatus.ConfiscationPolicy})
  AND coalesce(l.validUntil, now() at time zone 'utc') >= now() at time zone 'utc'
  AND coa.courtOrderAcceptanceSubmittedAt > @consensusAge

UNION

-- all with not valid consensus activation
SELECT 
  c.internalCourtOrderId,
  c.courtOrderHash,
  c.courtOrderType as courtOrderTypeId,
  l.legalEntityEndpointId,
  l.baseUrl as legalEntityEndpointUrl,
  l.apiKey as legalEntityEndpointApiKey,
  cal.retryCount
FROM 
  courtOrder c
  JOIN courtOrderAcceptance coa ON coa.internalCourtOrderId=c.internalCourtOrderId
  JOIN legalEntityEndpoint l ON l.legalEntityEndpointId=coa.legalEntityEndpointId
  JOIN consensusActivation ca ON ca.internalCourtOrderId=c.internalCourtOrderId
  JOIN consensusActivationLegalEntityEndpoint cal ON cal.consensusActivationId=ca.consensusActivationId and cal.legalEntityEndpointId=l.legalEntityEndpointId
WHERE 
  cal.lastErrorAt is not null
  AND (cal.retryCount IS NULL OR cal.retryCount < @maxRetryCount)
  AND coalesce(l.validUntil, now() at time zone 'utc') >= now() at time zone 'utc'
  AND coa.courtOrderAcceptanceSubmittedAt > @consensusAge
";
      var result = await connection
        .QueryAsync<PendingConsensusActivation>(cmdText, new { maxRetryCount, consensusAge = DateTime.UtcNow.AddDays(-rangeToCheck) });

      return result.ToArray();
    }

    public async Task InsertConsensusActivationAsync(ConsensusActivation consensusActivation, long internalCourtOrderId, int legalEntityEndpointId, bool isCaValid, Network network, int? retryCount)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      using var transaction = await connection.BeginTransactionAsync();
      // on conflict we do dummy update to force returning consensusActivationId. See https://stackoverflow.com/questions/34708509/how-to-use-returning-with-on-conflict-in-postgresql
      string cmdText = @"
INSERT INTO consensusActivation (internalCourtOrderId, signedConsensusActivationJSON, consensusActivationHash, signedDate, signedByKey, enforceAtHeight)
VALUES (@internalCourtOrderId, @signedConsensusActivationJSON, @consensusActivationHash, @signedDate, @signedByKey, @enforceAtHeight)
ON CONFLICT (consensusActivationHash) DO UPDATE SET consensusActivationHash=consensusActivation.consensusActivationHash
RETURNING consensusActivationId
";
      var consensusActivationId = await connection.ExecuteScalarAsync<long>(cmdText,
          new
          {
            internalCourtOrderId,
            signedConsensusActivationJSON = consensusActivation.SignedConsensusActivationJson,
            consensusActivationHash = consensusActivation.Hash,
            signedDate = consensusActivation.SignedDate,
            signedByKey = consensusActivation.PublicKey,
            enforceAtHeight = consensusActivation.EnforceAtHeight
          },
          transaction);

      cmdText = @"
INSERT INTO consensusActivationLegalEntityEndpoint (consensusActivationId, legalEntityEndpointId, receivedAt, lastError, lastErrorAt, retryCount)
VALUES (@consensusActivationId, @legalEntityEndpointId, now() at time zone 'utc', @lastError, @lastErrorAt, @retryCount)
ON CONFLICT (consensusActivationId, legalEntityEndpointId) DO UPDATE SET lastError=@lastError, lastErrorAt=@lastErrorAt, retryCount = @retryCount, receivedAt=now() at time zone 'utc'
";
      await connection.ExecuteAsync(cmdText,
        new
        {
          legalEntityEndpointId,
          consensusActivationId,
          lastError = isCaValid ? null : "Invalid consensus activation",
          lastErrorAt = isCaValid ? (DateTime?)null : DateTime.UtcNow,
          retryCount
        },
        transaction);

      if (consensusActivation.ConfiscationTimelockedTxs.Count > 0)
      {
        await InsertTransactionsAsync(transaction, internalCourtOrderId, consensusActivation.ConfiscationTimelockedTxs, true);
      }
      await transaction.CommitAsync();
    }

    public async Task UpdateLegalEntityEndpointErrorAsync(int legalEntityEndpointId, string error)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      using var transaction = await connection.BeginTransactionAsync();
      string cmdText =
        "UPDATE legalEntityEndpoint " +
        "SET lastError = @error, lastErrorAt = now() at time zone 'utc', lastContactedAt = now() at time zone 'utc'" +
        "WHERE legalEntityEndpointId=@legalEntityEndpointId";

      await connection.ExecuteAsync(cmdText,
          new
          {
            error,
            legalEntityEndpointId
          },
          transaction);
    }

    public static async Task EmptyRepositoryAsync(string connectionString)
    {
      var tableOrderToTruncate = new string[]
      {
        "fundstatenode",
        "nodewhitelist",
        "node",
        "fundstateenforceatheight",
        "fundenforceatheight",
        "fundstate",
        "courtorderfund",
        "fund",
        "courtorderstate",
        "consensusActivationLegalEntityEndpoint",
        "courtOrderAcceptance",
        "legalEntityEndpoint",
        "consensusActivation",
        "confiscationtransaction",
        "courtorder",
        "delegatingKey",
        "delegatedKey",
        "configurationParam",
        "trustlist",
      };
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(connectionString);
      using (var transaction = connection.BeginTransaction())
      {

        await transaction.Connection.ExecuteAsync("UPDATE fundstate SET fundstateidprevious = NULL;", null);
        await transaction.CommitAsync();
      }
      foreach (var tableName in tableOrderToTruncate)
      {
        using var transaction = connection.BeginTransaction();
        string cmdText = $"DELETE FROM {tableName};";
        await transaction.Connection.ExecuteAsync(cmdText, null);
        await transaction.CommitAsync();
        // Delay between each transaction because it sometimes happens that previous transaction
        // in postgres hasn't completed everything even though await has returned
        await Task.Delay(20);
      }

      using (var transaction = connection.BeginTransaction())
      {
        string cmdText = @"
ALTER SEQUENCE fundstate_fundstateid_seq RESTART  WITH 1;
ALTER SEQUENCE legalentityendpoint_legalentityendpointid_seq RESTART  WITH 1;
ALTER SEQUENCE node_nodeid_seq RESTART  WITH 1;
";
        await transaction.Connection.ExecuteAsync(cmdText, null);
        await transaction.CommitAsync();
      }
    }

    private async Task InsertCourtOrderAcceptanceStatusAsync(NpgsqlTransaction transaction, long internalCourtOrderId, int? legalEntityEndpointId)
    {
      var cmdText = "INSERT INTO CourtOrderAcceptance (internalCourtOrderId, legalEntityEndpointId, courtOrderReceivedAt) " +
                    "VALUES (@internalCourtOrderId, @legalEntityEndpointId, @courtOrderReceivedAt)";

      await transaction.Connection.ExecuteAsync(cmdText,
                                                      new
                                                      {
                                                        internalCourtOrderId,
                                                        legalEntityEndpointId,
                                                        courtOrderReceivedAt = DateTime.UtcNow
                                                      });
    }

    public async Task<IEnumerable<CourtOrderWithAcceptance>> GetCourtOrdersToSendAcceptancesAsync(int maxRetryCount)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();
      string cmdTextCO = "SELECT co.internalCourtOrderId, co.courtOrderHash, coa.retryCount " +
                         "FROM CourtOrder co " +
                         "INNER JOIN CourtOrderAcceptance coa ON co.internalCourtOrderId = coa.internalCourtOrderId " +
                         "WHERE coa.courtOrderAcceptanceSubmittedAt IS NULL AND (coa.RetryCount IS NULL OR coa.RetryCount < @maxRetryCount)";

      var courtOrders = await transaction.Connection.QueryAsync<CourtOrderWithAcceptance>(cmdTextCO, new { maxRetryCount });

      foreach (var co in courtOrders)
      {
        co.CourtOrderAcceptances = await GetCourtOrderAcceptancesAsync(co.InternalCourtOrderId, transaction);
      }
      return courtOrders;
    }

    public async Task<IEnumerable<CourtOrderAcceptance>> GetCourtOrderAcceptancesAsync(long internalCourtOrderId, IDbTransaction transaction = null)
    {
      static async Task<IEnumerable<CourtOrderAcceptance>> coAcceptances(long internalCourtOrderId, IDbTransaction trans)
      {
        string cmdTextCOAcceptance = "SELECT courtOrderAcceptanceId, legalEntityEndpointId " +
                                     "FROM CourtOrderAcceptance " +
                                     "WHERE internalCourtOrderId = @internalCourtOrderId AND courtOrderAcceptanceSubmittedAt IS NULL";
        return await trans.Connection.QueryAsync<CourtOrderAcceptance>(cmdTextCOAcceptance, new { internalCourtOrderId });
      }

      if (transaction == null)
      {
        using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
        using var innerTransaction = connection.BeginTransaction();
        return await coAcceptances(internalCourtOrderId, innerTransaction);
      }
      else
      {
        return await coAcceptances(internalCourtOrderId, transaction);
      }
    }

    public async Task<int> SetCourtOrderAcceptanceStatusAsync(int courtOrderAcceptanceId, string signedCOAcceptance, DateTime? coAcceptanceSubmitedAt, string error, int? retryCount)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = connection.BeginTransaction();
      var cmdText = "UPDATE CourtOrderAcceptance " +
                    "SET signedCourtOrderAcceptanceJSON = @signedCOAcceptance, courtOrderAcceptanceSubmittedAt = @coAcceptanceSubmitedAt, lastError = @error, lastErrorAt = @lastErrorAt, retryCount = @retryCount " +
                    "WHERE courtOrderAcceptanceId = @courtOrderAcceptanceId";

      var result = await transaction.Connection.ExecuteAsync(cmdText,
                                                            new
                                                            {
                                                              courtOrderAcceptanceId,
                                                              signedCOAcceptance,
                                                              coAcceptanceSubmitedAt,
                                                              error,
                                                              lastErrorAt = (error == null ? (object)DBNull.Value : DateTime.UtcNow),
                                                              retryCount
                                                            });

      await transaction.CommitAsync();
      return result;
    }

    public async Task<IEnumerable<CourtOrder>> GetCourtOrderForConfiscationTxWhiteListAsync()
    {
      string cmdText = $@"
SELECT CourtOrderId, CourtOrderHash, EnforceAtHeight, CourtOrderStatus Status
FROM CourtOrder
WHERE CourtOrderType = {(int)CourtOrderType.Confiscation} AND
      CourtOrderStatus = {(int)CourtOrderStatus.ConfiscationConsensus};
";

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      var courtOrders = await connection.QueryAsync<CourtOrder>(cmdText);

      return courtOrders;
    }

    public async Task<IEnumerable<CourtOrder>> GetCourtOrdersWaitingForConfiscationAsync(int courtOrderStatus, long minEAT, long maxEAT)
    {
      string cmdText = @"
SELECT CourtOrderId, CourtOrderHash, EnforceAtHeight
FROM CourtOrder
WHERE CourtOrderStatus = @courtOrderStatus AND 
      CourtOrderType = @CourtOrderType AND 
      EnforceAtHeight BETWEEN @minEAT AND @maxEAT";

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      var courtOrders = await connection.QueryAsync<CourtOrder>(cmdText, new
      {
        courtOrderStatus,
        CourtOrderType = CourtOrderType.Confiscation,
        minEAT,
        maxEAT
      });

      return courtOrders;
    }

    public async Task<IEnumerable<TransactionToSend>> GetConfiscationTransactionsAsync(int minEAT, int heightForSubmit, bool forceResend)
    {
      string cmdText = @"
SELECT transactionId TxId, transactionBody Body, RewardTransaction
FROM ConfiscationTransaction 
WHERE enforceAtHeight BETWEEN @minEAT AND @currentHeight
";

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      var confiscationTransactions = await connection.QueryAsync<TransactionToSend>(cmdText, new
      {
        currentHeight = heightForSubmit,
        minEAT
      });

      return confiscationTransactions;
    }

    public async Task<IEnumerable<TransactionToSend>> GetConfiscationTransactionsForWhiteListAsync(string courtOrderHash, int nodeId)
    {
      string cmdText = @"
SELECT transactionBody Body, ct.enforceAtHeight
FROM ConfiscationTransaction ct
INNER JOIN CourtOrder c ON c.internalCourtOrderId = ct.internalCourtOrderId
WHERE c.CourtOrderHash = @courtOrderHash AND
      ct.rewardTransaction = false AND 
      NOT EXISTS (SELECT FROM NodeWhiteList nw 
                  WHERE nw.confiscationTransactionId = ct.confiscationTransactionId AND 
                        nw.nodeId = @nodeId)";

      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      var confiscationTransactions = await connection.QueryAsync<TransactionToSend>(cmdText, new
      {
        courtOrderHash,
        nodeId
      });

      return confiscationTransactions;
    }

    public async Task InsertWhitelistedNodeInfoAsync(string courtOrderHash, int nodeId)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
INSERT INTO NodeWhiteList
SELECT confiscationTransactionId, @nodeId, @submittedAt
FROM ConfiscationTransaction ct 
INNER JOIN CourtOrder c ON c.internalCourtOrderId = ct.internalCourtOrderId
WHERE c.CourtOrderHash = @courtOrderHash AND
      ct.rewardTransaction = false;
";

      await transaction.Connection.ExecuteScalarAsync(cmdText, new { courtOrderHash, nodeId, submittedAt = DateTime.UtcNow });
      await transaction.CommitAsync();
    }

    public async Task SetTransactionErrorsAsync(SendRawTransactionsResult[] transactionsResults)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      string cmdCreateTxResultTemp = @"CREATE TEMPORARY TABLE TxResult_temp (
                                    txId VARCHAR(64), 
                                    submittedAtHeight INT,
                                    lastErrorAtHeight INT,
                                    lastErrorCode INT, 
                                    lastError TEXT) ON COMMIT DROP;";

      await transaction.Connection.ExecuteAsync(cmdCreateTxResultTemp);

      using (var txResultimporter = transaction.Connection.BeginBinaryImport(@"COPY TxResult_temp (txId, submittedAtHeight, lastErrorAtHeight, lastErrorCode, lastError) FROM STDIN (FORMAT BINARY)"))
      {
        foreach (var txResult in transactionsResults)
        {
          txResultimporter.StartRow();

          txResultimporter.Write(txResult.TxId, NpgsqlTypes.NpgsqlDbType.Varchar);
          txResultimporter.Write((object)txResult.SubmittedAtHeight ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Integer);
          txResultimporter.Write((object)txResult.ErrorAtHeight ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Integer);
          txResultimporter.Write((object)txResult.ErrorCode ?? DBNull.Value, NpgsqlTypes.NpgsqlDbType.Integer);
          txResultimporter.Write(txResult.ErrorDescription, NpgsqlTypes.NpgsqlDbType.Varchar);
        }
        await txResultimporter.CompleteAsync();
      }

      string cmdText = @"
UPDATE ConfiscationTransaction
SET submittedAtHeight = tmp.submittedAtHeight, lastErrorAtHeight = NULL, lastErrorCode = NULL, lastError = NULL
FROM TxResult_temp tmp
WHERE transactionId = tmp.txId AND tmp.submittedAtHeight IS NOT NULL; 

UPDATE ConfiscationTransaction
SET lastErrorAtHeight = tmp.lastErrorAtHeight, lastErrorCode = tmp.lastErrorCode, lastError = tmp.lastError
FROM TxResult_temp tmp
WHERE transactionId = tmp.txId AND tmp.submittedAtHeight IS NULL;
";
      await transaction.Connection.ExecuteAsync(cmdText);

      await transaction.CommitAsync();
    }

    public async Task<IEnumerable<TransactionStatus>> GetConfiscationTransactionsStatusAsync(string courtOrderHash)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      string cmdText = @"
SELECT transactionId, ct.enforceAtHeight, submittedAtHeight, lastErrorAtHeight, lastErrorCode, lastError
FROM ConfiscationTransaction ct
INNER JOIN CourtOrder c ON c.internalCourtOrderId = ct.internalCourtOrderId
WHERE c.CourtOrderHash = @courtOrderHash;
";

      var result = await connection.QueryAsync<TransactionStatus>(cmdText, new { courtOrderHash });

      return result;
    }

    public async Task InsertValidationErrorsAsync(int legalEntityEndpointId, string courtOrderhash, string errorData, string lastError)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
INSERT INTO CourtOrderValidationError (legalEntityEndpointId, courtOrderhash, errorData, lastError, lastErrorAt, retryCount)
VALUES (@legalEntityEndpointId, @courtOrderhash, @errorData, @lastError, @lastErrorAt, @retryCount)
ON CONFLICT (legalEntityEndpointId, courtOrderhash)
DO 
UPDATE SET lastError = @lastError, 
           lastErrorAt = @lastErrorAt, 
           retryCount = (SELECT retryCount + 1 FROM CourtOrderValidationError WHERE legalEntityEndpointId = @legalEntityEndpointId AND courtOrderhash = @courtOrderhash);
";

      await transaction.Connection.ExecuteAsync(cmdText, new
      {
        legalEntityEndpointId,
        courtOrderhash,
        errorData,
        lastError,
        lastErrorAt = DateTime.UtcNow,
        retryCount = 1
      });

      await transaction.CommitAsync();
    }

    public async Task<IEnumerable<ValidationError>> GetValidationErrorsAsync(int maxRetryCount)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);

      string cmdText = @"
SELECT legalEntityEndpointId, courtOrderhash, errorData
FROM CourtOrderValidationError
WHERE submittedAt IS NULL AND
      retryCount <= @maxRetryCount
";

      return await connection.QueryAsync<ValidationError>(cmdText, new { maxRetryCount });
    }

    public async Task MarkValidationErrorSuccessfulAsync(int legalEntityEndpointId, string courtOrderhash)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE CourtOrderValidationError
SET submittedAt = @submittedAt, lastError = NULL, lastErrorAt = NULL
WHERE legalEntityEndpointId = @legalEntityEndpointId AND
      courtOrderhash = @courtOrderhash;
";

      await transaction.Connection.ExecuteAsync(cmdText, new
      {
        submittedAt = DateTime.UtcNow,
        legalEntityEndpointId,
        courtOrderhash
      });

      await transaction.CommitAsync();
    }

    public async Task<IEnumerable<ValidationError>> GetFailedCourtOrdersAsync()
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = @"
SELECT legalEntityEndpointId, courtOrderhash
FROM CourtOrderValidationError
WHERE successfullyProcessedAt IS NULL;
";

      return await connection.QueryAsync<ValidationError>(cmdText);
    }

    public async Task MarkCourtOrderSuccessfullyProccesedAsync(int legalEntityEndpointId, string courtOrderhash)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE CourtOrderValidationError
SET successfullyProcessedAt = @successfullyProcessedAt
WHERE legalEntityEndpointId = @legalEntityEndpointId AND
      courtOrderhash = @courtOrderhash;
";

      await transaction.Connection.ExecuteAsync(cmdText, new
      {
        successfullyProcessedAt = DateTime.UtcNow,
        legalEntityEndpointId,
        courtOrderhash
      });

      await transaction.CommitAsync();
    }

    public async Task<IEnumerable<CourtOrderQuery>> QueryCourtOrdersAsync(string courtOrderHash, bool includeFunds)
    {
      using var connection = new NpgsqlConnection(_connectionString);
      await connection.OpenAsync();
      string cmdText = $@"
SELECT 
	c.courtOrderHash as courtOrderHash_c, c.courtorderstatus,
	c.courtOrderType, c.courtOrderId,
	(SELECT MIN(changedAt) FROM courtOrderState cos WHERE cos.courtOrderStatus in ({(int)CourtOrderStatus.FreezePolicy},{(int)CourtOrderStatus.UnfreezeNoConsensusYet}, {(int)CourtOrderStatus.ConfiscationPolicy}) AND cos.internalCourtOrderId=c.internalCourtOrderId ) AS policyEnforcementStartedAt,
	c.enforceAtHeight as consensusEnforcementStartedAtHeight,
	CASE WHEN t.courtOrderHash=c.courtOrderHash THEN
		MIN(stopEnforceAtHeight) OVER (PARTITION BY t.courtOrderHash)
	ELSE NULL END as consensusEnforcementStoppedAtHeight,
	ARRAY(SELECT courtOrderHash FROM courtOrder AS related WHERE related.internalCourtOrderId=c.freezeInternalCourtOrderId OR related.freezeInternalCourtOrderId=c.internalCourtOrderId) as relatedOrders,
	txId, vOut, 
	t.courtOrderHash, t.courtOrderHashUnfreeze, t.startEnforceAtHeight, t.stopEnforceAtHeight
FROM (
	SELECT
		f.fundId,
		f.txId, f.vOut,		
		fco.courtOrderHashRef as courtOrderHash,
		fco.courtOrderHash as courtOrderHashUnfreeze, 
		fco.courtOrderType,
		fco.enforceAtHeight as startEnforceAtHeight, 
		fco.enforceAtHeightUnfreeze as stopEnforceAtHeight
	FROM
		fundWithCourtOrder fco
		JOIN fund f ON f.fundId=fco.fundId
	WHERE 
		fco.courtOrderType={(int)CourtOrderType.Unfreeze}

	UNION

-- without unfreeze court order
	SELECT 
		f.fundId,
		f.txId, f.vOut,
		fco.courtOrderHash, 
		null as courtOrderHashUnfreeze,
		fco.courtOrderType,
		fco.enforceAtHeight as startEnforceAtHeight, 
		fco.enforceAtHeightUnfreeze as stopEnforceAtHeight
	FROM
		fundWithCourtOrder fco
		JOIN fund f ON f.fundId=fco.fundId
	WHERE 
		fco.courtOrderType={(int)CourtOrderType.Freeze}
		AND NOT EXISTS (SELECT * FROM fundWithCourtOrder fco2 WHERE fco2.courtOrderType={(int)CourtOrderType.Unfreeze} AND fco2.courtOrderHashRef=fco.courtOrderHash)
	) t
	JOIN courtOrderFund cof ON cof.fundId=t.fundId
	JOIN courtOrder c ON c.internalCourtOrderId=cof.internalCourtOrderId
WHERE
	1=1 ";

      if (!string.IsNullOrEmpty(courtOrderHash))
      {
        cmdText += "AND c.courtOrderHash=@courtOrderHash";
      }

      var allC = new Dictionary<string, CourtOrderQuery>();
      var allF = new Dictionary<string, FundQuery>();

      await connection.QueryAsync<CourtOrderQuery, FundQuery, EnforceAtHeightQuery, CourtOrderQuery>(cmdText,
        (co, cof, eah) =>
        {
          if (!allC.TryGetValue(co.CourtOrderHash, out CourtOrderQuery coEntity))
          {
            coEntity = co;
            allC.Add(co.CourtOrderHash, coEntity);
          }
          if ((co.ConsensusEnforcementStoppedAtHeight ?? int.MaxValue) < (coEntity.ConsensusEnforcementStoppedAtHeight ?? int.MaxValue))
          {
            coEntity.ConsensusEnforcementStoppedAtHeight = co.ConsensusEnforcementStoppedAtHeight;
          }
          if (includeFunds)
          {
            if (!allF.TryGetValue(coEntity.CourtOrderHash + cof.GetKey(), out FundQuery cofEntity))
            {
              cofEntity = cof;
              allF.Add(coEntity.CourtOrderHash + cof.GetKey(), cofEntity);
              coEntity.Funds.Add(cofEntity);
            }
            cofEntity.EnforceAtHeight.Add(eah);
          }
          return coEntity;
        },
        param:
          new
          {
            courtOrderHash
          },
        splitOn: "txId,courtOrderHash");
      return allC.Values;
    }

    public async Task<FundQuery> QueryFundByTxOutAsync(string txId, long vout)
    {
      using var connection = new NpgsqlConnection(_connectionString);
      await connection.OpenAsync();
      string cmdText = $@"
SELECT 
	*
FROM (
	SELECT 
		f.txId, f.vOut,		
		fco.courtOrderHashRef as courtOrderHash,
		fco.courtOrderHash as courtOrderHashUnfreeze, 
		fco.courtOrderType,
		fco.enforceAtHeight as startEnforceAtHeight, 
		fco.enforceAtHeightUnfreeze as stopEnforceAtHeight
	FROM
		fundWithCourtOrder fco
		JOIN fund f ON f.fundId=fco.fundId
	WHERE 
		fco.courtOrderType={(int)CourtOrderType.Unfreeze}

	UNION

-- without unfreeze court order
	SELECT 
		f.txId, f.vOut,
		fco.courtOrderHash, 
		null as courtOrderHashUnfreeze,
		fco.courtOrderType,
		fco.enforceAtHeight as startEnforceAtHeight, 
		fco.enforceAtHeightUnfreeze as stopEnforceAtHeight
	FROM
		fundWithCourtOrder fco
		JOIN fund f ON f.fundId=fco.fundId
	WHERE 
		fco.courtOrderType={(int)CourtOrderType.Freeze}
		AND NOT EXISTS (SELECT * FROM fundWithCourtOrder fco2 WHERE fco2.courtOrderType={(int)CourtOrderType.Unfreeze} AND fco2.courtOrderHashRef=fco.courtOrderHash)
	) t
WHERE
	txId=@txId AND vOut=@vOut";

      var all = new Dictionary<string, FundQuery>();

      await connection.QueryAsync<FundQuery, EnforceAtHeightQuery, FundQuery>(cmdText,
        (cof, eah) =>
        {
          if (!all.TryGetValue(cof.GetKey(), out FundQuery cofEntity))
          {
            cofEntity = cof;
            all.Add(cof.GetKey(), cofEntity);
          }
          cofEntity.EnforceAtHeight.Add(eah);
          return cofEntity;
        },
        param:
          new
          {
            txId = txId.ToLower(),
            vout
          },
        splitOn: "courtOrderHash");
      return all.Values.SingleOrDefault();
    }

    public async Task CancelConfiscationOrderAsync(string courtOrderHash)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      using var transaction = await connection.BeginTransactionAsync();

      string cmdText = @"
UPDATE courtOrder co
SET cancelled = true
WHERE co.courtOrderHash = @courtOrderHash;
";

      await transaction.Connection.ExecuteScalarAsync(cmdText, new { courtOrderHash });
      await transaction.CommitAsync();
    }

    public async Task<int> GetNumberOfSignedDocumentsAsync(string signedByKey)
    {
      using var connection = await HelperTools.OpenNpgSQLConnectionAsync(_connectionString);
      string cmdText = @"
SELECT COUNT(*)
FROM 
(
  SELECT 1
  FROM courtOrder c
  WHERE c.signedByKey = @signedByKey

  UNION ALL 

  SELECT 1
  FROM consensusActivation ca
  WHERE ca.signedByKey = @signedByKey
) counter;
";

      return await connection.ExecuteScalarAsync<int>(cmdText, new { signedByKey });
    }
  }
}
