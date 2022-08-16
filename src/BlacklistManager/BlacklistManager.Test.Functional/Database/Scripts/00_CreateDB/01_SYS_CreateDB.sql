-- Copyright (c) 2020 Bitcoin Association

do $$
declare
  cnt integer;
begin
  SELECT count(*)INTO cnt FROM pg_roles WHERE rolname='frozenfund_test';
  if cnt = 0 then
	CREATE ROLE frozenfund_test LOGIN
	PASSWORD 'frozenfund_test'
	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;
  end if;
end $$;

CREATE DATABASE frozenfund_test
  WITH OWNER = frozenfund_test
  ENCODING = 'UTF8'
  TABLESPACE = pg_default
  CONNECTION LIMIT = -1;
