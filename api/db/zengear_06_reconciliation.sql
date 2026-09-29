-- ZenGear — reconciliation queries (run nightly on database zengear)
-- Each query returns rows only when stored data disagrees with its source — the job reports and never corrects.
-- Queries are grouped by the phase whose tables they need: run the groups up to the phase deployed.
-- Denormalized values checked here:
--   R-REC-01 (Phase 1): product_skus.sold_count = Σ order_items.quantity of Completed orders — gross, returns are not subtracted
--   R-REC-02 (Phase 3): vouchers.used_count = number of active voucher_usages (revoked_at IS NULL) of the voucher
--   R-REC-03 (Phase 1): stock_balances.quantity = Σ stock_ledger_entries.quantity_change per (sku_id, warehouse_id), checked in both directions (balance without ledger, ledger without balance)
--   R-REC-04 (Phase 1): For SKUs with requires_serial: serial units IN_STOCK + RESERVED + DEFECTIVE per warehouse = stock_balances.quantity of that warehouse
--   R-REC-05 (Phase 1): purchase_order_items.quantity_received = Σ purchase_receipts.quantity per po_item_id, a purchase_return_items.po_item_id belongs to the purchase order of its return
--   R-REC-06 (Phase 1): stock_balances.reserved_quantity of MAIN = Σ order_items.quantity of Pending orders per SKU (excluding PC BUILD lines) + Σ order_item_components.quantity × PC quantity of PC BUILD lines per component SKU
--   R-REC-07 (Phase 1): stock_ledger_entries.balance_after = balance_after of the previous entry of the same (sku_id, warehouse_id) + quantity_change (first entry: quantity_change), the latest entry equals stock_balances.quantity
--   R-REC-08 (Phase 3): purchase_returns.vat_amount = Σ round(quantity × unit_cost × vat_rate / 100) of its lines (rate of the purchase order line), total_amount = Σ quantity × unit_cost + vat_amount
--   R-REC-09 (Phase 1): returns.refund_amount of a Completed return = Σ REFUND payments of the return that are not Failed
--   R-REC-10 (Phase 1): product_skus.average_cost = moving average replayed from stock_ledger_entries in time order: receipts, opening stock, restocks from cancellation, failed delivery and returns, and PC assembly output recalculate it, other movements keep it
--   R-REC-11 (Phase 1): Every stock_ledger_entries.reference_id points to an existing row of the table named by reference_type

-- Phase 1
SELECT s.id, s.sku, s.sold_count,
       COALESCE(SUM(oi.quantity) FILTER (WHERE o.status = 'Completed'), 0) AS expected
FROM product_skus s
LEFT JOIN order_items oi ON oi.sku_id = s.id
LEFT JOIN orders o       ON o.id = oi.order_id
GROUP BY s.id
HAVING s.sold_count <> COALESCE(SUM(oi.quantity) FILTER (WHERE o.status = 'Completed'), 0);

SELECT COALESCE(i.sku_id, m.sku_id) AS sku_id,
       COALESCE(i.warehouse_id, m.warehouse_id) AS warehouse_id,
       COALESCE(i.quantity, 0) AS actual, COALESCE(m.total, 0) AS expected
FROM stock_balances i
FULL JOIN (SELECT sku_id, warehouse_id, SUM(quantity_change) AS total
           FROM stock_ledger_entries GROUP BY sku_id, warehouse_id) m
  ON m.sku_id = i.sku_id AND m.warehouse_id = i.warehouse_id
WHERE COALESCE(i.quantity, 0) <> COALESCE(m.total, 0);

SELECT b.sku_id, b.warehouse_id, b.quantity AS actual,
       COUNT(u.id) FILTER (WHERE u.status IN ('IN_STOCK','RESERVED','DEFECTIVE')) AS expected
FROM stock_balances b
JOIN product_skus s ON s.id = b.sku_id
JOIN products p     ON p.id = s.product_id AND p.requires_serial AND NOT s.is_service
LEFT JOIN serial_units u ON u.sku_id = b.sku_id AND u.warehouse_id = b.warehouse_id
GROUP BY b.sku_id, b.warehouse_id, b.quantity
HAVING b.quantity <> COUNT(u.id) FILTER (WHERE u.status IN ('IN_STOCK','RESERVED','DEFECTIVE'));

SELECT i.id AS po_item_id, i.purchase_order_id, i.sku_id, i.quantity_received AS actual, COALESCE(SUM(r.quantity), 0) AS expected
FROM purchase_order_items i
LEFT JOIN purchase_receipts r ON r.po_item_id = i.id
GROUP BY i.id, i.purchase_order_id, i.sku_id, i.quantity_received
HAVING i.quantity_received <> COALESCE(SUM(r.quantity), 0);

SELECT b.sku_id, b.warehouse_id, b.reserved_quantity, COALESCE(p.pending, 0) AS expected
FROM stock_balances b
JOIN warehouses w ON w.id = b.warehouse_id AND w.type = 'MAIN'
LEFT JOIN (SELECT x.sku_id, SUM(x.qty) AS pending
           FROM (SELECT oi.sku_id, oi.quantity AS qty
                 FROM order_items oi JOIN orders o ON o.id = oi.order_id
                 WHERE o.status = 'Pending' AND oi.pc_fulfillment IS DISTINCT FROM 'BUILD'
                 UNION ALL
                 SELECT c.component_sku_id, c.quantity * oi.quantity
                 FROM order_item_components c
                 JOIN order_items oi ON oi.id = c.order_item_id AND oi.pc_fulfillment = 'BUILD'
                 JOIN orders o ON o.id = oi.order_id
                 WHERE o.status = 'Pending') x
           GROUP BY x.sku_id) p ON p.sku_id = b.sku_id
WHERE b.reserved_quantity <> COALESCE(p.pending, 0);

SELECT p.id AS payment_id, p.amount, SUM(r.amount) AS refunded
FROM order_payments p
JOIN order_payments r ON r.refund_of_payment_id = p.id AND r.status = 'Paid'
GROUP BY p.id, p.amount
HAVING SUM(r.amount) > p.amount;

SELECT p.id AS payment_id, p.type, p.amount, v.amount AS book_amount
FROM order_payments p
LEFT JOIN cash_vouchers v ON v.source_type = 'order_payments' AND v.source_id = p.id AND v.status = 'Active'
WHERE p.status = 'Paid' AND p.method <> 'COD' AND (v.id IS NULL OR v.amount <> p.amount);

SELECT v.id, v.code, v.amount
FROM cash_vouchers v
LEFT JOIN cash_vouchers r ON r.source_type = 'cash_vouchers' AND r.source_id = v.id AND r.status = 'Active'
WHERE v.direction = 'PAYMENT' AND v.counter_account_id IS NOT NULL AND v.status = 'Active'
  AND (r.id IS NULL OR r.amount <> v.amount OR r.cash_account_id <> v.counter_account_id);

SELECT a.id, a.purpose, a.amount
FROM shift_cash_adjustments a
LEFT JOIN cash_vouchers v ON v.source_type = 'shift_cash_adjustments' AND v.source_id = a.id AND v.status = 'Active'
WHERE (a.purpose IN ('EXPENSE', 'OTHER_INCOME', 'BANK_DEPOSIT') AND (v.id IS NULL OR v.amount <> a.amount))
   OR (a.purpose IN ('TO_SAFE', 'FROM_SAFE') AND v.id IS NOT NULL);

SELECT o.id, o.order_code, o.subtotal, COALESCE(SUM(oi.quantity * oi.unit_price), 0) AS expected
FROM orders o
LEFT JOIN order_items oi ON oi.order_id = o.id
GROUP BY o.id, o.order_code, o.subtotal
HAVING o.subtotal <> COALESCE(SUM(oi.quantity * oi.unit_price), 0);

SELECT o.id, o.order_code, o.discount_amount, COALESCE(SUM(oi.discount_allocated), 0) AS allocated
FROM orders o
LEFT JOIN order_items oi ON oi.order_id = o.id
GROUP BY o.id, o.order_code, o.discount_amount
HAVING o.discount_amount <> COALESCE(SUM(oi.discount_allocated), 0);

SELECT i.id, o.order_code, i.amount, o.total_amount
FROM e_invoices i
JOIN orders o ON o.id = i.order_id
WHERE i.invoice_type = 'SALE' AND i.status IN ('Pending', 'Issued') AND i.amount <> o.total_amount;

SELECT r.id, r.return_value, COALESCE(SUM(ri.return_value), 0) AS lines_value
FROM returns r
LEFT JOIN return_items ri ON ri.return_id = r.id
GROUP BY r.id, r.return_value
HAVING r.return_value <> COALESCE(SUM(ri.return_value), 0);

SELECT oi.id AS order_item_id, SUM(ri.quantity) AS returned_qty, SUM(ri.return_value) AS returned_value
FROM order_items oi
JOIN return_items ri ON ri.order_item_id = oi.id
JOIN returns r       ON r.id = ri.return_id AND r.status = 'Completed'
GROUP BY oi.id, oi.quantity, oi.unit_price, oi.discount_allocated
HAVING SUM(ri.quantity) > oi.quantity OR SUM(ri.return_value) > oi.quantity * oi.unit_price - oi.discount_allocated;

SELECT v.id, v.code, v.source_type, v.source_id
FROM cash_vouchers v
WHERE v.source_type IS NOT NULL
  AND NOT CASE v.source_type
        WHEN 'order_payments'         THEN EXISTS (SELECT 1 FROM order_payments x WHERE x.id = v.source_id)
        WHEN 'supplier_payments'      THEN EXISTS (SELECT 1 FROM supplier_payments x WHERE x.id = v.source_id)
        WHEN 'shift_cash_adjustments' THEN EXISTS (SELECT 1 FROM shift_cash_adjustments x WHERE x.id = v.source_id)
        WHEN 'cash_vouchers'          THEN EXISTS (SELECT 1 FROM cash_vouchers x WHERE x.id = v.source_id)
        ELSE true END;

SELECT r.id AS refund_id, r.refund_of_payment_id
FROM order_payments r
JOIN order_payments p ON p.id = r.refund_of_payment_id
WHERE p.type <> 'PAYMENT' OR p.order_id <> r.order_id OR p.method <> r.method;

SELECT oi.id AS order_item_id, oi.serial_unit_id, u.status, u.condition
FROM order_items oi
JOIN orders o       ON o.id = oi.order_id AND o.status = 'Pending'
JOIN serial_units u ON u.id = oi.serial_unit_id
WHERE u.status <> 'RESERVED' OR u.condition = 'NEW' OR u.sku_id <> oi.sku_id;

SELECT a.id, a.code, a.unit_cost, COALESCE(SUM(c.unit_cost * c.quantity), 0) AS expected
FROM pc_assemblies a
LEFT JOIN pc_assembly_components c ON c.assembly_id = a.id
WHERE a.status = 'Completed'
GROUP BY a.id, a.code, a.unit_cost
HAVING a.unit_cost <> COALESCE(SUM(c.unit_cost * c.quantity), 0);

SELECT c.serial_unit_id, u.status, u.parent_serial_unit_id, a.pc_serial_unit_id
FROM pc_assembly_components c
JOIN pc_assemblies a ON a.id = c.assembly_id AND a.status = 'Completed'
JOIN serial_units u  ON u.id = c.serial_unit_id
WHERE u.parent_serial_unit_id IS DISTINCT FROM a.pc_serial_unit_id AND u.status = 'ASSEMBLED';

SELECT e.id, e.sku_id, e.warehouse_id, e.balance_after,
       COALESCE(e.prev_balance, 0) + e.quantity_change AS expected
FROM (SELECT l.*, LAG(l.balance_after) OVER (PARTITION BY l.sku_id, l.warehouse_id ORDER BY l.created_at, l.id) AS prev_balance
      FROM stock_ledger_entries l) e
WHERE e.balance_after <> COALESCE(e.prev_balance, 0) + e.quantity_change;

SELECT b.sku_id, b.warehouse_id, b.quantity AS actual, l.balance_after AS expected
FROM stock_balances b
JOIN LATERAL (SELECT balance_after FROM stock_ledger_entries x
              WHERE x.sku_id = b.sku_id AND x.warehouse_id = b.warehouse_id
              ORDER BY x.created_at DESC, x.id DESC LIMIT 1) l ON true
WHERE b.quantity <> l.balance_after;

SELECT r.id, r.refund_amount, COALESCE(SUM(p.amount), 0) AS refunds
FROM returns r
LEFT JOIN order_payments p ON p.return_id = r.id AND p.type = 'REFUND' AND p.status <> 'Failed'
WHERE r.status = 'Completed' AND r.refund_amount IS NOT NULL
GROUP BY r.id, r.refund_amount
HAVING r.refund_amount <> COALESCE(SUM(p.amount), 0);

WITH RECURSIVE l AS (
  SELECT sku_id, quantity_change AS q, COALESCE(unit_cost, 0) AS c,
         movement_type IN ('OPENING', 'PURCHASE_RECEIPT', 'ORDER_CANCEL', 'DELIVERY_RETURN', 'RETURN_RESTOCK', 'ASSEMBLY_IN') AS recalc,
         row_number() OVER (PARTITION BY sku_id ORDER BY created_at, id) AS rn
  FROM stock_ledger_entries),
r AS (
  SELECT sku_id, rn, q AS qty, CASE WHEN recalc AND q > 0 THEN c ELSE 0 END::numeric AS avg_cost FROM l WHERE rn = 1
  UNION ALL
  SELECT l.sku_id, l.rn, r.qty + l.q,
         CASE WHEN l.recalc AND l.q > 0 AND GREATEST(r.qty, 0) + l.q > 0
              THEN round((GREATEST(r.qty, 0) * r.avg_cost + l.q * l.c) / (GREATEST(r.qty, 0) + l.q))
              ELSE r.avg_cost END
  FROM r JOIN l ON l.sku_id = r.sku_id AND l.rn = r.rn + 1)
SELECT s.id, s.sku, s.average_cost, x.avg_cost AS expected
FROM product_skus s
JOIN (SELECT DISTINCT ON (sku_id) sku_id, avg_cost FROM r ORDER BY sku_id, rn DESC) x ON x.sku_id = s.id
WHERE s.average_cost <> x.avg_cost;

SELECT e.id, e.reference_type, e.reference_id
FROM stock_ledger_entries e
WHERE e.reference_type IS NOT NULL AND NOT CASE e.reference_type
  WHEN 'purchase_receipts' THEN EXISTS (SELECT 1 FROM purchase_receipts x WHERE x.id = e.reference_id)
  WHEN 'orders'            THEN EXISTS (SELECT 1 FROM orders x WHERE x.id = e.reference_id)
  WHEN 'stock_adjustments' THEN EXISTS (SELECT 1 FROM stock_adjustments x WHERE x.id = e.reference_id)
  WHEN 'stock_counts'      THEN EXISTS (SELECT 1 FROM stock_counts x WHERE x.id = e.reference_id)
  WHEN 'return_items'      THEN EXISTS (SELECT 1 FROM return_items x WHERE x.id = e.reference_id)
  WHEN 'warranty_requests' THEN EXISTS (SELECT 1 FROM warranty_requests x WHERE x.id = e.reference_id)
  WHEN 'serial_units'      THEN EXISTS (SELECT 1 FROM serial_units x WHERE x.id = e.reference_id)
  WHEN 'pc_assemblies'     THEN EXISTS (SELECT 1 FROM pc_assemblies x WHERE x.id = e.reference_id)
  ELSE true END;

-- Phase 2
SELECT cs.id, cs.code, cs.total_cod, cs.total_fees,
       COALESCE(SUM(l.cod_amount), 0) AS expected_cod,
       COALESCE(SUM(l.shipping_fee + l.cod_fee + l.return_fee + l.other_fee), 0) AS expected_fees
FROM carrier_settlements cs
LEFT JOIN carrier_settlement_lines l ON l.settlement_id = cs.id
WHERE cs.status IN ('Confirmed', 'Paid')
GROUP BY cs.id
HAVING cs.total_cod  <> COALESCE(SUM(l.cod_amount), 0)
    OR cs.total_fees <> COALESCE(SUM(l.shipping_fee + l.cod_fee + l.return_fee + l.other_fee), 0);

SELECT v.id, v.code, v.source_id
FROM cash_vouchers v
WHERE v.source_type = 'carrier_settlements'
  AND NOT EXISTS (SELECT 1 FROM carrier_settlements x WHERE x.id = v.source_id);

SELECT o.id, o.order_code, o.status
FROM orders o
JOIN order_shipping_details d ON d.order_id = o.id
WHERE o.fulfillment_method = 'SHIP'
  AND o.status IN ('Shipped', 'Completed', 'Returned_to_sender')
  AND d.carrier_id IS NULL;

-- Phase 3
SELECT v.id, v.code, v.used_count, COUNT(u.id) AS expected
FROM vouchers v
LEFT JOIN voucher_usages u ON u.voucher_id = v.id AND u.revoked_at IS NULL
GROUP BY v.id
HAVING v.used_count <> COUNT(u.id);

SELECT ri.id, ri.purchase_return_id, ri.po_item_id
FROM purchase_return_items ri
JOIN purchase_returns pr    ON pr.id = ri.purchase_return_id
JOIN purchase_order_items i ON i.id = ri.po_item_id
WHERE i.purchase_order_id <> pr.purchase_order_id;

SELECT pr.id, pr.total_amount, pr.vat_amount, x.goods, x.vat
FROM purchase_returns pr
JOIN (SELECT ri.purchase_return_id, SUM(ri.quantity * ri.unit_cost) AS goods,
             SUM(round(ri.quantity * ri.unit_cost * i.vat_rate / 100)) AS vat
      FROM purchase_return_items ri
      JOIN purchase_order_items i ON i.id = ri.po_item_id
      GROUP BY ri.purchase_return_id) x ON x.purchase_return_id = pr.id
WHERE pr.status = 'Completed' AND (pr.vat_amount <> x.vat OR pr.total_amount <> x.goods + x.vat);

SELECT u.id, u.order_id, u.voucher_id, o.voucher_id AS order_voucher_id
FROM voucher_usages u
JOIN orders o ON o.id = u.order_id
WHERE o.voucher_id IS DISTINCT FROM u.voucher_id;

SELECT pr.id, pr.total_amount, pr.vat_amount,
       COALESCE(SUM(ri.quantity * ri.unit_cost), 0) + COALESCE(SUM(round(ri.quantity * ri.unit_cost * pi.vat_rate / 100)), 0) AS expected_total,
       COALESCE(SUM(round(ri.quantity * ri.unit_cost * pi.vat_rate / 100)), 0) AS expected_vat
FROM purchase_returns pr
LEFT JOIN purchase_return_items ri ON ri.purchase_return_id = pr.id
LEFT JOIN purchase_order_items pi ON pi.id = ri.po_item_id
GROUP BY pr.id
HAVING pr.vat_amount <> COALESCE(SUM(round(ri.quantity * ri.unit_cost * pi.vat_rate / 100)), 0)
    OR pr.total_amount <> COALESCE(SUM(ri.quantity * ri.unit_cost), 0) + COALESCE(SUM(round(ri.quantity * ri.unit_cost * pi.vat_rate / 100)), 0);

SELECT e.id, e.reference_id
FROM stock_ledger_entries e
WHERE e.reference_type = 'purchase_returns' AND NOT EXISTS (SELECT 1 FROM purchase_returns x WHERE x.id = e.reference_id);
