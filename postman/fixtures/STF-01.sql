-- Dev-only fixture of STF-01, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
-- The refusal of R-TX-11 needs a caller that is not Admin, holds staff.create and has no pending password change;
-- until AUT-05 (role permissions) exists, only SQL reaches that state.
--   Store Manager receives staff.create; the fixture account manager.fixture@zengear.test is a Store Manager.
-- The permission key may not exist yet: the API creates the catalog when it starts (DEC-095), which happens after
-- the reset. A key inserted here is not "new" to that sync, which then only sets its description; the Admin role
-- receives it anyway, since system roles get their whole catalog set at every start.

INSERT INTO permissions (key) VALUES ('staff.create') ON CONFLICT (key) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'Store Manager' AND p.key = 'staff.create'
ON CONFLICT DO NOTHING;

WITH manager AS (
  INSERT INTO users (firebase_uid, name, email, status)
  VALUES (:'uid_manager', 'Quản lý (fixture)', 'manager.fixture@zengear.test', 'ACTIVE')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, joined_date, must_change_password)
  SELECT id, 'FX0001', DATE '2026-10-01', false FROM manager
)
INSERT INTO user_roles (user_id, role_id)
SELECT manager.id, roles.id FROM manager, roles WHERE roles.name = 'Store Manager';
