-- Copyright (c) 2020 Bitcoin Association

CREATE TABLE IF NOT EXISTS ConfiscationTransaction (
	confiscationTransactionId BIGSERIAL,
	internalCourtOrderId BIGINT REFERENCES CourtOrder,
	transactionId VARCHAR(64) NOT NULL,
	transactionBody BYTEA NOT NULL,
	enforceAtHeight INT,
	submittedAtHeight INT,
	lastErrorAtHeight INT,
	lastErrorCode INT,
	lastError TEXT,

	PRIMARY KEY (confiscationTransactionId)
);

CREATE TABLE IF NOT EXISTS NodeWhiteList(
	confiscationTransactionId BIGINT REFERENCES ConfiscationTransaction,
	nodeId INT REFERENCES Node,
	submittedAt TIMESTAMP,

	PRIMARY KEY (confiscationTransactionId, nodeId)
);

CREATE TABLE IF NOT EXISTS CourtOrderValidationError (
	legalEntityEndpointId INT REFERENCES LegalEntityEndpoint,
	courtOrderhash VARCHAR(256),
	errorData TEXT,
	submittedAt TIMESTAMP,
	lastError TEXT,
	lastErrorAt TIMESTAMP,
	retryCount INT,
	successfullyProcessedAt TIMESTAMP,

	PRIMARY KEY (legalEntityEndpointId, courtOrderhash)
);
	
