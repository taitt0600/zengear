-- Dev-only fixture of STF-02, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
--   1. Store Manager receives staff.disable, so Lê Thu Hà (STF-01 fixture) can show R-TX-11. The
--      permission sync grants it to Store Manager as a default (owner's decision for STF-02), but only when the key is
--      new to the database at the API start, which comes after the reset; this fixture may insert the key first.
--   2. An ANONYMIZED staff account, for the refusal of R-USR-03 (ANONYMIZED is final): no endpoint anonymizes an
--      account yet (STF-14 will). Fixed id, named anonymizedStaffId in the dev and ci environments; no Firebase
--      account; name, username and email are non-personal placeholders for the values STF-14 will set (R-USR-23;
--      ck_users_email_phone needs an email or a phone); the employee code ZG260008 is kept, as R-USR-23 keeps it until
--      the retention job, and comes after the cast (DEC-146 (2)); a Sales Consultant role (R-SEC-01) that holds no
--      staff permission.

INSERT INTO permissions (key) VALUES ('staff.disable') ON CONFLICT (key) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'Store Manager' AND p.key = 'staff.disable'
ON CONFLICT DO NOTHING;

WITH gone AS (
  INSERT INTO users (id, firebase_uid, name, email, status)
  VALUES ('0199a0f2-0000-7000-8000-000000000001', NULL, 'Đã ẩn danh',
          'anonymized-0199a0f2-0000-7000-8000-000000000001@zengear.test', 'ANONYMIZED')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, must_change_password)
  SELECT id, 'ZG260008', 'anonymized-0199a0f2-0000-7000-8000-000000000001', DATE '2026-04-01', false FROM gone
)
INSERT INTO user_roles (user_id, role_id)
SELECT gone.id, roles.id FROM gone, roles WHERE roles.name = 'Sales Consultant';

-- Past the cast's codes (R-USR-24), as in STF-01.sql.
INSERT INTO employee_code_sequences (year, last_value) VALUES (2026, 8)
ON CONFLICT (year) DO UPDATE SET last_value = GREATEST(employee_code_sequences.last_value, 8), updated_at = now();
