-- Copyright (c) 2020 Bitcoin Association

CREATE TABLE CourtOrder (
	internalCourtOrderId			BIGSERIAL NOT NULL,
	courtOrderType					INT NOT NULL,
	courtOrderId					VARCHAR(256) NOT NULL,
	freezeCourtOrderId     			VARCHAR(256),
	freezeCourtOrderHash   			VARCHAR(256),
	freezeInternalCourtOrderId		INT,
	validFrom						TIMESTAMP,
	validTo							TIMESTAMP,
	courtOrderHash          		VARCHAR(256)  NOT NULL,	
	courtOrderStatus				INT NOT NULL,
	
	enforceAtHeight					INT,
	signedCourtOrderJSON    		TEXT,
	
	PRIMARY KEY (internalCourtOrderId),
	FOREIGN KEY (freezeInternalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId)
);

ALTER TABLE CourtOrder ADD CONSTRAINT courtorder_courtOrderHash UNIQUE (courtOrderHash);

CREATE TABLE CourtOrderState  (
	courtOrderStateId       		SERIAL NOT NULL,
	internalCourtOrderId    		BIGINT NOT NULL,
	courtOrderStatus        		INT NOT NULL,
	changedAt               		TIMESTAMP,

	PRIMARY KEY (courtOrderStateId),
	FOREIGN KEY (internalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId)
);

CREATE TABLE LegalEntityEndpoint (
	legalEntityEndpointId			SERIAL NOT NULL,
	
	baseUrl							VARCHAR(256) NOT NULL,
	apiKey							VARCHAR(256) NOT NULL,
	createdAt						TIMESTAMP NOT NULL,
	validUntil						TIMESTAMP,
	lastContactedAt					TIMESTAMP,
	lastError               		TEXT,
	lastErrorAt             		TIMESTAMP,
	
	courtOrderSyncToken				VARCHAR(256),
	courtOrderAcceptanceSyncToken	VARCHAR(256),
	courtOrderDeltaLink				VARCHAR(256),
	
	PRIMARY KEY (legalEntityEndpointId)
);

ALTER TABLE LegalEntityEndpoint ADD CONSTRAINT legalEntityEndpoint_baseUrl UNIQUE (baseUrl);

CREATE TABLE CourtOrderAcceptance (
	courtOrderAcceptanceId			BIGSERIAL NOT NULL,
	internalCourtOrderId			BIGINT NOT NULL,
	legalEntityEndpointId			INT,
	signedCourtOrderAcceptanceJSON  TEXT,
		
	courtOrderReceivedAt			TIMESTAMP,
	courtOrderAcceptanceSubmittedAt	TIMESTAMP,	
	lastError						TEXT,
		
	PRIMARY KEY (courtOrderAcceptanceId),
	FOREIGN KEY (internalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId),
	FOREIGN KEY (legalEntityEndpointId) REFERENCES LegalEntityEndpoint (legalEntityEndpointId)	
);

CREATE TABLE ConsensusActivation (
	consensusActivationId			BIGSERIAL NOT NULL,
	internalCourtOrderId			BIGINT NOT NULL,
		
	signedConsensusActivationJSON	TEXT,
	consensusActivationHash			VARCHAR(256),
	
	PRIMARY KEY (consensusActivationId),
	FOREIGN KEY (internalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId)	
);

ALTER TABLE ConsensusActivation ADD CONSTRAINT consensusActivation_consensusActivationHash UNIQUE (consensusActivationHash);

CREATE TABLE ConsensusActivationLegalEntityEndpoint (
	consensusActivationId			BIGINT NOT NULL,
	legalEntityEndpointId			INT NOT NULL,
	
	receivedAt						TIMESTAMP,
	lastErrorAt						TIMESTAMP,
	lastError						TEXT,
	
	PRIMARY KEY (consensusActivationId, legalEntityEndpointId),
	FOREIGN KEY (consensusActivationId) REFERENCES ConsensusActivation (consensusActivationId),
	FOREIGN KEY (legalEntityEndpointId) REFERENCES LegalEntityEndpoint (legalEntityEndpointId)
);

CREATE TABLE Fund (
	fundId							BIGSERIAL NOT NULL,
	txId							VARCHAR(100),
	vOut							BIGINT,
	fundStatus						INT NOT NULL,
		
	PRIMARY KEY (fundId)
);

ALTER TABLE fund ADD CONSTRAINT fund_txout UNIQUE (txid,vout);

CREATE TABLE FundEnforceAtHeight (
		fundId						BIGINT NOT NULL,
		internalCourtOrderId  		BIGINT NOT NULL,
		startEnforceAtHeight		INT NOT NULL,
		stopEnforceAtHeight			INT NOT NULL,
		hasUnfreezeOrder			BOOL NOT NULL,

		PRIMARY KEY (fundId, internalCourtOrderId),
		FOREIGN KEY (fundId) REFERENCES Fund (fundId),
		FOREIGN KEY (internalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId)
);

CREATE TABLE CourtOrderFund (
	fundId							BIGINT NOT NULL,
	internalCourtOrderId			BIGINT NOT NULL,

	PRIMARY KEY (fundId, internalCourtOrderId),
	FOREIGN KEY (fundId) REFERENCES Fund (fundId),
	FOREIGN KEY (internalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId)
);

CREATE TABLE FundState (
	fundStateId             		BIGSERIAL NOT NULL,
	fundId                  		BIGINT NOT NULL,
	fundStateIdPrevious				BIGINT NULL,
	fundStatus              		INT NOT NULL,
	fundStatusPrevious				INT NULL,
	changedAt               		TIMESTAMP,
	--internalCourtOrderId			BIGINT NOT NULL,

	PRIMARY KEY (fundStateId),
	FOREIGN KEY (fundId) REFERENCES Fund (fundId),
	FOREIGN KEY (fundStateIdPrevious) REFERENCES FundState (fundStateId)
	--,FOREIGN KEY (internalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId)
);
CREATE INDEX fundstate_fundid_idx ON public.fundstate USING btree (fundid);

CREATE TABLE FundStateEnforceAtHeight (
		fundStateId					BIGINT NOT NULL,
		internalCourtOrderId  		BIGINT NOT NULL,
		startEnforceAtHeight		INT NOT NULL,
		stopEnforceAtHeight			INT NOT NULL,
		hasUnfreezeOrder			BOOLEAN NOT NULL,

		PRIMARY KEY (fundStateId, internalCourtOrderId),
		FOREIGN KEY (fundStateId) REFERENCES FundState (fundStateId),
		FOREIGN KEY (internalCourtOrderId) REFERENCES CourtOrder (internalCourtOrderId)
);


CREATE TABLE Node (
	nodeId                  		SERIAL NOT NULL,
	host							VARCHAR(50) NOT NULL,
	port							INT NOT NULL,
	username						VARCHAR(50) NOT NULL,
	password						VARCHAR(50) NOT NULL,
	Remarks	        				VARCHAR(1024),
	nodeStatus              		INT NOT NULL,
	lastError               		TEXT,
	lastErrorAt             		TIMESTAMP,

	PRIMARY KEY (nodeId)    
);

ALTER TABLE Node ADD CONSTRAINT node_hostAndPort UNIQUE (host,port);

CREATE TABLE FundStateNode (
	fundStateId						BIGINT NOT NULL,
	nodeId							INT NOT NULL,
	propagatedAt            		TIMESTAMP,

	PRIMARY KEY (fundStateId, nodeId),
	FOREIGN KEY (fundStateId) REFERENCES FundState (fundStateId),
	FOREIGN KEY (nodeId) REFERENCES Node (nodeId)
);

CREATE TABLE TrustList (
	publicKey       				VARCHAR(256) NOT NULL,
	trusted         				BOOLEAN NOT NULL,
	createdAt						TIMESTAMP NOT NULL,
	updateAt						TIMESTAMP,
	Remarks	        				VARCHAR(1024)	
);

ALTER TABLE TrustList ADD CONSTRAINT trustList_publicKey UNIQUE (publicKey);


CREATE TABLE ConfigurationParam (
	paramKey						VARCHAR(50),	
	paramValue						VARCHAR(1024), 

	PRIMARY KEY (paramKey)
);

CREATE TABLE DelegatedKey (
	delegatedKeyId					SERIAL NOT NULL,
	privateKey						BYTEA NOT NULL,
	publicKey						VARCHAR(256) NOT NULL,
	delegationRequired				BOOLEAN NOT NULL,
	isActive						BOOLEAN NOT NULL,
	activatedAt						TIMESTAMP,
	createdAt						TIMESTAMP NOT NULL,

	PRIMARY KEY (delegatedKeyId)
);
ALTER TABLE DelegatedKey ADD CONSTRAINT DelegatedKey_privateKey UNIQUE (privateKey);

CREATE TABLE DelegatingKey (
	delegatingKeyId					SERIAL NOT NULL,
    
	publicKeyAddress				VARCHAR(256),
	publicKey						VARCHAR(256),
	dataToSign						TEXT,
	signedDelegatedKeyJSON			TEXT,
	createdAt						TIMESTAMP NOT NULL,
	validatedAt						TIMESTAMP,

	delegatedKeyId					INT,

	PRIMARY KEY (delegatingKeyId),
	FOREIGN KEY (delegatedKeyId) REFERENCES DelegatedKey (delegatedKeyId)
);
ALTER TABLE DelegatingKey ADD CONSTRAINT DelegatingKey_publicKeyAddress UNIQUE (publicKey, publicKeyAddress);

--- TRIGGERS ---

CREATE OR REPLACE FUNCTION log_courtorder_status() RETURNS TRIGGER AS $log_cos$
		BEGIN
		IF (TG_OP = 'INSERT' OR (TG_OP = 'UPDATE' AND NEW.courtOrderStatus <> OLD.courtOrderStatus)) THEN
					INSERT INTO courtOrderState (internalCourtOrderId, courtOrderStatus, changedAt) VALUES (NEW.internalCourtOrderId, NEW.courtOrderStatus, now() at time zone 'utc');
		END IF;
				RETURN NULL; -- result is ignored since this is an AFTER trigger
		END;
$log_cos$ LANGUAGE plpgsql;

CREATE TRIGGER log_courtorder_status_change
		AFTER INSERT OR UPDATE ON courtOrder
		FOR EACH ROW EXECUTE PROCEDURE log_courtorder_status();
	
--- VIEWS ---

CREATE VIEW fundWithCourtOrder AS  
  SELECT
    f.fundid,
    case when co.courtOrderType=1 /*@freeze*/ then co.internalCourtOrderId else cor.internalCourtOrderId end as internalCourtOrderId,
    co.courtOrderHash as courtOrderHash, 
    case when co.courtOrderType=1 /*@freeze*/ then co.courtOrderHash else co.freezeCourtOrderHash end as courtOrderHashRef,
    co.courtOrderType, 
    case when co.courtOrderType=1 /*@freeze*/ then co.enforceAtHeight else cor.enforceAtHeight end as enforceAtHeight,
    case when co.courtOrderType=2 /*@unfreeze*/ then co.enforceAtHeight else null end as enforceAtHeightUnfreeze,
    case when co.courtOrderType=2 /*@unfreeze*/ then 1 else 0 end as hasUnfreezeOrder
  FROM 
    fund f
    INNER JOIN courtOrderFund cof ON cof.fundid=f.fundid
    INNER JOIN courtOrder co ON cof.internalCourtOrderId=co.internalCourtOrderId
    LEFT JOIN courtOrder cor ON cor.courtOrderHash=co.freezeCourtOrderHash
  WHERE
    co.courtOrderStatus <> 199 /*@imported*/ AND coalesce(cor.courtOrderStatus,0) <> 199 /*@imported*/;

CREATE VIEW fundWithCourtOrderPivot AS 
  SELECT 
    *
  FROM
    (SELECT 
      fundId, internalCourtOrderId, 
			courtOrderHash, courtOrderHashRef, 
			courtOrderType, 
			enforceAtHeight as startEnforceAtHeight,
      MIN(enforceAtHeightUnfreeze) OVER (PARTITION BY fundId, courtOrderHashRef) as stopEnforceAtHeight,
      MAX(hasUnfreezeOrder) OVER (PARTITION BY fundId, courtOrderHashRef) as hasUnfreezeOrder
    FROM
      fundWithCourtOrder
     ) f
  WHERE
    courtOrderType=1;	/*@freeze*/

/*
DROP VIEW fundWithCourtOrderPivot;
DROP VIEW fundWithCourtOrder;

DROP TABLE ConsensusActivationLegalEntityEndpoint;
DROP TABLE CourtOrderAcceptance;
DROP TABLE ConsensusActivation;
DROP TABLE LegalEntityEndpoint;
DROP TABLE FundEnforceAtHeight;
DROP TABLE FundStateEnforceAtHeight;
DROP TABLE FundStateNode;
DROP TABLE FundState;
DROP TABLE CourtOrderFund;
DROP TABLE Fund;
DROP TABLE CourtOrderState;
DROP TABLE CourtOrder;
DROP TABLE Node;
DROP TABLE TrustList;
DROP TABLE ConfigurationParam;
DROP TABLE DelegatingKey;
DROP TABLE DelegatedKey;
*/
    
/* Just for testing API To local Notary tool
insert into legalentityendpoint (apikey, baseurl, createdat)
values ('71466A87-CBBA-4FC8-818F-D9F78AC40E43', 'http://localhost:54607/NotaryTool.Web/api/v1/CourtOrder', '2020-06-05 00:00:00.000');
*/
/*
insert into node (host, port, username, password, nodestatus)
values('bsvmain', 8322, 'user1234', '8239476543245', 600);
*/

 