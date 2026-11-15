-- ZenGear seed — departments and positions (STF-15, R-ORG-01, R-ORG-02, DEC-144)
-- Run after the schema migrations, as the schema owner. Re-runnable: a row whose code exists is kept with the Admin's
-- edits; a seeded code the Admin has changed is inserted again as a new row.
-- Both catalogues are data, edited through the API with org.update; no code depends on a code or a name.
-- Permissions (org.update, staff.assignment.update) are defined in code and synchronized at startup (DEC-095), not here.

-- Departments: Ban điều hành is the root, the 5 departments sit under it. A store is a business location, not a
-- department (DEC-143).
INSERT INTO departments (code, name) VALUES
  ('EXEC', 'Ban điều hành')
ON CONFLICT ((lower(trim(code)))) DO NOTHING;

INSERT INTO departments (code, name, parent_id)
SELECT d.code, d.name, root.id
FROM (VALUES
  ('SALES', 'Kinh doanh và Vận hành'),
  ('PROC', 'Mua hàng và Kho vận'),
  ('MKT', 'Marketing và Chăm sóc khách hàng'),
  ('FINHR', 'Tài chính và Nhân sự'),
  ('IT', 'Công nghệ thông tin')
) AS d(code, name)
CROSS JOIN (SELECT id FROM departments WHERE lower(trim(code)) = 'exec') AS root
ON CONFLICT ((lower(trim(code)))) DO NOTHING;

-- Positions: not tied to a department or a role, and they grant no permission (R-ORG-02).
INSERT INTO positions (code, name) VALUES
  ('CEO', 'Giám đốc'),
  ('OPS_LEAD', 'Trưởng vận hành'),
  ('SALES_OPS_MANAGER', 'Trưởng phòng Kinh doanh và Vận hành'),
  ('STORE_MANAGER', 'Cửa hàng trưởng'),
  ('SALES_STAFF', 'Nhân viên tư vấn bán hàng'),
  ('CASHIER', 'Thu ngân'),
  ('PC_TECHNICIAN', 'Kỹ thuật viên lắp ráp PC'),
  ('PURCH_INV_MANAGER', 'Trưởng phòng Mua hàng và Kho vận'),
  ('PURCHASING_STAFF', 'Nhân viên mua hàng'),
  ('WAREHOUSE_STAFF', 'Nhân viên kho'),
  ('DELIVERY_STAFF', 'Nhân viên giao hàng'),
  ('MARKETING_EXEC', 'Chuyên viên marketing'),
  ('CONTENT_CREATOR', 'Nhân viên sáng tạo nội dung'),
  ('CS_STAFF', 'Nhân viên chăm sóc khách hàng'),
  ('WARRANTY_STAFF', 'Nhân viên bảo hành'),
  ('ACCOUNTANT', 'Kế toán'),
  ('HR_ADMIN', 'Nhân viên hành chính nhân sự'),
  ('IT_LEAD', 'Trưởng phòng Công nghệ thông tin'),
  ('BACKEND_DEV', 'Lập trình viên backend'),
  ('FRONTEND_DEV', 'Lập trình viên frontend'),
  ('IT_SUPPORT', 'Nhân viên hỗ trợ CNTT')
ON CONFLICT ((lower(trim(code)))) DO NOTHING;
