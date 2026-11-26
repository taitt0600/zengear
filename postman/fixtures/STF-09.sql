-- Dev-only fixture of STF-09, run by tools/dev_reset.ps1 after the seed, bootstrap-admin and AUT-05.sql (by file
-- name), never in production.
--
-- 1. The two PENDING accounts of the sample cast (DEC-146 (2), issue #326), with the codes STF-01-cast.sql reserved:
--    ZG260006 Võ Thị Lan (Cashier) and ZG260007 Bùi Văn Tùng (Warehouse Keeper), created by Phạm Quốc Bảo, the IT
--    Support, who holds no staff.approve (R-USR-17). PENDING accounts have no Firebase account until approved, so no
--    line of accounts.txt; the Postman folder only reads them and creates its own PENDING accounts to change.
-- 2. The shift lead: the role "Trưởng ca" (fixed id, roleShiftLead in the environments) holding staff.approve and
--    managing the Cashier only, and Ngô Thanh Tâm ZG260009 of the cast holding it, with no pending password change,
--    for the refusals of R-SEC-12 on an approval. An account created through POST /api/v1/admin/staff keeps
--    must_change_password, which the collection cannot clear (as AUT-05.sql explains). The role's staff.read comes from
--    the first requests of the STF-09 folder (PUT /api/v1/admin/roles/{id}): inserting staff.read here would make the
--    key not new to the permission sync of DEC-095, which would then not grant it to the Store Manager and IT Support.
--    Replaced when a folder can sign in an account it created itself.
-- 3. IT Support receives staff.create, as STF-01.sql does for the Store Manager, so the STF-09 folder runs without the
--    AUT-05 folder, which grants it through the API.
-- staff.approve and staff.create may not be in permissions yet (the API creates the catalog when it starts, after the
-- reset); inserting them early loses nothing: only the Admin holds staff.approve by default, and system roles receive
-- the whole catalog at every start.

INSERT INTO permissions (key) VALUES ('staff.create'), ('staff.approve') ON CONFLICT (key) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'IT Support' AND p.key = 'staff.create'
ON CONFLICT DO NOTHING;

INSERT INTO roles (id, name, scoping_type, is_system_role)
VALUES ('0199a000-0000-7000-8000-0000000000f1', 'Trưởng ca', 'NONE', false);

INSERT INTO role_permissions (role_id, permission_id)
SELECT '0199a000-0000-7000-8000-0000000000f1', p.id FROM permissions p WHERE p.key = 'staff.approve';

INSERT INTO role_manageable_roles (role_id, managed_role_id)
SELECT '0199a000-0000-7000-8000-0000000000f1', roles.id FROM roles WHERE roles.name = 'Cashier';

WITH lan AS (
  INSERT INTO users (name, email, status, created_by)
  SELECT 'Võ Thị Lan', 'lanvtzg260006@zengear.test', 'PENDING', bao.id FROM users bao WHERE bao.email = 'baopqzg260003@zengear.test'
  RETURNING id, created_by
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, work_phone, must_change_password, created_by)
  SELECT id, 'ZG260006', 'LanVTZG260006', DATE '2026-11-02', '0900000006', true, created_by FROM lan
)
INSERT INTO user_roles (user_id, role_id)
SELECT lan.id, roles.id FROM lan, roles WHERE roles.name = 'Cashier';

WITH tung AS (
  INSERT INTO users (name, email, status, created_by)
  SELECT 'Bùi Văn Tùng', 'tungbvzg260007@zengear.test', 'PENDING', bao.id FROM users bao WHERE bao.email = 'baopqzg260003@zengear.test'
  RETURNING id, created_by
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, work_phone, must_change_password, created_by)
  SELECT id, 'ZG260007', 'TungBVZG260007', DATE '2026-11-09', '0900000007', true, created_by FROM tung
)
INSERT INTO user_roles (user_id, role_id)
SELECT tung.id, roles.id FROM tung, roles WHERE roles.name = 'Warehouse Keeper';

WITH tam AS (
  INSERT INTO users (firebase_uid, name, email, status)
  VALUES (:'uid_shiftlead', 'Ngô Thanh Tâm', 'tamntzg260009@zengear.test', 'ACTIVE')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, work_phone, must_change_password)
  SELECT id, 'ZG260009', 'TamNTZG260009', DATE '2026-04-01', '0900000009', false FROM tam
)
INSERT INTO user_roles (user_id, role_id)
SELECT tam.id, '0199a000-0000-7000-8000-0000000000f1' FROM tam;

-- Past the cast's codes (R-USR-24): an account created through the API takes ZG260010 or later.
INSERT INTO employee_code_sequences (year, last_value) VALUES (2026, 9)
ON CONFLICT (year) DO UPDATE SET last_value = GREATEST(employee_code_sequences.last_value, 9), updated_at = now();
