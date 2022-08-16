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

namespace BlacklistManager.Infrastructure.Repositories
{
  public class CourtOrderRepositoryPostgres : ICourtOrderRepository
  {
    private readonly string connectionString;
    private readonly ILogger logger;

    public CourtOrderRepositoryPostgres(
      string connectionString,
      ILoggerFactory logger)
    {
      this.connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
      this.logger = logger.CreateLogger(LogCategories.Database) ?? throw new ArgumentNullException(nameof(logger));
    }

    private class QueryFreezeCourtOrderbyHashResult
    {
      public string CourtOrderId { get; set; }
      public long InternalCourtOrderId { get; set; }
    }

    private QueryFreezeCourtOrderbyHashResult QueryFreezeCourtOrderbyHash(IDbTransaction transaction, string courtOrderHash)
    {
      string cmdText =
        "SELECT internalCourtOrderId, courtOrderId FROM CourtOrder WHERE courtOrderHash=@courtOrderHash AND courtOrderType=@courtOrderType";
      var result = transaction.Connection.Query<QueryFreezeCourtOrderbyHashResult>(
        cmdText,
        new
        {
          courtOrderHash,
          courtOrderType = CourtOrderType.Freeze
        }, transaction).ToArray();

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

    public async Task<bool> InsertCourtOrderAsync(CourtOrder courtOrder, string signedCourtOrder, int? legalEntityEndpointId)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());

        using (var transaction = await connection.BeginTransactionAsync())
        {
          QueryFreezeCourtOrderbyHashResult referenceFreezeOrder = null;
          if (courtOrder.Type == CourtOrderType.Unfreeze)
          {
            referenceFreezeOrder = QueryFreezeCourtOrderbyHash(transaction, courtOrder.FreezeCourtOrderHash);
            if (referenceFreezeOrder == null)
            {
              throw new BadRequestException($"Unable to find freeze court order by hash '{courtOrder.FreezeCourtOrderHash}'");
            }
            if (referenceFreezeOrder.CourtOrderId != courtOrder.FreezeCourtOrderId)
            {
              throw new BadRequestException("Court order id of freeze order in database doesn't match referenced parameter FreezeCourtOrderId on provided unfreeze order.");
            }
          }

          string cmdText =
            "INSERT INTO CourtOrder " +
            "(courtOrderId, courtOrderHash, courtOrderType, courtOrderStatus, freezeCourtOrderId, freezeCourtOrderHash, freezeInternalCourtOrderId, validFrom, validTo, signedCourtOrderJson) " +
            "VALUES (@courtOrderId, @courtOrderHash, @courtOrderType, @courtOrderStatus, @freezeCourtOrderId, @freezeCourtOrderHash, @freezeInternalCourtOrderId, @validFrom, @validTo, @signedCourtOrder ) " +
            "ON CONFLICT (courtOrderHash) DO NOTHING " +
            "RETURNING internalCourtOrderId";

          long? internalCourtOrderId;

          internalCourtOrderId = await connection.ExecuteScalarAsync<long?>(cmdText,
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
              signedCourtOrder
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
              await InsertFundsAsync(transaction, internalCourtOrderId.Value, courtOrder.Funds);
            }
            await InsertCourtOrderAcceptanceStatusAsync(transaction, internalCourtOrderId.Value, legalEntityEndpointId);
          }

          await transaction.CommitAsync();

          return internalCourtOrderId.HasValue;
        }
      }
    }

    /// <summary>
    /// Set court order and funds status.
    /// Database trigger writes status changes in CourtOrderState and FundState tables.
    /// </summary>
    public async Task SetCourtOrderStatusAsync(string courtOrderHash, CourtOrderStatus courtOrderStatus, int? enforceAtHeight)
    {
      logger.LogTrace("SetCourtOrderStatusAsync start");

      if (courtOrderStatus == CourtOrderStatus.FreezeConsensus || courtOrderStatus == CourtOrderStatus.UnfreezeConsensus)
      {
        if (!enforceAtHeight.HasValue)
        {
          throw new ArgumentNullException("Parameter should not be null for consensus level status", "enforceAtHeight");
        }
      }
      else
      {
        if (enforceAtHeight.HasValue)
        {
          throw new ArgumentException("Parameter should be null for policy level status", "enforceAtHeight");
        }
      }

      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());

        using (var transaction = connection.BeginTransaction())
        {
          string cmdSetStatus =
            "UPDATE CourtOrder " +
            "  SET courtOrderStatus = @courtOrderStatus, enforceAtHeight = @enforceAtHeight " +
            "  WHERE courtOrderHash = @courtOrderHash" +
            "  RETURNING internalCourtOrderId";


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

          await UpdateFundStatusAsync(transaction, courtOrderHash);
          await transaction.CommitAsync();
        }
      }
      logger.LogTrace("SetCourtOrderStatusAsync end");
    }

    public async Task UpdateFundStatusAsync(IDbTransaction transaction, string courtOrderHash)
    {
      // order by in fundToUpdate is for tests where predictable order of update is required 
      // ProcessCourtOrder_TestRpcCallsForMultipleOrders, step #8 could split removeFromPolicy in two calls
      string cmdUpdateFundStatus =@"
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

INSERT INTO fundEnforceAtHeight (fundId, internalCourtOrderId, startEnforceAtHeight, stopEnforceAtHeight, hasUnfreezeOrder) 
SELECT DISTINCT 
  fundId, 
  internalCourtOrderId,
  COALESCE(startEnforceAtHeight,-1), 
  COALESCE(stopEnforceAtHeight,-1),
  CASE WHEN hasUnfreezeOrder>0 THEN true ELSE false END
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

      logger.LogDebug($"UpdateFundStatus: {rowsAffected} updated");
    }

    public IEnumerable<Fund> GetFunds()
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmdText = @"
SELECT 
  txid, vout, fundstatus as status,
  courtOrderHash, startEnforceAtHeight, stopEnforceAtHeight, hasUnfreezeOrder
FROM 
  fund f
  JOIN fundEnforceAtHeight fh ON fh.fundId=f.fundId
  JOIN courtOrder co ON co.internalCourtOrderId=fh.internalCourtOrderId
ORDER BY
  f.fundId
";
          var all = new Dictionary<string, Fund>();

          var result = connection.Query<Fund, EnforceAtHeight, Fund>(cmdText, 
            (f, eah) =>
            {
              if (!all.TryGetValue(f.TxOut.ToString(), out Fund fEntity))
              {
                all.Add(f.TxOut.ToString(), fEntity = f);
              }

              if (!fEntity.EnforceAtHeight.Contains(eah))
              {
                fEntity.EnforceAtHeight.Add(eah);
              }

              return fEntity;
            },
            transaction,
            splitOn: "courtOrderHash");

          return all.Values;
        }
      }
    }

    private async Task InsertFundsFromFreezeOrderAsync(IDbTransaction transaction, long internalCourtOrderId, string freezeCourtOrderHash)
    {
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
                                        IReadOnlyCollection<Fund> courtOrderFunds)
    {
      string cmdCreateFundsTemp = @"CREATE TEMPORARY TABLE funds_temp (
                                    fundId BIGINT DEFAULT nextval('fund_fundid_seq'),
                                    txId VARCHAR(256), 
                                    vOut BIGINT, 
                                    fundStatus INT,
                                    internalCourtOrderId BIGINT, 
                                    exists BOOL) ON COMMIT DROP;";

      await transaction.Connection.ExecuteAsync(cmdCreateFundsTemp);

      using (var fundImporter = transaction.Connection.BeginBinaryImport(@"COPY funds_temp (txId, vOut, fundStatus, internalCourtOrderId, exists) FROM STDIN (FORMAT BINARY)"))
      {
        foreach (var fund in courtOrderFunds)
        {
          fundImporter.StartRow();

          fundImporter.Write(fund.TxOut.TxId, NpgsqlTypes.NpgsqlDbType.Varchar);
          fundImporter.Write(fund.TxOut.Vout, NpgsqlTypes.NpgsqlDbType.Bigint);
          fundImporter.Write((int)FundStatus.Imported, NpgsqlTypes.NpgsqlDbType.Integer);
          fundImporter.Write(internalCourtOrderId, NpgsqlTypes.NpgsqlDbType.Bigint);
          fundImporter.Write(false, NpgsqlTypes.NpgsqlDbType.Boolean);
        }
        await fundImporter.CompleteAsync();
      }

      string cmdInsertFunds = @"
-- Update temporary table with existing fundId's for funds that are already in DB
UPDATE funds_temp SET fundId = f.fundId, exists = true
FROM (SELECT fundid, txid, vout FROM fund) f
WHERE 
  f.txid = funds_temp.txid AND f.vout = funds_temp.vout;

INSERT INTO fund (fundId, txid, vout, fundStatus) 
SELECT 
  fundId, 
  txId, 
  vOut, 
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
      logger.LogTrace("GetFundStateToPropagateAsync start");
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
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
          var all = new Dictionary<string, FundStateToPropagate>();

          var result = await connection.QueryAsync<FundStateToPropagate, EnforceAtHeight, FundStateToPropagate>(cmdText, (fsp, eah) =>
          {
            if (!all.TryGetValue(fsp.Key, out FundStateToPropagate fspEntity))
            {
              all.Add(fsp.Key, fspEntity = fsp);
            }

            if (!fspEntity.EnforceAtHeight.Contains(eah))
            {
              fspEntity.EnforceAtHeight.Add(eah);
            }
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

          result = await connection.QueryAsync<FundStateToPropagate, EnforceAtHeight, FundStateToPropagate>(cmdText, (fsp, eah) =>
          {
            if (!all.TryGetValue(fsp.Key, out FundStateToPropagate fspEntity))
            {
              throw new BadRequestException($"There should be no new fundStateId to propagate. New key is '{fsp.Key}'");
            }

            if (!fspEntity.EnforceAtHeightPrevious.Contains(eah))
            {
              fspEntity.EnforceAtHeightPrevious.Add(eah);
            }
            return fspEntity;
          },
          param: new
          {
            processed = (int)FundStatus.Processed,
            connected = (int)NodeStatus.Connected
          },
          transaction,
          splitOn: "courtOrderHash");

          logger.LogTrace("GetFundStateToPropagateAsync end");
          return all.Values;
        }
      }
    }

    public void InsertFundStateNode(IEnumerable<FundStatePropagated> fundStateNodeList)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {

          using (var writer =
            transaction.Connection.BeginBinaryImport(
              "COPY fundstatenode (fundstateid, nodeid, propagatedat) FROM STDIN (FORMAT BINARY)"))
          {
            foreach (var fundStateNode in fundStateNodeList)
            {
              writer.StartRow();
              writer.Write(fundStateNode.StateToPropagate.Id, NpgsqlTypes.NpgsqlDbType.Bigint);
              writer.Write(fundStateNode.Node.Id, NpgsqlTypes.NpgsqlDbType.Integer);
              writer.Write(fundStateNode.PropagatedAt, NpgsqlTypes.NpgsqlDbType.Timestamp);
            }

            writer.Complete();
          }
          transaction.Commit();
        }
      }
    }


    class FundWithCourtOrder
    {
      public string CourtOrderHash { get; set; }

      public string Txid { get; set; }
      public long Vout { get; set; }
      public FundStatus FundStatus { get; set; }

    }
    
    private async Task<IEnumerable<CourtOrder>> GetCourtOrderListAsync(NpgsqlTransaction transaction, string courtOrderhash, string courtOrderCondition = null)
    {
      string cmdText =
  @" SELECT  
 courtOrderType, courtOrderId, validFrom, validTo, courtOrderHash, enforceAtHeight, courtOrderStatus, freezeCourtOrderId, freezeCourtOrderHash
FROM 
  CourtOrder co 
WHERE ";

      if (courtOrderCondition == null)
      {
        cmdText += "@courtOrderHash IS NULL OR  courtOrderHash = @courtOrderHash";
      }
      else
      {
        cmdText += courtOrderCondition;
      }

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
      logger.LogTrace("GetCourtOrders start");
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          var courtOrders = await GetCourtOrderListAsync(transaction, courtOrderhash);

          if (includeFunds)
          {
            string cmdFunds = @"
SELECT
  txid, vout, fundStatus, courtOrderHash 
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
                  new Fund(x.Txid, x.Vout, x.FundStatus))
                );
            }
          }

          logger.LogTrace("GetCourtOrders end");
          return courtOrders;
        }
      }
    }

    public async Task<IEnumerable<string>> GetCourtOrdersToActivateAsync()
    {
      logger.LogTrace("GetCourtOrdersToActivateAsync start");
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        string cmdText = @"SELECT courtOrderHash FROM courtOrder WHERE courtOrderStatus = @status";
        var courtOrders = await connection
          .QueryAsync<string>(cmdText,
            new
            {
              status = (int)CourtOrderStatus.Imported
            });

        logger.LogTrace("GetCourtOrdersToActivateAsync end");
        return courtOrders.ToArray();
      }
    }

    public async Task<IEnumerable<PendingConsensusActivation>> GetPendingConsensusActivationsAsync()
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        string cmdText = @"
-- all with no consensus activation
SELECT 
  c.internalCourtOrderId,
  c.courtOrderHash,
  c.courtOrderType as courtOrderTypeId,
  l.legalEntityEndpointId,
  l.baseUrl as legalEntityEndpointUrl,
  l.apiKey as legalEntityEndpointApiKey
FROM 
  courtOrder c
  JOIN courtOrderAcceptance coa ON coa.internalCourtOrderId=c.internalCourtOrderId
  JOIN legalEntityEndpoint l ON l.legalEntityEndpointId=coa.legalEntityEndpointId
  LEFT JOIN consensusActivation ca ON ca.internalCourtOrderId=c.internalCourtOrderId
WHERE 
  ca.consensusActivationId is null
  AND coalesce(l.validUntil, now() at time zone 'utc') >= now() at time zone 'utc'

UNION

-- all with not valid consensus activation
SELECT 
  c.internalCourtOrderId,
  c.courtOrderHash,
  c.courtOrderType as courtOrderTypeId,
  l.legalEntityEndpointId,
  l.baseUrl as legalEntityEndpointUrl,
  l.apiKey as legalEntityEndpointApiKey
FROM 
  courtOrder c
  JOIN courtOrderAcceptance coa ON coa.internalCourtOrderId=c.internalCourtOrderId
  JOIN legalEntityEndpoint l ON l.legalEntityEndpointId=coa.legalEntityEndpointId
  JOIN consensusActivation ca ON ca.internalCourtOrderId=c.internalCourtOrderId
  JOIN consensusActivationLegalEntityEndpoint cal ON cal.consensusActivationId=ca.consensusActivationId and cal.legalEntityEndpointId=l.legalEntityEndpointId
WHERE 
  cal.lastErrorAt is not null
  AND coalesce(l.validUntil, now() at time zone 'utc') >= now() at time zone 'utc'
";
        var result = await connection
          .QueryAsync<PendingConsensusActivation>(cmdText);

        return result.ToArray();
      }
    }

    public async Task InsertConsensusActivationAsync(ConsensusActivation consensusActivation, long internalCourtOrderId, int legalEntityEndpointId, bool isCaValid)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());

        using (var transaction = await connection.BeginTransactionAsync())
        {
          // on conflict we do dummy update to force returning consensusActivationId. See https://stackoverflow.com/questions/34708509/how-to-use-returning-with-on-conflict-in-postgresql
          string cmdText = @"
INSERT INTO consensusActivation (internalCourtOrderId, signedConsensusActivationJSON, consensusActivationHash)
VALUES (@internalCourtOrderId, @signedConsensusActivationJSON, @consensusActivationHash)
ON CONFLICT (consensusActivationHash) DO UPDATE SET consensusActivationHash=consensusActivation.consensusActivationHash
RETURNING consensusActivationId
";
          var consensusActivationId = await connection.ExecuteScalarAsync<long>(cmdText,
              new
              {
                internalCourtOrderId,
                signedConsensusActivationJSON = consensusActivation.SignedConsensusActivationJson,
                consensusActivationHash = consensusActivation.Hash
              },
              transaction);

          cmdText = @"
INSERT INTO consensusActivationLegalEntityEndpoint (consensusActivationId, legalEntityEndpointId, receivedAt, lastError, lastErrorAt)
VALUES (@consensusActivationId, @legalEntityEndpointId, now() at time zone 'utc', @lastError, @lastErrorAt)
ON CONFLICT (consensusActivationId, legalEntityEndpointId) DO UPDATE SET lastError=@lastError, lastErrorAt=@lastErrorAt, receivedAt=now() at time zone 'utc'
";
          await connection.ExecuteAsync(cmdText,
            new
            {
              legalEntityEndpointId,
              consensusActivationId,
              lastError = isCaValid ? null : "Invalid consensus activation",
              lastErrorAt = isCaValid ? (DateTime?)null : DateTime.UtcNow
            },
            transaction);

          await transaction.CommitAsync();
        }
      }
    }

    public async Task UpdateLegalEntityEndpointErrorAsync(int legalEntityEndpointId, string error)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());

        using (var transaction = await connection.BeginTransactionAsync())
        {
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
      }
    }

    public static void EmptyRepository(string connectionString)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());

        string cmdText = @"
DELETE FROM fundstatenode;
DELETE FROM node;
DELETE FROM fundstateenforceatheight;
DELETE FROM fundenforceatheight;
DELETE FROM fundstate;
DELETE FROM courtorderfund;
DELETE FROM fund;
DELETE FROM courtorderstate;
DELETE FROM consensusActivationLegalEntityEndpoint;
DELETE FROM courtOrderAcceptance;
DELETE FROM legalEntityEndpoint;
DELETE FROM consensusActivation;
DELETE FROM courtorder;
DELETE FROM delegatingKey;
DELETE FROM delegatedKey;
DELETE FROM configurationParam;
DELETE FROM trustlist;
ALTER SEQUENCE fundstate_fundstateid_seq RESTART  WITH 1;
ALTER SEQUENCE legalentityendpoint_legalentityendpointid_seq RESTART  WITH 1;
ALTER SEQUENCE node_nodeid_seq RESTART  WITH 1;
";
        connection.Execute(cmdText, null);
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

    public async Task<IEnumerable<CourtOrderWithAcceptance>> GetCourtOrdersToSendAcceptancesAsync()
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          string cmdTextCO = "SELECT co.internalCourtOrderId, co.courtOrderHash " +
                             "FROM CourtOrder co " +
                             "INNER JOIN CourtOrderAcceptance coa ON co.internalCourtOrderId = coa.internalCourtOrderId " +
                             "WHERE coa.courtOrderAcceptanceSubmittedAt IS NULL";

          var courtOrders = await transaction.Connection.QueryAsync<CourtOrderWithAcceptance>(cmdTextCO);

          string cmdTextCOAcceptance = "SELECT courtOrderAcceptanceId, legalEntityEndpointId " +
                                       "FROM CourtOrderAcceptance " +
                                       "WHERE internalCourtOrderId = @internalCourtOrderId AND courtOrderAcceptanceSubmittedAt IS NULL";
          foreach (var co in courtOrders)
          {
            co.CourtOrderAcceptances = await transaction.Connection.QueryAsync<CourtOrderAcceptance>(cmdTextCOAcceptance, new { internalCourtOrderId = co.InternalCourtOrderId });
          }
          return courtOrders;
        }
      }

    }

    public async Task<int> SetCourtOrderAcceptanceStatusAsync(int courtOrderAcceptanceId, string signedCOAcceptance, DateTime? coAcceptanceSubmitedAt, string error)
    {
      using (var connection = new NpgsqlConnection(connectionString))
      {
        RetryUtils.Exec(() => connection.Open());
        using (var transaction = connection.BeginTransaction())
        {
          var cmdText = "UPDATE CourtOrderAcceptance "+
                        "SET signedCourtOrderAcceptanceJSON = @signedCOAcceptance, courtOrderAcceptanceSubmittedAt = @coAcceptanceSubmitedAt, lastError = @error "+
                        "WHERE courtOrderAcceptanceId = @courtOrderAcceptanceId";

          var result = await transaction.Connection.ExecuteAsync(cmdText, 
                                                                new 
                                                                {
                                                                  courtOrderAcceptanceId, 
                                                                  signedCOAcceptance,
                                                                  coAcceptanceSubmitedAt,
                                                                  error
                                                                });

          await transaction.CommitAsync();
          return result;
        }
      }
    }
  }
}
