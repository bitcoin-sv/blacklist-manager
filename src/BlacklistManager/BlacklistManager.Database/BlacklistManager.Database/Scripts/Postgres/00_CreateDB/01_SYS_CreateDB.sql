-- Copyright (c) 2020 Bitcoin Association

do $$
declare
  cnt integer;
begin
  SELECT count(*)INTO cnt FROM pg_roles WHERE rolname='frozenfund';
  if cnt = 0 then
	CREATE ROLE frozenfund LOGIN
	PASSWORD '${FROZENFUND_PASSWORD}'
	NOSUPERUSER INHERIT NOCREATEDB NOCREATEROLE NOREPLICATION;
  end if;
end $$;

CREATE DATABASE frozenfund
  WITH OWNER = frozenfund
  ENCODING = 'UTF8'
  TABLESPACE = pg_default
  CONNECTION LIMIT = -1;
