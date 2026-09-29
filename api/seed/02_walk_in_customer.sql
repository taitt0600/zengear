-- ZenGear seed — system account "Khách lẻ" for counter customers without phone or email (R-USR-08)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

WITH u AS (
  INSERT INTO users (name, email, status) VALUES ('Khách lẻ', 'walk-in@zengear.local', 'ACTIVE')
  ON CONFLICT (email) DO NOTHING
  RETURNING id)
, p AS (INSERT INTO customer_profiles (user_id) SELECT id FROM u RETURNING user_id)
INSERT INTO user_roles (user_id, role_id)
SELECT p.user_id, r.id FROM p JOIN roles r ON r.name = 'Customer';
