-- Dev-only accounts of the sample cast (DEC-146 (2)) that no Postman folder needs as a state, run by
-- tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production. DEC-146 creates the cast in a fixed order,
-- so every environment shows the same people with the same codes (R-USR-24) that the Layouts and docs/ACCEPTANCE.md
-- (Accounts) use; unlike the state fixtures of DEC-101 (5), nothing replaces this file when a folder grows.
--   ZG260004 Nguyễn Văn An, Cashier, ACTIVE (the toast sample of SCR-STF-02).
--   ZG260005 Hoàng Đức Minh, Technical Support, DISABLED since 2026-10-31 (the locked-account sample). His emulator
--   account stays enabled: the API refuses him by status (R-SEC-04), whereas STF-02 also disables the Firebase account.
--   ZG260006 Võ Thị Lan (Cashier) and ZG260007 Bùi Văn Tùng (Warehouse Keeper) wait for approval in the cast, a status
--   STF-09 adds: their numbers are only reserved here, and STF-09's fixtures add them with these codes.
-- No password change is pending, so the owner signs in with the test password of the reset.

WITH an AS (
  INSERT INTO users (firebase_uid, name, email, status)
  VALUES (:'uid_cashier', 'Nguyễn Văn An', 'annvzg260004@zengear.test', 'ACTIVE')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, work_phone, must_change_password)
  SELECT id, 'ZG260004', 'AnNVZG260004', DATE '2026-03-02', '0900000004', false FROM an
)
INSERT INTO user_roles (user_id, role_id)
SELECT an.id, roles.id FROM an, roles WHERE roles.name = 'Cashier';

WITH minh AS (
  INSERT INTO users (firebase_uid, name, email, status)
  VALUES (:'uid_technical', 'Hoàng Đức Minh', 'minhhdzg260005@zengear.test', 'DISABLED')
  RETURNING id
), profile AS (
  INSERT INTO staff_profiles (user_id, employee_code, username, joined_date, left_date, work_phone, must_change_password)
  SELECT id, 'ZG260005', 'MinhHDZG260005', DATE '2026-03-16', DATE '2026-10-31', '0900000005', false FROM minh
)
INSERT INTO user_roles (user_id, role_id)
SELECT minh.id, roles.id FROM minh, roles WHERE roles.name = 'Technical Support';

-- Past the cast's codes, the two reserved ones included (R-USR-24).
INSERT INTO employee_code_sequences (year, last_value) VALUES (2026, 7)
ON CONFLICT (year) DO UPDATE SET last_value = GREATEST(employee_code_sequences.last_value, 7), updated_at = now();
