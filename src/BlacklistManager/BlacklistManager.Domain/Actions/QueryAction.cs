// Copyright (c) 2020 Bitcoin Association

using BlacklistManager.Domain.Models;
using Dapper;
using Npgsql;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlacklistManager.Domain.Actions
{
  public class QueryAction : IQueryAction
  {
    private readonly string connectionString;
    public QueryAction(string connectionString)
    {
      this.connectionString = connectionString;
    }

    public async Task<IEnumerable<CourtOrderQuery>> QueryCourtOrdersAsync(string courtOrderHash, bool includeFunds)
    {
      using var connection = new NpgsqlConnection(connectionString);
      await connection.OpenAsync();
      string cmdText = @"
SELECT 
	c.courtOrderHash as courtOrderHash_c,
	c.courtOrderType, c.courtOrderId,
	(SELECT MIN(changedAt) FROM courtOrderState cos WHERE cos.courtOrderStatus in (@freezePolicy,@unfreezeNoConsensus) AND cos.internalCourtOrderId=c.internalCourtOrderId ) AS policyEnforcementStartedAt,
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
		fco.courtOrderType=2 /*@unfreeze*/

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
		fco.courtOrderType=1 /*@freeze*/
		AND NOT EXISTS (SELECT * FROM fundWithCourtOrder fco2 WHERE fco2.courtOrderType=2 /*@unfreeze*/ AND fco2.courtOrderHashRef=fco.courtOrderHash)
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
      var allF = new Dictionary<string, CourtOrderQuery.Fund>();

      await connection.QueryAsync<CourtOrderQuery, CourtOrderQuery.Fund, CourtOrderQuery.EnforceAtHeight, CourtOrderQuery>(cmdText,
        (co, cof, eah) =>
        {
          if (!allC.TryGetValue(co.CourtOrderHash, out CourtOrderQuery coEntity))
          {
            allC.Add(co.CourtOrderHash, coEntity = co);
          }
          if ((co.ConsensusEnforcementStoppedAtHeight ?? int.MaxValue) < (coEntity.ConsensusEnforcementStoppedAtHeight ?? int.MaxValue))
          {
            coEntity.ConsensusEnforcementStoppedAtHeight = co.ConsensusEnforcementStoppedAtHeight;
          }
          if (includeFunds)
          {
            if (!allF.TryGetValue(coEntity.CourtOrderHash + cof.GetKey(), out CourtOrderQuery.Fund cofEntity))
            {
              allF.Add(coEntity.CourtOrderHash + cof.GetKey(), cofEntity = cof);
              coEntity.Funds.Add(cofEntity);
            }
            cofEntity.EnforceAtHeight.Add(eah);
          }
          return coEntity;
        },
        param:
          new
          {
            courtOrderHash,
            freeze = CourtOrderType.Freeze,
            unfreeze = CourtOrderType.Unfreeze,
            freezePolicy = CourtOrderStatus.FreezePolicy,
            unfreezeNoConsensus = CourtOrderStatus.UnfreezeNoConsensusYet
          },
        splitOn: "txId,courtOrderHash");
      return allC.Values;
    }



    public async Task<CourtOrderQuery.Fund> QueryFundByTxOutAsync(string txId, long vout)
    {
      using var connection = new NpgsqlConnection(connectionString);
      await connection.OpenAsync();
      string cmdText = @"
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
		fco.courtOrderType=@unfreeze

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
		fco.courtOrderType=@freeze
		AND NOT EXISTS (SELECT * FROM fundWithCourtOrder fco2 WHERE fco2.courtOrderType=@unfreeze AND fco2.courtOrderHashRef=fco.courtOrderHash)
	) t
WHERE
	txId=@txId AND vOut=@vOut";

      var all = new Dictionary<string, CourtOrderQuery.Fund>();

      await connection.QueryAsync<CourtOrderQuery.Fund, CourtOrderQuery.EnforceAtHeight, CourtOrderQuery.Fund>(cmdText,
        (cof, eah) =>
        {
          if (!all.TryGetValue(cof.GetKey(), out CourtOrderQuery.Fund cofEntity))
          {
            all.Add(cof.GetKey(), cofEntity = cof);
          }
          cofEntity.EnforceAtHeight.Add(eah);
          return cofEntity;
        },
        param:
          new
          {
            txId = txId.ToLower(),
            vout,
            freeze = CourtOrderType.Freeze,
            unfreeze = CourtOrderType.Unfreeze
          },
        splitOn: "courtOrderHash");
      return all.Values.SingleOrDefault();
    }
  }
}
