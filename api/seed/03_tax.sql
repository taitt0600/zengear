-- ZenGear seed — VAT classes and rates of the credit method (DEC-143, DEC-147)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

INSERT INTO tax_classes (code, name) VALUES
  ('GOODS', 'Hàng hóa (linh kiện, phụ kiện, PC dựng sẵn)'),
  ('SERVICE', 'Dịch vụ bán riêng (lắp ráp, cài phần mềm)'),
  ('SHIPPING', 'Phí giao hàng')
ON CONFLICT (code) DO NOTHING;

-- 10% from the first invoice (2027-01-01, Vietnam time), open-ended (Luật Thuế GTGT 48/2024/QH15 Điều 9, from secondary sources, DEC-147);
-- a later change is a new row with a later valid_from, entered by an Admin (TAX-01).
INSERT INTO tax_rates (tax_class_id, rate, valid_from)
SELECT c.id, v.rate, timestamptz '2027-01-01 00:00:00+07'
FROM (VALUES ('GOODS', 10.00), ('SERVICE', 10.00), ('SHIPPING', 10.00)) AS v(code, rate)
JOIN tax_classes c ON c.code = v.code
WHERE NOT EXISTS (SELECT 1 FROM tax_rates r WHERE r.tax_class_id = c.id);
