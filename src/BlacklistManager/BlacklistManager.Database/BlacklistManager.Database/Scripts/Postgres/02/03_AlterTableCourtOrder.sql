-- Copyright (c) 2020 Bitcoin Association

ALTER TABLE CourtOrder ADD COLUMN IF NOT EXISTS DestinationAddress VARCHAR(64);

ALTER TABLE CourtOrder ADD COLUMN IF NOT EXISTS DestinationAmount BIGINT;

ALTER TABLE CourtOrder ADD COLUMN IF NOT EXISTS SignedByKey VARCHAR(256);

ALTER TABLE CourtOrder ADD COLUMN IF NOT EXISTS SignedDate TIMESTAMP;

UPDATE CourtOrder
SET SignedByKey = d.publicKey,
    SignedDate = d.signedDate
FROM 
(
  SELECT internalCourtOrderId, publicKey, to_timestamp(payload::json->>'signedDate', 'YYYY-MM-DD"T"HH24:MI:SS"Z"') signedDate 
  FROM
  (
    SELECT internalCourtOrderId, signedcourtorderjson::json->>'publicKey' publicKey , signedcourtorderjson::json->>'payload' payload
    FROM courtorder c 
  ) a
) d
WHERE CourtOrder.internalCourtOrderId = d.internalCourtOrderId;

ALTER TABLE CourtOrder ALTER COLUMN SignedByKey SET NOT NULL;

ALTER TABLE CourtOrder ALTER COLUMN SignedDate SET NOT NULL;