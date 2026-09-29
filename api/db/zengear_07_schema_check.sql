-- ZenGear — schema check (run after the phase files on database zengear)
-- Lists objects that the ERD does not show (partial and expression indexes, exclusion constraint, triggers, extensions) and are missing.
-- Groups are labeled by the phase that creates the object, an empty result means the schema is complete for the full install.

SELECT expected AS missing_object
FROM (VALUES
  -- Phase 1
  ('ix_product_images_product_id_primary'), ('ix_product_skus_product_id_default'), ('ix_shifts_staff_id_open'),
  ('ix_product_skus_product_id_attributes'), ('ix_order_payments_order_id_pending'), ('ix_e_invoices_order_id_sale_active'),
  ('ix_stock_count_lines_stock_count_id_sku_id_serial_number'), ('ix_products_attributes'), ('ix_product_skus_attributes'),
  ('ix_product_skus_product_id_price_active'), ('ix_notifications_next_attempt_at_pending'), ('ix_cash_accounts_type_default'),
  ('ix_cash_vouchers_source_type_source_id_active'), ('ex_tax_rates_no_overlap'), ('trg_user_roles_min_one'),
  ('trg_user_roles_last_admin'),
  -- Phase 2
  ('ix_addresses_user_id_default'), ('ix_shipping_fee_rules_method_province_min_value'), ('ix_products_name_trgm'),
  -- Phase 3
  ('ix_voucher_usages_voucher_id_customer_id_active'), ('ix_voucher_usages_voucher_code_id_active')
) AS e(expected)
WHERE NOT EXISTS (SELECT 1 FROM pg_indexes    WHERE indexname = e.expected)
  AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname   = e.expected)
  AND NOT EXISTS (SELECT 1 FROM pg_trigger    WHERE tgname    = e.expected)
-- Phase 1
UNION ALL
SELECT 'extension btree_gist' WHERE NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'btree_gist')
UNION ALL
SELECT 'fillfactor product_skus' WHERE NOT EXISTS (SELECT 1 FROM pg_class WHERE relname = 'product_skus' AND 'fillfactor=90' = ANY (reloptions))
UNION ALL
SELECT 'fillfactor stock_balances' WHERE NOT EXISTS (SELECT 1 FROM pg_class WHERE relname = 'stock_balances' AND 'fillfactor=90' = ANY (reloptions))
-- Phase 2
UNION ALL
SELECT 'extension unaccent' WHERE NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'unaccent')
UNION ALL
SELECT 'extension pg_trgm' WHERE NOT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'pg_trgm')
UNION ALL
SELECT 'function f_unaccent' WHERE NOT EXISTS (SELECT 1 FROM pg_proc WHERE proname = 'f_unaccent');
