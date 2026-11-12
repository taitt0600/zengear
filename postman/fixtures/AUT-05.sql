-- Dev-only fixture of AUT-05, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
-- The refusals of R-SEC-12 need a signed-in IT Support (DEC-128) with no pending password change: an account created
-- through POST /api/v1/admin/staff keeps must_change_password until its owner changes the password in the web
-- (STF-03), which the collection cannot do. Replaced when a folder can sign in an account it created itself.
--
-- This fixture writes no row of role_manageable_roles and grants no permission: api/seed/01_roles.sql, which runs
-- before it, has written the default manageable sets already (R-SEC-12, DEC-129). The AUT-05 folder grants IT Support
-- its staff permissions through PUT /api/v1/admin/roles/{id}: the sync cannot, since STF-01.sql and STF-02.sql insert
-- staff.create and staff.disable before it runs.

WITH it AS (
  INSERT INTO users (firebase_uid, name, email, status)
  VALUES (:'uid_itsupport', 'Hỗ trợ IT (fixture)', 'itsupport.fixture@zengear.test', 'ACTIVE')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, joined_date, must_change_password)
  SELECT id, 'FX0005', DATE '2026-10-01', false FROM it
)
INSERT INTO user_roles (user_id, role_id)
SELECT it.id, roles.id FROM it, roles WHERE roles.name = 'IT Support';
