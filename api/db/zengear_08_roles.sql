-- ZenGear — database roles (run after the schema migrations, as a role allowed to create roles)
-- Group roles only; login users are created by operations and granted one of these roles.

DO $$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'zengear_app') THEN CREATE ROLE zengear_app NOLOGIN; END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'zengear_jobs') THEN CREATE ROLE zengear_jobs NOLOGIN; END IF;
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'zengear_readonly') THEN CREATE ROLE zengear_readonly NOLOGIN; END IF;
END $$;

REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO zengear_app, zengear_jobs, zengear_readonly;

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO zengear_app, zengear_jobs;
REVOKE UPDATE, DELETE ON audit_logs, stock_ledger_entries, status_history, supplier_payments FROM zengear_app, zengear_jobs;
REVOKE DELETE ON notifications FROM zengear_app;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO zengear_readonly;

REVOKE EXECUTE ON FUNCTION fn_audit_erase_values(varchar, jsonb) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION fn_audit_erase_values(varchar, jsonb) TO zengear_app;

-- tables created by later migrations (run by the schema owner) get the same grants
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO zengear_app, zengear_jobs;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT ON TABLES TO zengear_readonly;
