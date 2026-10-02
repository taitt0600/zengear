-- ZenGear seed — roles (actors A01–A11 of the blueprint)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.
--
-- System roles (is_system_role = true): Admin and Customer. Their permissions are exactly the ones
-- defined in code, overwritten at every startup, and never edited through the API (DEC-095).
-- Default roles (is_system_role = false): the 7 staff roles. They receive their default permissions
-- from code only when a permission key is new; the Admin edits them afterwards (AUT-05).
-- The permission sync finds roles by name: default roles must not be renamed or deleted.

-- Fixed ids (DEC-100): the Postman collection (postman/environments) and the dev-only fixtures name roles by id,
-- since no endpoint lists roles. Never change them: a database seeded earlier keeps the ids it has.
INSERT INTO roles (id, name, scoping_type, is_system_role) VALUES
  ('0199a000-0000-7000-8000-000000000001', 'Admin', 'NONE', true),
  ('0199a000-0000-7000-8000-000000000002', 'Store Manager', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000003', 'Sales Consultant', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000004', 'Technical Support', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000005', 'Cashier', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000006', 'Warehouse Keeper', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000007', 'Purchasing', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000008', 'Accountant', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000009', 'Customer', 'SELF', true)
ON CONFLICT (name) DO NOTHING;

-- max_discount_percent (PRM-04) is set per role by the owner in the admin UI; NULL = cannot discount.
-- Permissions (resource.action) are defined in code and synchronized by the application at startup;
-- the sync does nothing until all 9 roles exist, so restart the API once after the first seed.
