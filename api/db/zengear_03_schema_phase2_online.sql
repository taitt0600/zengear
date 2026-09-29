-- ZenGear schema — Phase 2: Online sales (run after Phase 1)

BEGIN;

-- 0. Extensions
CREATE EXTENSION IF NOT EXISTS unaccent;
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Immutable wrapper so unaccent can be used in an index expression
CREATE OR REPLACE FUNCTION f_unaccent(text) RETURNS text
  LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT
  AS $$ SELECT public.unaccent('public.unaccent'::regdictionary, $1) $$;

-- 2. Geography & Addresses

CREATE TABLE addresses (
  id             uuid        NOT NULL DEFAULT uuidv7(),
  recipient_name varchar     NOT NULL,
  phone          varchar(20) NOT NULL,
  address_detail text        NOT NULL,
  label          varchar,
  is_default     boolean     NOT NULL DEFAULT false,
  created_by     uuid,
  created_at     timestamptz NOT NULL DEFAULT now(),
  updated_by     uuid,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  user_id        uuid        NOT NULL,
  country_id     integer     NOT NULL,
  province_id    integer,
  ward_id        integer,
  CONSTRAINT pk_addresses PRIMARY KEY (id),
  CONSTRAINT fk_addresses_countries_country_id FOREIGN KEY (country_id) REFERENCES countries (id) ON DELETE RESTRICT,
  CONSTRAINT fk_addresses_provinces_province_id FOREIGN KEY (province_id, country_id) REFERENCES provinces (id, country_id) ON DELETE RESTRICT,
  CONSTRAINT fk_addresses_wards_ward_id_province_id FOREIGN KEY (ward_id, province_id) REFERENCES wards (id, province_id) ON DELETE RESTRICT,
  CONSTRAINT fk_addresses_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT ck_addresses_ward_id CHECK (ward_id is null or province_id is not null)
);
CREATE INDEX ix_addresses_country_id ON addresses (country_id);
CREATE INDEX ix_addresses_province_id ON addresses (province_id);
CREATE INDEX ix_addresses_ward_id ON addresses (ward_id);
CREATE INDEX ix_addresses_user_id ON addresses (user_id);
CREATE UNIQUE INDEX ix_addresses_user_id_default ON addresses (user_id) WHERE is_default;

-- 6. Cart & Promotion

CREATE TABLE cart_items (
  user_id    uuid        NOT NULL,
  sku_id     uuid        NOT NULL,
  quantity   integer     NOT NULL,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_cart_items PRIMARY KEY (user_id, sku_id),
  CONSTRAINT fk_cart_items_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT fk_cart_items_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE CASCADE,
  CONSTRAINT ck_cart_items_quantity CHECK (quantity > 0)
);
CREATE INDEX ix_cart_items_sku_id ON cart_items (sku_id);

CREATE TABLE cart_serial_units (
  user_id        uuid        NOT NULL,
  serial_unit_id uuid        NOT NULL,
  created_at     timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_cart_serial_units PRIMARY KEY (user_id, serial_unit_id),
  CONSTRAINT fk_cart_serial_units_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT fk_cart_serial_units_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id) REFERENCES serial_units (id) ON DELETE CASCADE
);
CREATE INDEX ix_cart_serial_units_serial_unit_id ON cart_serial_units (serial_unit_id);

-- 7. Orders

CREATE TABLE carriers (
  id               uuid        NOT NULL DEFAULT uuidv7(),
  code             varchar(30) NOT NULL,
  name             varchar     NOT NULL,
  integration_type varchar(10) NOT NULL DEFAULT 'MANUAL',
  cod_settlement_days integer,
  is_active        boolean     NOT NULL DEFAULT true,
  created_by       uuid,
  created_at       timestamptz NOT NULL DEFAULT now(),
  updated_by       uuid,
  updated_at       timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_carriers PRIMARY KEY (id),
  CONSTRAINT ck_carriers_cod_settlement_days CHECK (cod_settlement_days is null or cod_settlement_days > 0)
);
CREATE UNIQUE INDEX ix_carriers_code ON carriers (code);

CREATE TABLE shipping_methods (
  id           uuid          NOT NULL DEFAULT uuidv7(),
  name         varchar       NOT NULL,
  base_fee     numeric(15,0) NOT NULL,
  is_active    boolean       NOT NULL DEFAULT true,
  created_by   uuid,
  created_at   timestamptz   NOT NULL DEFAULT now(),
  updated_by   uuid,
  updated_at   timestamptz   NOT NULL DEFAULT now(),
  tax_class_id uuid          NOT NULL,
  carrier_id   uuid,
  CONSTRAINT pk_shipping_methods PRIMARY KEY (id),
  CONSTRAINT fk_shipping_methods_tax_classes_tax_class_id FOREIGN KEY (tax_class_id) REFERENCES tax_classes (id) ON DELETE RESTRICT,
  CONSTRAINT fk_shipping_methods_carriers_carrier_id FOREIGN KEY (carrier_id) REFERENCES carriers (id) ON DELETE RESTRICT,
  CONSTRAINT ck_shipping_methods_base_fee CHECK (base_fee >= 0)
);
CREATE INDEX ix_shipping_methods_tax_class_id ON shipping_methods (tax_class_id);
CREATE INDEX ix_shipping_methods_carrier_id ON shipping_methods (carrier_id);

CREATE TABLE shipping_fee_rules (
  id                 uuid          NOT NULL DEFAULT uuidv7(),
  min_order_value    numeric(15,0) NOT NULL DEFAULT 0,
  fee                numeric(15,0) NOT NULL,
  is_active          boolean       NOT NULL DEFAULT true,
  created_by         uuid,
  created_at         timestamptz   NOT NULL DEFAULT now(),
  updated_by         uuid,
  updated_at         timestamptz   NOT NULL DEFAULT now(),
  shipping_method_id uuid          NOT NULL,
  province_id        integer,
  CONSTRAINT pk_shipping_fee_rules PRIMARY KEY (id),
  CONSTRAINT fk_shipping_fee_rules_shipping_methods_shipping_method_id FOREIGN KEY (shipping_method_id) REFERENCES shipping_methods (id) ON DELETE CASCADE,
  CONSTRAINT fk_shipping_fee_rules_provinces_province_id FOREIGN KEY (province_id) REFERENCES provinces (id) ON DELETE RESTRICT,
  CONSTRAINT ck_shipping_fee_rules_min_order_value CHECK (min_order_value >= 0),
  CONSTRAINT ck_shipping_fee_rules_fee CHECK (fee >= 0)
);
CREATE UNIQUE INDEX ix_shipping_fee_rules_method_province_min_value ON shipping_fee_rules (shipping_method_id, province_id, min_order_value) NULLS NOT DISTINCT;
CREATE INDEX ix_shipping_fee_rules_province_id ON shipping_fee_rules (province_id);

CREATE TABLE order_shipping_details (
  order_id                uuid        NOT NULL,
  shipping_country        varchar     NOT NULL,
  shipping_province       varchar,
  shipping_ward           varchar,
  shipping_address_detail text        NOT NULL,
  tracking_code           varchar(50),
  created_at              timestamptz NOT NULL DEFAULT now(),
  shipping_method_id      uuid        NOT NULL,
  carrier_id              uuid,
  CONSTRAINT pk_order_shipping_details PRIMARY KEY (order_id),
  CONSTRAINT fk_order_shipping_details_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_shipping_details_shipping_methods_shipping_method_id FOREIGN KEY (shipping_method_id) REFERENCES shipping_methods (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_shipping_details_carriers_carrier_id FOREIGN KEY (carrier_id) REFERENCES carriers (id) ON DELETE RESTRICT,
  CONSTRAINT ck_order_shipping_details_tracking_code CHECK (tracking_code is null or carrier_id is not null)
);
CREATE INDEX ix_order_shipping_details_shipping_method_id ON order_shipping_details (shipping_method_id);
CREATE INDEX ix_order_shipping_details_carrier_id ON order_shipping_details (carrier_id);

-- 9. Payments & POS

CREATE TABLE carrier_settlements (
  id               uuid          NOT NULL DEFAULT uuidv7(),
  code             varchar(20)   NOT NULL,
  status           varchar(20)   NOT NULL DEFAULT 'Draft',
  source           varchar(20)   NOT NULL DEFAULT 'MANUAL',
  external_ref     varchar(100),
  period_from      date          NOT NULL,
  period_to        date          NOT NULL,
  total_cod        numeric(15,0) NOT NULL DEFAULT 0,
  total_fees       numeric(15,0) NOT NULL DEFAULT 0,
  previous_balance numeric(15,0) NOT NULL DEFAULT 0,
  net_amount       numeric(15,0) NOT NULL DEFAULT 0,
  bank_reference   varchar(100),
  confirmed_at     timestamptz,
  confirmed_by     uuid,
  paid_at          timestamptz,
  note             text,
  created_by       uuid,
  created_at       timestamptz   NOT NULL DEFAULT now(),
  updated_by       uuid,
  updated_at       timestamptz   NOT NULL DEFAULT now(),
  carrier_id       uuid          NOT NULL,
  shift_id         uuid,
  CONSTRAINT pk_carrier_settlements PRIMARY KEY (id),
  CONSTRAINT fk_carrier_settlements_carriers_carrier_id FOREIGN KEY (carrier_id) REFERENCES carriers (id) ON DELETE RESTRICT,
  CONSTRAINT fk_carrier_settlements_shifts_shift_id FOREIGN KEY (shift_id) REFERENCES shifts (id) ON DELETE RESTRICT,
  CONSTRAINT ck_carrier_settlements_period CHECK (period_to >= period_from),
  CONSTRAINT ck_carrier_settlements_totals CHECK (total_cod >= 0 and total_fees >= 0),
  CONSTRAINT ck_carrier_settlements_net_amount CHECK (net_amount = total_cod - total_fees + previous_balance),
  CONSTRAINT ck_carrier_settlements_paid_at CHECK ((status = 'Paid') = (paid_at is not null)),
  CONSTRAINT ck_carrier_settlements_confirmed_at CHECK (status not in ('Confirmed', 'Paid') or confirmed_at is not null),
  CONSTRAINT ck_carrier_settlements_bank_reference CHECK (status <> 'Paid' or net_amount = 0 or bank_reference is not null or shift_id is not null)
);
CREATE UNIQUE INDEX ix_carrier_settlements_code ON carrier_settlements (code);
CREATE INDEX ix_carrier_settlements_carrier_id_status ON carrier_settlements (carrier_id, status);
CREATE UNIQUE INDEX ix_carrier_settlements_carrier_id_external_ref ON carrier_settlements (carrier_id, external_ref);
CREATE INDEX ix_carrier_settlements_shift_id ON carrier_settlements (shift_id);

CREATE TABLE carrier_settlement_lines (
  id                    uuid          NOT NULL DEFAULT uuidv7(),
  expected_cod          numeric(15,0) NOT NULL DEFAULT 0,
  cod_amount            numeric(15,0) NOT NULL DEFAULT 0,
  shipping_fee          numeric(15,0) NOT NULL DEFAULT 0,
  cod_fee               numeric(15,0) NOT NULL DEFAULT 0,
  return_fee            numeric(15,0) NOT NULL DEFAULT 0,
  other_fee             numeric(15,0) NOT NULL DEFAULT 0,
  note                  text,
  created_by            uuid,
  created_at            timestamptz   NOT NULL DEFAULT now(),
  updated_by            uuid,
  updated_at            timestamptz   NOT NULL DEFAULT now(),
  settlement_id         uuid          NOT NULL,
  order_id              uuid          NOT NULL,
  CONSTRAINT pk_carrier_settlement_lines PRIMARY KEY (id),
  CONSTRAINT fk_carrier_settlement_lines_carrier_settlements_settlement_id FOREIGN KEY (settlement_id) REFERENCES carrier_settlements (id) ON DELETE RESTRICT,
  CONSTRAINT fk_carrier_settlement_lines_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT ck_carrier_settlement_lines_cod CHECK (expected_cod >= 0 and cod_amount >= 0),
  CONSTRAINT ck_carrier_settlement_lines_fees CHECK (shipping_fee >= 0 and cod_fee >= 0 and return_fee >= 0 and other_fee >= 0)
);
CREATE UNIQUE INDEX ix_carrier_settlement_lines_order_id ON carrier_settlement_lines (order_id);
CREATE INDEX ix_carrier_settlement_lines_settlement_id ON carrier_settlement_lines (settlement_id);

-- 10. System

CREATE TABLE url_redirects (
  id         uuid         NOT NULL DEFAULT uuidv7(),
  old_path   varchar(300) NOT NULL,
  new_path   varchar(300) NOT NULL,
  created_by uuid,
  created_at timestamptz  NOT NULL DEFAULT now(),
  CONSTRAINT pk_url_redirects PRIMARY KEY (id),
  CONSTRAINT ck_url_redirects_old_path_new_path CHECK (old_path <> new_path)
);
CREATE UNIQUE INDEX ix_url_redirects_old_path ON url_redirects (old_path);

-- Columns added to tables of earlier phases
ALTER TABLE customer_profiles ADD COLUMN cod_allowed boolean NOT NULL DEFAULT true;

CREATE INDEX ix_products_name_trgm ON products USING gin (f_unaccent(lower(name)) gin_trgm_ops);

ALTER TABLE order_payments ADD COLUMN expires_at timestamptz;
ALTER TABLE order_payments ADD CONSTRAINT ck_order_payments_expires_at CHECK (expires_at is null or method = 'VNPAY');

COMMIT;
