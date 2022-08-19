-- Copyright (c) 2020 Bitcoin Association

ALTER TABLE Fund ADD COLUMN IF NOT EXISTS Value BIGINT;

ALTER TABLE Fund ADD COLUMN IF NOT EXISTS HasConfiscationOrder BOOL NOT NULL DEFAULT false;

ALTER TABLE ConsensusActivationLegalEntityEndpoint ADD COLUMN IF NOT EXISTS RetryCount INT;

ALTER TABLE ConsensusActivationLegalEntityEndpoint ADD COLUMN IF NOT EXISTS LastErrorAt TIMESTAMP;

ALTER TABLE CourtOrderAcceptance ADD COLUMN IF NOT EXISTS RetryCount INT;

ALTER TABLE CourtOrderAcceptance ADD COLUMN IF NOT EXISTS LastErrorAt TIMESTAMP;

ALTER TABLE ConfiscationTransaction ADD COLUMN IF NOT EXISTS rewardTransaction BOOL NOT NULL;

ALTER TABLE TrustList ADD COLUMN IF NOT EXISTS ReplacedBy VARCHAR(256);

ALTER TABLE TrustList ADD PRIMARY KEY (PublicKey);

ALTER TABLE TrustList ADD CONSTRAINT fk_trustlist_replacedby FOREIGN KEY (ReplacedBy) REFERENCES TrustList (PublicKey);

ALTER TABLE FundEnforceAtHeight ADD COLUMN IF NOT EXISTS HasConfiscationOrder BOOL NOT NULL DEFAULT FALSE;
