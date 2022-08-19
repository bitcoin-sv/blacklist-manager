-- Copyright (c) 2020 Bitcoin Association

ALTER TABLE LegalEntityEndpoint ADD COLUMN IF NOT EXISTS ProcessedOrdersCount INT DEFAULT 0;

ALTER TABLE LegalEntityEndpoint ADD COLUMN IF NOT EXISTS FailureCount INT DEFAULT 0;
