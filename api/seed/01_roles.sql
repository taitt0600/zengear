-- ZenGear seed — roles (actors A01–A11 and A17 of the blueprint)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.
--
-- System roles (is_system_role = true): Admin and Customer. Their permissions are exactly the ones
-- defined in code, overwritten at every startup, and never edited through the API (DEC-095).
-- Default roles (is_system_role = false): the 8 staff roles (IT Support added by DEC-128). They receive their default permissions
-- from code only when a permission key is new; the Admin edits them afterwards (AUT-05).
-- The permission sync finds roles by name: default roles must not be renamed or deleted.

-- Fixed ids (DEC-100): the Postman collection (postman/environments) and the dev-only fixtures name roles by id.
-- Never change them: a database seeded earlier keeps the ids it has.
INSERT INTO roles (id, name, scoping_type, is_system_role) VALUES
  ('0199a000-0000-7000-8000-000000000001', 'Admin', 'NONE', true),
  ('0199a000-0000-7000-8000-000000000002', 'Store Manager', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000003', 'Sales Consultant', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000004', 'Technical Support', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000005', 'Cashier', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000006', 'Warehouse Keeper', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000007', 'Purchasing', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000008', 'Accountant', 'NONE', false),
  ('0199a000-0000-7000-8000-000000000009', 'Customer', 'SELF', true),
  ('0199a000-0000-7000-8000-00000000000a', 'IT Support', 'NONE', false)
ON CONFLICT (name) DO NOTHING;

-- Default manageable sets (Phạm vi quản lý, R-SEC-12, DEC-129): the roles whose staff accounts a holder may create,
-- disable, restore and approve. Store Manager manages the 8 default roles; IT Support manages the 6 operational roles
-- (not Store Manager, not IT Support). Written only while role_manageable_roles has no row: running the seed again
-- adds nothing while any pair is left, but writes every default pair again once the Admin has removed them all (the
-- owner kept this, DEC-129). The permission sync never touches this table; a default pair for a running database (a
-- default role added later) goes in a data migration.
INSERT INTO role_manageable_roles (role_id, managed_role_id)
SELECT pairs.role_id, pairs.managed_role_id
FROM (VALUES
  -- Store Manager
  ('0199a000-0000-7000-8000-000000000002'::uuid, '0199a000-0000-7000-8000-000000000002'::uuid),
  ('0199a000-0000-7000-8000-000000000002', '0199a000-0000-7000-8000-000000000003'),
  ('0199a000-0000-7000-8000-000000000002', '0199a000-0000-7000-8000-000000000004'),
  ('0199a000-0000-7000-8000-000000000002', '0199a000-0000-7000-8000-000000000005'),
  ('0199a000-0000-7000-8000-000000000002', '0199a000-0000-7000-8000-000000000006'),
  ('0199a000-0000-7000-8000-000000000002', '0199a000-0000-7000-8000-000000000007'),
  ('0199a000-0000-7000-8000-000000000002', '0199a000-0000-7000-8000-000000000008'),
  ('0199a000-0000-7000-8000-000000000002', '0199a000-0000-7000-8000-00000000000a'),
  -- IT Support
  ('0199a000-0000-7000-8000-00000000000a', '0199a000-0000-7000-8000-000000000003'),
  ('0199a000-0000-7000-8000-00000000000a', '0199a000-0000-7000-8000-000000000004'),
  ('0199a000-0000-7000-8000-00000000000a', '0199a000-0000-7000-8000-000000000005'),
  ('0199a000-0000-7000-8000-00000000000a', '0199a000-0000-7000-8000-000000000006'),
  ('0199a000-0000-7000-8000-00000000000a', '0199a000-0000-7000-8000-000000000007'),
  ('0199a000-0000-7000-8000-00000000000a', '0199a000-0000-7000-8000-000000000008')
) AS pairs (role_id, managed_role_id)
WHERE NOT EXISTS (SELECT 1 FROM role_manageable_roles);

-- max_discount_percent (PRM-04) is set per role by the owner in the admin UI; NULL = cannot discount.
-- Permissions (resource.action) are defined in code and synchronized by the application at startup;
-- the sync does nothing until all 10 roles exist, so restart the API once after the first seed.
