-- Dev-only fixture of STF-01, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
-- The refusal of R-TX-11 needs a caller that is not Admin, holds staff.create and has no pending password change.
-- AUT-05 lets the Admin grant staff.create through PUT /api/v1/admin/roles/{id}, but the collection runs this state
-- before any request, so SQL still sets it (DEC-101 (5): drop this fixture once a folder creates the state itself).
--   Store Manager receives staff.create; Lê Thu Hà of the sample cast (DEC-146 (2)) is the Store Manager, with the
--   identifiers R-USR-24 gives her as the second account of 2026 (ZG260002).
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
  VALUES (:'uid_manager', 'Lê Thu Hà', 'haltzg260002@zengear.test', 'ACTIVE')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, work_phone, must_change_password)
  SELECT id, 'ZG260002', 'HaLTZG260002', DATE '2026-01-12', '0900000002', false FROM manager
)
INSERT INTO user_roles (user_id, role_id)
SELECT manager.id, roles.id FROM manager, roles WHERE roles.name = 'Store Manager';

-- The cast's codes are written above, so the counter of 2026 (R-USR-24) is raised past them: an account the API
-- creates next takes the following number, never one of the cast.
INSERT INTO employee_code_sequences (year, last_value) VALUES (2026, 2)
ON CONFLICT (year) DO UPDATE SET last_value = GREATEST(employee_code_sequences.last_value, 2), updated_at = now();
