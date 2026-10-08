-- Dev-only fixture of STF-02, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
--   1. Store Manager receives staff.disable, so manager.fixture@zengear.test (STF-01 fixture) can show R-TX-11. The
--      permission sync grants it to Store Manager as a default (owner's decision for STF-02), but only when the key is
--      new to the database at the API start, which comes after the reset; this fixture may insert the key first.
--   2. An ANONYMIZED staff account, for the refusal of R-USR-03 (ANONYMIZED is final): no endpoint anonymizes an
--      account yet (ACC-08 will). Fixed id, named anonymizedStaffId in the dev and ci environments; no Firebase
--      account, a placeholder email (ck_users_email_phone needs an email or a phone), a Sales Consultant role
--      (R-SEC-01) that holds no staff permission.

INSERT INTO permissions (key) VALUES ('staff.disable') ON CONFLICT (key) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'Store Manager' AND p.key = 'staff.disable'
ON CONFLICT DO NOTHING;

WITH gone AS (
  INSERT INTO users (id, firebase_uid, name, email, status)
  VALUES ('0199a0f2-0000-7000-8000-000000000001', NULL, 'Đã ẩn danh (fixture)', 'anonymized.fixture@zengear.test', 'ANONYMIZED')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, joined_date, must_change_password)
  SELECT id, 'FX0002', DATE '2026-10-01', false FROM gone
)
INSERT INTO user_roles (user_id, role_id)
SELECT gone.id, roles.id FROM gone, roles WHERE roles.name = 'Sales Consultant';
