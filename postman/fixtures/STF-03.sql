-- Dev-only fixture of STF-03, run by tools/dev_reset.ps1 after the seed and bootstrap-admin, never in production.
-- The refusal of the password change for a customer account (409 staff-account-required) needs a registered customer;
-- no endpoint creates one yet (AUT-02, AUT-03, AUT-08 will), so only SQL reaches that state.
--   customer.fixture@zengear.test: an ACTIVE user with the Customer role and no staff profile. Its customer profile
--   is left out: that table is not migrated yet (AUT-02 adds it).

WITH customer AS (
  INSERT INTO users (firebase_uid, name, email, status)
  VALUES (:'uid_customer', 'Khách hàng (fixture)', 'customer.fixture@zengear.test', 'ACTIVE')
  RETURNING id
)
INSERT INTO user_roles (user_id, role_id)
SELECT customer.id, roles.id FROM customer, roles WHERE roles.name = 'Customer';
