-- ZenGear seed — cash fund and system categories of the cash book (R-ACT-01, R-ACT-02)
-- Run after the schema migrations, as the schema owner. Re-runnable: existing rows are kept.

INSERT INTO cash_accounts (name, type, is_default) VALUES ('Quỹ tiền mặt', 'CASH', true)
ON CONFLICT (name) DO NOTHING;
-- The default bank account (type BANK, bank_name, account_number, is_default = true) is entered by the owner.

-- affects_business_result = false: money of sales, refunds, suppliers, carriers and transfers is not operating income or expense
INSERT INTO cash_voucher_categories (name, direction, affects_business_result, is_system)
SELECT v.name, v.direction, v.affects, true
FROM (VALUES
  ('Thu tiền bán hàng', 'RECEIPT', false),
  ('Thu tiền đối soát hãng vận chuyển', 'RECEIPT', false),
  ('Nhận hoàn tiền nhà cung cấp', 'RECEIPT', false),
  ('Nhận chuyển quỹ', 'RECEIPT', false),
  ('Số dư đầu kỳ', 'RECEIPT', false),
  ('Thu khác', 'RECEIPT', true),
  ('Hoàn tiền khách hàng', 'PAYMENT', false),
  ('Trả tiền nhà cung cấp', 'PAYMENT', false),
  ('Chuyển quỹ', 'PAYMENT', false),
  ('Phí ngân hàng', 'PAYMENT', true)
) AS v(name, direction, affects)
WHERE NOT EXISTS (SELECT 1 FROM cash_voucher_categories c WHERE c.name = v.name AND c.direction = v.direction);
-- Expense categories (Tiền điện, Tiền thuê mặt bằng…) are added by the owner or accountant.
