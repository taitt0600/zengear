-- ZenGear seed — business lines and VAT-on-revenue rates of a household business (DEC-029)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

INSERT INTO tax_classes (code, name) VALUES
  ('GOODS_DISTRIBUTION', 'Phân phối, cung cấp hàng hóa'),
  ('GOODS_RELATED_SERVICE', 'Sản xuất, vận tải, dịch vụ có gắn với hàng hóa'),
  ('SERVICE', 'Dịch vụ, xây dựng không bao thầu nguyên vật liệu')
ON CONFLICT (code) DO NOTHING;

-- valid_from only has to precede the first sale; add a new row with a later valid_from when the law changes.
INSERT INTO tax_rates (tax_class_id, rate, valid_from)
SELECT c.id, v.rate, timestamptz '2026-01-01 00:00:00+07'
FROM (VALUES ('GOODS_DISTRIBUTION', 1.00), ('GOODS_RELATED_SERVICE', 3.00), ('SERVICE', 5.00)) AS v(code, rate)
JOIN tax_classes c ON c.code = v.code
WHERE NOT EXISTS (SELECT 1 FROM tax_rates r WHERE r.tax_class_id = c.id);
