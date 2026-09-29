-- ZenGear schema — Phase 3: Operations (run after Phase 2)

BEGIN;

-- 5. Inventory & Purchasing

CREATE TABLE purchase_returns (
  id                uuid          NOT NULL DEFAULT uuidv7(),
  status            varchar(20)   NOT NULL DEFAULT 'Draft',
  reason            text          NOT NULL,
  total_amount      numeric(15,0) NOT NULL DEFAULT 0,
  vat_amount        numeric(15,0) NOT NULL DEFAULT 0,
  completed_at      timestamptz,
  created_by        uuid,
  created_at        timestamptz   NOT NULL DEFAULT now(),
  updated_by        uuid,
  updated_at        timestamptz   NOT NULL DEFAULT now(),
  supplier_id       uuid          NOT NULL,
  purchase_order_id uuid          NOT NULL,
  CONSTRAINT pk_purchase_returns PRIMARY KEY (id),
  CONSTRAINT fk_purchase_returns_suppliers_supplier_id FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT,
  CONSTRAINT fk_purchase_returns_purchase_orders_purchase_order_id FOREIGN KEY (purchase_order_id, supplier_id) REFERENCES purchase_orders (id, supplier_id) ON DELETE RESTRICT,
  CONSTRAINT ck_purchase_returns_total_amount CHECK (total_amount >= 0),
  CONSTRAINT ck_purchase_returns_vat_amount CHECK (vat_amount >= 0 and vat_amount <= total_amount),
  CONSTRAINT ck_purchase_returns_status CHECK ((status = 'Completed') = (completed_at is not null))
);
CREATE INDEX ix_purchase_returns_supplier_id ON purchase_returns (supplier_id);
CREATE INDEX ix_purchase_returns_purchase_order_id ON purchase_returns (purchase_order_id);
CREATE INDEX ix_purchase_returns_status ON purchase_returns (status);

CREATE TABLE purchase_return_items (
  id                 uuid          NOT NULL DEFAULT uuidv7(),
  quantity           integer       NOT NULL,
  unit_cost          numeric(15,0) NOT NULL,
  created_by         uuid,
  created_at         timestamptz   NOT NULL DEFAULT now(),
  purchase_return_id uuid          NOT NULL,
  po_item_id         uuid          NOT NULL,
  serial_unit_id     uuid,
  CONSTRAINT pk_purchase_return_items PRIMARY KEY (id),
  CONSTRAINT fk_purchase_return_items_purchase_returns_purchase_return_id FOREIGN KEY (purchase_return_id) REFERENCES purchase_returns (id) ON DELETE RESTRICT,
  CONSTRAINT fk_purchase_return_items_purchase_order_items_po_item_id FOREIGN KEY (po_item_id) REFERENCES purchase_order_items (id) ON DELETE RESTRICT,
  CONSTRAINT fk_purchase_return_items_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT ck_purchase_return_items_quantity CHECK (quantity > 0),
  CONSTRAINT ck_purchase_return_items_unit_cost CHECK (unit_cost >= 0),
  CONSTRAINT ck_purchase_return_items_serial_unit_id CHECK (serial_unit_id is null or quantity = 1)
);
CREATE INDEX ix_purchase_return_items_purchase_return_id ON purchase_return_items (purchase_return_id);
CREATE INDEX ix_purchase_return_items_po_item_id ON purchase_return_items (po_item_id);
CREATE INDEX ix_purchase_return_items_serial_unit_id ON purchase_return_items (serial_unit_id);

-- 6. Cart & Promotion

CREATE TABLE vouchers (
  id                    uuid          NOT NULL DEFAULT uuidv7(),
  code                  varchar,
  code_type             varchar(20)   NOT NULL DEFAULT 'SHARED',
  type                  varchar(20)   NOT NULL,
  value                 numeric(15,0) NOT NULL,
  max_discount_amount   numeric(15,0),
  min_order_value       numeric(15,0),
  usage_limit           integer,
  used_count            integer       NOT NULL DEFAULT 0,
  starts_at             timestamptz   NOT NULL,
  ends_at               timestamptz   NOT NULL,
  applies_to_sale_items boolean       NOT NULL DEFAULT true,
  once_per_customer     boolean       NOT NULL DEFAULT true,
  is_active             boolean       NOT NULL DEFAULT true,
  created_by            uuid,
  created_at            timestamptz   NOT NULL DEFAULT now(),
  updated_by            uuid,
  updated_at            timestamptz   NOT NULL DEFAULT now(),
  CONSTRAINT pk_vouchers PRIMARY KEY (id),
  CONSTRAINT ck_vouchers_value CHECK (value > 0),
  CONSTRAINT ck_vouchers_percentage_value CHECK (type <> 'PERCENTAGE' or value <= 100),
  CONSTRAINT ck_vouchers_max_discount_amount CHECK (max_discount_amount is null or max_discount_amount > 0),
  CONSTRAINT ck_vouchers_min_order_value CHECK (min_order_value is null or min_order_value >= 0),
  CONSTRAINT ck_vouchers_usage_limit CHECK (usage_limit is null or usage_limit > 0),
  CONSTRAINT ck_vouchers_used_count CHECK (used_count >= 0),
  CONSTRAINT ck_vouchers_usage_limit_used_count CHECK (usage_limit is null or used_count <= usage_limit),
  CONSTRAINT ck_vouchers_ends_at CHECK (ends_at > starts_at),
  CONSTRAINT ck_vouchers_code_type CHECK ((code_type = 'SHARED') = (code is not null))
);
CREATE UNIQUE INDEX ix_vouchers_code ON vouchers (code);

CREATE TABLE voucher_codes (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  code       varchar(30) NOT NULL,
  is_active  boolean     NOT NULL DEFAULT true,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  voucher_id uuid        NOT NULL,
  CONSTRAINT pk_voucher_codes PRIMARY KEY (id),
  CONSTRAINT fk_voucher_codes_vouchers_voucher_id FOREIGN KEY (voucher_id) REFERENCES vouchers (id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ix_voucher_codes_code ON voucher_codes (code);
CREATE INDEX ix_voucher_codes_voucher_id ON voucher_codes (voucher_id);
CREATE UNIQUE INDEX ix_voucher_codes_id_voucher_id ON voucher_codes (id, voucher_id);

CREATE TABLE voucher_targets (
  id          uuid        NOT NULL DEFAULT uuidv7(),
  created_by  uuid,
  created_at  timestamptz NOT NULL DEFAULT now(),
  voucher_id  uuid        NOT NULL,
  category_id uuid,
  product_id  uuid,
  CONSTRAINT pk_voucher_targets PRIMARY KEY (id),
  CONSTRAINT fk_voucher_targets_vouchers_voucher_id FOREIGN KEY (voucher_id) REFERENCES vouchers (id) ON DELETE CASCADE,
  CONSTRAINT fk_voucher_targets_categories_category_id FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE RESTRICT,
  CONSTRAINT fk_voucher_targets_products_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT,
  CONSTRAINT ck_voucher_targets_category_id CHECK ((category_id is not null)::int + (product_id is not null)::int = 1)
);
CREATE INDEX ix_voucher_targets_voucher_id ON voucher_targets (voucher_id);
CREATE INDEX ix_voucher_targets_category_id ON voucher_targets (category_id);
CREATE INDEX ix_voucher_targets_product_id ON voucher_targets (product_id);

-- 7. Orders

CREATE TABLE voucher_usages (
  id              uuid        NOT NULL DEFAULT uuidv7(),
  revoked_at      timestamptz,
  once_per_customer boolean     NOT NULL DEFAULT true,
  created_at      timestamptz NOT NULL DEFAULT now(),
  order_id        uuid        NOT NULL,
  voucher_id      uuid        NOT NULL,
  customer_id     uuid,
  voucher_code_id uuid,
  CONSTRAINT pk_voucher_usages PRIMARY KEY (id),
  CONSTRAINT fk_voucher_usages_orders_order_id_customer_id FOREIGN KEY (order_id, customer_id) REFERENCES orders (id, customer_id) ON DELETE RESTRICT,
  CONSTRAINT fk_voucher_usages_voucher_codes_voucher_code_id FOREIGN KEY (voucher_code_id, voucher_id) REFERENCES voucher_codes (id, voucher_id) ON DELETE RESTRICT,
  CONSTRAINT fk_voucher_usages_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_voucher_usages_vouchers_voucher_id FOREIGN KEY (voucher_id) REFERENCES vouchers (id) ON DELETE RESTRICT,
  CONSTRAINT fk_voucher_usages_users_customer_id FOREIGN KEY (customer_id) REFERENCES users (id) ON DELETE RESTRICT
);
CREATE INDEX ix_voucher_usages_voucher_id_customer_id ON voucher_usages (voucher_id, customer_id);
CREATE UNIQUE INDEX ix_voucher_usages_order_id ON voucher_usages (order_id);
CREATE INDEX ix_voucher_usages_customer_id ON voucher_usages (customer_id);
CREATE INDEX ix_voucher_usages_voucher_code_id ON voucher_usages (voucher_code_id);
CREATE UNIQUE INDEX ix_voucher_usages_voucher_id_customer_id_active ON voucher_usages (voucher_id, customer_id) WHERE revoked_at IS NULL AND once_per_customer;
CREATE UNIQUE INDEX ix_voucher_usages_voucher_code_id_active ON voucher_usages (voucher_code_id) WHERE revoked_at IS NULL;

-- Columns added to tables of earlier phases
ALTER TABLE supplier_payments ADD COLUMN purchase_return_id uuid;
ALTER TABLE supplier_payments ADD CONSTRAINT fk_supplier_payments_purchase_returns_purchase_return_id FOREIGN KEY (purchase_return_id) REFERENCES purchase_returns (id) ON DELETE RESTRICT;
ALTER TABLE supplier_payments ADD CONSTRAINT ck_supplier_payments_purchase_return_id CHECK (purchase_return_id is null or type = 'REFUND');
CREATE INDEX ix_supplier_payments_purchase_return_id ON supplier_payments (purchase_return_id);

ALTER TABLE orders DROP CONSTRAINT ck_orders_discount_manual_only;
ALTER TABLE orders ADD COLUMN voucher_id uuid;
ALTER TABLE orders ADD CONSTRAINT fk_orders_vouchers_voucher_id FOREIGN KEY (voucher_id) REFERENCES vouchers (id) ON DELETE RESTRICT;
ALTER TABLE orders ADD CONSTRAINT ck_orders_discount_without_voucher CHECK (voucher_id is not null or discount_amount = manual_discount_amount);
CREATE INDEX ix_orders_voucher_id ON orders (voucher_id);

COMMIT;
