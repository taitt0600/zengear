-- ZenGear seed — stock areas of the store
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

INSERT INTO warehouses (name, type)
SELECT v.name, v.type
FROM (VALUES ('Kho tổng', 'MAIN'), ('Kho bảo hành', 'WARRANTY'), ('Hàng trưng bày', 'DISPLAY'), ('Máy đổi trả', 'OUTLET')) AS v(name, type)
WHERE NOT EXISTS (SELECT 1 FROM warehouses w WHERE w.type = v.type);
