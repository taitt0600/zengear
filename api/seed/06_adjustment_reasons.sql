-- ZenGear seed — stock adjustment reasons
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

INSERT INTO adjustment_reasons (key, label) VALUES
  ('DAMAGED', 'Hàng hỏng'),
  ('LOST', 'Mất hàng'),
  ('MISCOUNT', 'Chênh lệch kiểm đếm')
ON CONFLICT (key) DO NOTHING;
