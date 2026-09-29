-- ZenGear seed — roles (actors A01–A11 of the blueprint)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

INSERT INTO roles (name, scoping_type, is_system_role) VALUES
  ('Admin', 'NONE', true),
  ('Store Manager', 'NONE', true),
  ('Sales Consultant', 'NONE', true),
  ('Technical Support', 'NONE', true),
  ('Cashier', 'NONE', true),
  ('Warehouse Keeper', 'NONE', true),
  ('Purchasing', 'NONE', true),
  ('Accountant', 'NONE', true),
  ('Customer', 'SELF', true)
ON CONFLICT (name) DO NOTHING;

-- max_discount_percent (PRM-04) is set per role by the owner in the admin UI; NULL = cannot discount.
-- Permissions (resource.action) are defined in code and synchronized by the application at startup.
