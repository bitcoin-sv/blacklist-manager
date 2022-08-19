-- Copyright (c) 2020 Bitcoin Association
ALTER TABLE ConsensusActivation ADD COLUMN IF NOT EXISTS EnforceAtHeight INT;

ALTER TABLE ConsensusActivation ADD COLUMN IF NOT EXISTS SignedDate TIMESTAMP;

ALTER TABLE ConsensusActivation ADD COLUMN IF NOT EXISTS SignedByKey VARCHAR(256);

UPDATE ConsensusActivation
SET SignedByKey = d.publicKey,
    SignedDate = d.signedDate,
    EnforceAtHeight = d.enforceAtHeight
FROM 
(
  SELECT consensusActivationid, publicKey, to_timestamp(payload::json->>'signedDate', 'YYYY-MM-DD"T"HH24:MI:SS"Z"') signedDate, (payload::json->>'enforceAtHeight')::INT enforceAtHeight
  FROM
  (
    SELECT consensusActivationid, signedconsensusactivationjson::json->>'publicKey' publicKey , signedconsensusactivationjson::json->>'payload' payload
    FROM ConsensusActivation c 
  ) a
) d
WHERE ConsensusActivation.consensusActivationid = d.consensusActivationid;

ALTER TABLE ConsensusActivation ALTER COLUMN SignedByKey SET NOT NULL;

ALTER TABLE ConsensusActivation ALTER COLUMN SignedDate SET NOT NULL;

ALTER TABLE ConsensusActivation ALTER COLUMN EnforceAtHeight SET NOT NULL;