-- Dev-only fixture of AUT-05, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
-- The refusals of R-SEC-12 need a signed-in IT Support (DEC-128) with no pending password change: an account created
-- through POST /api/v1/admin/staff keeps must_change_password until its owner changes the password in the web
-- (STF-03), which the collection cannot do. Replaced when a folder can sign in an account it created itself.
-- Phạm Quốc Bảo of the sample cast (DEC-146 (2)) is the IT Support, with the identifiers R-USR-24 gives him as the
-- third account of 2026 (ZG260003).
--
-- This fixture writes no row of role_manageable_roles and grants no permission: api/seed/01_roles.sql, which runs
-- before it, has written the default manageable sets already (R-SEC-12, DEC-129). The AUT-05 folder grants IT Support
-- its staff permissions through PUT /api/v1/admin/roles/{id}: the sync cannot, since STF-01.sql and STF-02.sql insert
-- staff.create and staff.disable before it runs.

WITH it AS (
  INSERT INTO users (firebase_uid, name, email, status)
  VALUES (:'uid_itsupport', 'Phạm Quốc Bảo', 'baopqzg260003@zengear.test', 'ACTIVE')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, work_phone, must_change_password)
  SELECT id, 'ZG260003', 'BaoPQZG260003', DATE '2026-02-02', '0900000003', false FROM it
)
INSERT INTO user_roles (user_id, role_id)
SELECT it.id, roles.id FROM it, roles WHERE roles.name = 'IT Support';

-- Past the cast's codes (R-USR-24), as in STF-01.sql.
INSERT INTO employee_code_sequences (year, last_value) VALUES (2026, 3)
ON CONFLICT (year) DO UPDATE SET last_value = GREATEST(employee_code_sequences.last_value, 3), updated_at = now();
