CREATE OR REPLACE VIEW public.fundwithcourtorder
AS SELECT f.fundid,
        CASE
            WHEN co.courtordertype = 1 THEN co.internalcourtorderid
            ELSE cor.internalcourtorderid
        END AS internalcourtorderid,
    co.courtorderhash,
        CASE
            WHEN co.courtordertype = 1 THEN co.courtorderhash
            ELSE co.freezecourtorderhash
        END AS courtorderhashref,
    co.courtordertype,
        CASE
            WHEN co.courtordertype = 1 THEN co.enforceatheight
            ELSE cor.enforceatheight
        END AS enforceatheight,
        CASE
            WHEN co.courtordertype = 2 THEN co.enforceatheight
            ELSE NULL::integer
        END AS enforceatheightunfreeze,
        CASE
            WHEN co.courtordertype = 2 THEN 1
            ELSE 0
        END AS hasunfreezeorder,
        CASE
            WHEN co.courtordertype = 3 AND co.courtorderstatus <> 303 THEN 1 -- Keep in sync with BlacklistManager.Domain.Models.CourtOrderStatus.ConfiscationCancelled status
            ELSE 0
        END AS hasconfiscationorder
   FROM fund f
     JOIN courtorderfund cof ON cof.fundid = f.fundid
     JOIN courtorder co ON cof.internalcourtorderid = co.internalcourtorderid
     LEFT JOIN courtorder cor ON cor.courtorderhash::text = co.freezecourtorderhash::text
  WHERE co.courtorderstatus <> 199 AND COALESCE(cor.courtorderstatus, 0) <> 199;

CREATE OR REPLACE VIEW public.fundwithcourtorderpivot
AS SELECT f.fundid,
    f.internalcourtorderid,
    f.courtorderhash,
    f.courtorderhashref,
    f.courtordertype,
    f.startenforceatheight,
    f.stopenforceatheight,
    f.hasunfreezeorder,
    f.hasConfiscationOrder
   FROM ( SELECT fundwithcourtorder.fundid,
            fundwithcourtorder.internalcourtorderid,
            fundwithcourtorder.courtorderhash,
            fundwithcourtorder.courtorderhashref,
            fundwithcourtorder.courtordertype,
            fundwithcourtorder.enforceatheight AS startenforceatheight,
            min(fundwithcourtorder.enforceatheightunfreeze) OVER (PARTITION BY fundwithcourtorder.fundid, fundwithcourtorder.courtorderhashref) AS stopenforceatheight,
            max(fundwithcourtorder.hasunfreezeorder) OVER (PARTITION BY fundwithcourtorder.fundid, fundwithcourtorder.courtorderhashref) AS hasunfreezeorder,
            max(fundwithcourtorder.hasconfiscationorder) OVER (PARTITION BY fundwithcourtorder.fundid, fundwithcourtorder.courtorderhashref) AS hasconfiscationorder
           FROM fundwithcourtorder) f
  WHERE f.courtordertype = 1;