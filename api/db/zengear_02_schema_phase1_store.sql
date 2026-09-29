-- ZenGear schema — Phase 1: Store (in-store sales)
-- DEV/TEST ONLY: drops all tables (connect to database: zengear)

BEGIN;

-- 0. Drop & extensions
DROP VIEW IF EXISTS product_with_effective_price;
DROP TABLE IF EXISTS
  users, customer_profiles, staff_profiles, roles, permissions, user_roles, role_permissions,
  countries, provinces, wards, addresses,
  tax_classes, tax_rates,
  categories, brands, product_series, products, product_skus, product_images, product_badges, product_badge_assignments, product_tags, pc_build_components,
  business_locations, warehouses, suppliers, purchase_orders, purchase_order_items, purchase_receipts, stock_balances, stock_ledger_entries, serial_units, adjustment_reasons, stock_adjustments, stock_counts, stock_count_lines, purchase_returns, purchase_return_items, supplier_payments,
  wishlists, cart_items, cart_serial_units, vouchers, voucher_codes, voucher_targets,
  carriers, shipping_methods, shipping_fee_rules, orders, order_shipping_details, order_invoice_details, order_items, order_item_serials, order_item_components, pc_assemblies, pc_assembly_components, voucher_usages,
  returns, return_items, warranty_requests,
  shifts, shift_cash_adjustments, cash_accounts, cash_voucher_categories, cash_vouchers, order_payments, e_invoices, carrier_settlements, carrier_settlement_lines,
  daily_sequences, import_jobs, status_history, attachments, url_redirects, notifications, audit_logs
CASCADE;
DROP FUNCTION IF EXISTS fn_user_roles_min_one(), fn_user_roles_last_admin(), f_unaccent(text), fn_audit_erase_values(varchar, jsonb);

CREATE EXTENSION IF NOT EXISTS btree_gist;

-- 1. Identity & Access

CREATE TABLE users (
  id                uuid         NOT NULL DEFAULT uuidv7(),
  firebase_uid      varchar(128),
  name              varchar      NOT NULL,
  email             varchar(255),
  phone             varchar(20),
  phone_verified_at timestamptz,
  status            varchar(20)  NOT NULL DEFAULT 'ACTIVE',
  last_login_at     timestamptz,
  created_by        uuid,
  created_at        timestamptz  NOT NULL DEFAULT now(),
  updated_by        uuid,
  updated_at        timestamptz  NOT NULL DEFAULT now(),
  CONSTRAINT pk_users PRIMARY KEY (id),
  CONSTRAINT ck_users_email CHECK (email = lower(email)),
  CONSTRAINT ck_users_email_phone CHECK (email is not null or phone is not null),
  CONSTRAINT ck_users_phone CHECK (phone is null or phone ~ '^\+[1-9][0-9]{7,14}$'),
  CONSTRAINT ck_users_phone_verified_at CHECK (phone_verified_at is null or phone is not null)
);
CREATE UNIQUE INDEX ix_users_firebase_uid ON users (firebase_uid);
CREATE UNIQUE INDEX ix_users_email ON users (email);
CREATE UNIQUE INDEX ix_users_phone ON users (phone);

CREATE TABLE customer_profiles (
  user_id              uuid        NOT NULL,
  gender               varchar(10),
  date_of_birth        date,
  marketing_consent    boolean     NOT NULL DEFAULT false,
  marketing_consent_at timestamptz,
  marketing_consent_source  varchar(20),
  marketing_consent_version varchar(20),
  internal_note        text,
  created_at           timestamptz NOT NULL DEFAULT now(),
  updated_by           uuid,
  updated_at           timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_customer_profiles PRIMARY KEY (user_id),
  CONSTRAINT fk_customer_profiles_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT ck_customer_profiles_date_of_birth CHECK (date_of_birth is null or date_of_birth >= '1900-01-01'),
  CONSTRAINT ck_customer_profiles_marketing_consent CHECK (marketing_consent = false or marketing_consent_at is not null),
  CONSTRAINT ck_customer_profiles_marketing_consent_proof CHECK (marketing_consent = false or (marketing_consent_source is not null and marketing_consent_version is not null))
);

CREATE TABLE staff_profiles (
  user_id              uuid        NOT NULL,
  employee_code        varchar,
  joined_date          date,
  work_phone           varchar(20),
  left_date            date,
  must_change_password boolean     NOT NULL DEFAULT false,
  created_at           timestamptz NOT NULL DEFAULT now(),
  updated_by           uuid,
  updated_at           timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_staff_profiles PRIMARY KEY (user_id),
  CONSTRAINT fk_staff_profiles_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT ck_staff_profiles_left_date CHECK (left_date is null or joined_date is null or left_date >= joined_date)
);
CREATE UNIQUE INDEX ix_staff_profiles_employee_code ON staff_profiles (employee_code);

CREATE TABLE roles (
  id             uuid        NOT NULL DEFAULT uuidv7(),
  name           varchar     NOT NULL,
  scoping_type   varchar(10) NOT NULL,
  is_system_role boolean     NOT NULL DEFAULT false,
  max_discount_percent numeric(5,2),
  created_by     uuid,
  created_at     timestamptz NOT NULL DEFAULT now(),
  updated_by     uuid,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_roles PRIMARY KEY (id),
  CONSTRAINT ck_roles_max_discount_percent CHECK (max_discount_percent is null or (max_discount_percent >= 0 and max_discount_percent <= 100))
);
CREATE UNIQUE INDEX ix_roles_name ON roles (name);

CREATE TABLE permissions (
  id          uuid         NOT NULL DEFAULT uuidv7(),
  key         varchar(100) NOT NULL,
  description text,
  created_by  uuid,
  created_at  timestamptz  NOT NULL DEFAULT now(),
  updated_by  uuid,
  updated_at  timestamptz  NOT NULL DEFAULT now(),
  CONSTRAINT pk_permissions PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_permissions_key ON permissions (key);

CREATE TABLE user_roles (
  user_id    uuid        NOT NULL,
  role_id    uuid        NOT NULL,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_user_roles PRIMARY KEY (user_id, role_id),
  CONSTRAINT fk_user_roles_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE,
  CONSTRAINT fk_user_roles_roles_role_id FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE RESTRICT
);
CREATE INDEX ix_user_roles_role_id ON user_roles (role_id);
CREATE OR REPLACE FUNCTION fn_user_roles_min_one() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  PERFORM 1 FROM users WHERE id = OLD.user_id FOR UPDATE;
  IF FOUND AND NOT EXISTS (SELECT 1 FROM user_roles WHERE user_id = OLD.user_id) THEN
    RAISE EXCEPTION 'R-SEC-01: user % must keep at least 1 role', OLD.user_id USING ERRCODE = 'check_violation';
  END IF;
  RETURN NULL;
END $$;

CREATE CONSTRAINT TRIGGER trg_user_roles_min_one
  AFTER DELETE ON user_roles DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION fn_user_roles_min_one();

CREATE OR REPLACE FUNCTION fn_user_roles_last_admin() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  PERFORM 1 FROM roles WHERE id = OLD.role_id AND name = 'Admin' FOR UPDATE;
  IF FOUND AND NOT EXISTS (
       SELECT 1 FROM user_roles ur JOIN users u ON u.id = ur.user_id
       WHERE ur.role_id = OLD.role_id AND u.status = 'ACTIVE') THEN
    RAISE EXCEPTION 'R-SEC-02: cannot remove the last active Admin' USING ERRCODE = 'check_violation';
  END IF;
  RETURN NULL;
END $$;

CREATE CONSTRAINT TRIGGER trg_user_roles_last_admin
  AFTER DELETE ON user_roles DEFERRABLE INITIALLY DEFERRED
  FOR EACH ROW EXECUTE FUNCTION fn_user_roles_last_admin();

CREATE TABLE role_permissions (
  role_id       uuid        NOT NULL,
  permission_id uuid        NOT NULL,
  created_by    uuid,
  created_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_role_permissions PRIMARY KEY (role_id, permission_id),
  CONSTRAINT fk_role_permissions_roles_role_id FOREIGN KEY (role_id) REFERENCES roles (id) ON DELETE CASCADE,
  CONSTRAINT fk_role_permissions_permissions_permission_id FOREIGN KEY (permission_id) REFERENCES permissions (id) ON DELETE CASCADE
);
CREATE INDEX ix_role_permissions_permission_id ON role_permissions (permission_id);

-- 2. Geography & Addresses

CREATE TABLE countries (
  id                    integer     NOT NULL,
  country_code          varchar(2)  NOT NULL,
  country_code3         varchar(3),
  country_number        varchar(3),
  common_name           varchar,
  formal_name           varchar,
  country_type          varchar,
  country_sub_type      varchar,
  sovereignty           varchar,
  capital               varchar,
  currency_code         varchar(3),
  currency_name         varchar,
  telephone_code        varchar(10),
  internet_country_code varchar(10),
  flag_url              text,
  sort_order            integer,
  is_published          boolean     NOT NULL DEFAULT true,
  is_deleted            boolean     NOT NULL DEFAULT false,
  CONSTRAINT pk_countries PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_countries_country_code ON countries (country_code);
CREATE UNIQUE INDEX ix_countries_country_code3 ON countries (country_code3);

CREATE TABLE provinces (
  id         integer     NOT NULL,
  name       varchar     NOT NULL,
  code       varchar(20),
  type       varchar(20),
  zip_code   varchar(20),
  phone_code varchar(10),
  is_active  boolean     NOT NULL DEFAULT true,
  sort_order integer     NOT NULL DEFAULT 0,
  country_id integer     NOT NULL,
  CONSTRAINT pk_provinces PRIMARY KEY (id),
  CONSTRAINT fk_provinces_countries_country_id FOREIGN KEY (country_id) REFERENCES countries (id) ON DELETE RESTRICT
);
CREATE INDEX ix_provinces_country_id ON provinces (country_id);
CREATE UNIQUE INDEX ix_provinces_id_country_id ON provinces (id, country_id);
CREATE UNIQUE INDEX ix_provinces_code ON provinces (code);

CREATE TABLE wards (
  id          integer     NOT NULL,
  name        varchar     NOT NULL,
  code        varchar(20),
  type        varchar(20),
  zip_code    varchar(20),
  phone_code  varchar(10),
  is_active   boolean     NOT NULL DEFAULT true,
  sort_order  integer     NOT NULL DEFAULT 0,
  province_id integer,
  CONSTRAINT pk_wards PRIMARY KEY (id),
  CONSTRAINT fk_wards_provinces_province_id FOREIGN KEY (province_id) REFERENCES provinces (id) ON DELETE RESTRICT
);
CREATE INDEX ix_wards_province_id ON wards (province_id);
CREATE UNIQUE INDEX ix_wards_id_province_id ON wards (id, province_id);
CREATE UNIQUE INDEX ix_wards_code ON wards (code);

-- 3. Tax

CREATE TABLE tax_classes (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  code       varchar(50) NOT NULL,
  name       varchar     NOT NULL,
  is_active  boolean     NOT NULL DEFAULT true,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_tax_classes PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_tax_classes_code ON tax_classes (code);

CREATE TABLE tax_rates (
  id           uuid         NOT NULL DEFAULT uuidv7(),
  rate         numeric(5,2) NOT NULL,
  valid_from   timestamptz  NOT NULL,
  valid_to     timestamptz,
  created_by   uuid,
  created_at   timestamptz  NOT NULL DEFAULT now(),
  updated_by   uuid,
  updated_at   timestamptz  NOT NULL DEFAULT now(),
  tax_class_id uuid         NOT NULL,
  CONSTRAINT pk_tax_rates PRIMARY KEY (id),
  CONSTRAINT fk_tax_rates_tax_classes_tax_class_id FOREIGN KEY (tax_class_id) REFERENCES tax_classes (id) ON DELETE RESTRICT,
  CONSTRAINT ck_tax_rates_rate CHECK (rate >= 0 and rate <= 100),
  CONSTRAINT ck_tax_rates_valid_to CHECK (valid_to is null or valid_to > valid_from),
  CONSTRAINT ex_tax_rates_no_overlap EXCLUDE USING gist (tax_class_id WITH =, tstzrange(valid_from, valid_to) WITH &&)
);
CREATE INDEX ix_tax_rates_tax_class_id_valid_from ON tax_rates (tax_class_id, valid_from);

-- 4. Catalog

CREATE TABLE categories (
  id                 uuid        NOT NULL DEFAULT uuidv7(),
  name               varchar     NOT NULL,
  slug               varchar     NOT NULL,
  attribute_schema   jsonb,
  doa_exchange_days  integer,
  return_days        integer,
  return_fee_first_month_percent numeric(5,2),
  return_fee_monthly_percent     numeric(5,2),
  is_active          boolean     NOT NULL DEFAULT true,
  created_by         uuid,
  created_at         timestamptz NOT NULL DEFAULT now(),
  updated_by         uuid,
  updated_at         timestamptz NOT NULL DEFAULT now(),
  parent_category_id uuid,
  CONSTRAINT pk_categories PRIMARY KEY (id),
  CONSTRAINT fk_categories_categories_parent_category_id FOREIGN KEY (parent_category_id) REFERENCES categories (id) ON DELETE RESTRICT,
  CONSTRAINT ck_categories_doa_exchange_days CHECK (doa_exchange_days is null or doa_exchange_days >= 0),
  CONSTRAINT ck_categories_return_days CHECK (return_days is null or return_days >= 0),
  CONSTRAINT ck_categories_return_fee_first_month_percent CHECK (return_fee_first_month_percent is null or (return_fee_first_month_percent >= 0 and return_fee_first_month_percent <= 100)),
  CONSTRAINT ck_categories_return_fee_monthly_percent CHECK (return_fee_monthly_percent is null or (return_fee_monthly_percent >= 0 and return_fee_monthly_percent <= 100)),
  CONSTRAINT ck_categories_return_fee_pair CHECK ((return_fee_first_month_percent is null) = (return_fee_monthly_percent is null))
);
CREATE UNIQUE INDEX ix_categories_slug ON categories (slug);
CREATE INDEX ix_categories_parent_category_id ON categories (parent_category_id);

CREATE TABLE brands (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  name       varchar     NOT NULL,
  slug       varchar     NOT NULL,
  logo_url   text,
  is_active  boolean     NOT NULL DEFAULT true,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_brands PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_brands_slug ON brands (slug);

CREATE TABLE product_series (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  name       varchar     NOT NULL,
  slug       varchar     NOT NULL,
  is_active  boolean     NOT NULL DEFAULT true,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid,
  updated_at timestamptz NOT NULL DEFAULT now(),
  brand_id   uuid        NOT NULL,
  CONSTRAINT pk_product_series PRIMARY KEY (id),
  CONSTRAINT fk_product_series_brands_brand_id FOREIGN KEY (brand_id) REFERENCES brands (id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ix_product_series_slug ON product_series (slug);
CREATE INDEX ix_product_series_brand_id ON product_series (brand_id);
CREATE UNIQUE INDEX ix_product_series_id_brand_id ON product_series (id, brand_id);

CREATE TABLE products (
  id              uuid        NOT NULL DEFAULT uuidv7(),
  name            varchar     NOT NULL,
  slug            varchar     NOT NULL,
  description     text,
  attributes      jsonb,
  variant_options jsonb,
  meta            jsonb,
  requires_serial boolean     NOT NULL DEFAULT false,
  warranty_months integer,
  status          varchar(20) NOT NULL DEFAULT 'DRAFT',
  created_by      uuid,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_by      uuid,
  updated_at      timestamptz NOT NULL DEFAULT now(),
  category_id     uuid        NOT NULL,
  brand_id        uuid        NOT NULL,
  series_id       uuid,
  tax_class_id    uuid        NOT NULL,
  CONSTRAINT pk_products PRIMARY KEY (id),
  CONSTRAINT fk_products_categories_category_id FOREIGN KEY (category_id) REFERENCES categories (id) ON DELETE RESTRICT,
  CONSTRAINT fk_products_brands_brand_id FOREIGN KEY (brand_id) REFERENCES brands (id) ON DELETE RESTRICT,
  CONSTRAINT fk_products_product_series_series_id FOREIGN KEY (series_id, brand_id) REFERENCES product_series (id, brand_id) ON DELETE RESTRICT,
  CONSTRAINT fk_products_tax_classes_tax_class_id FOREIGN KEY (tax_class_id) REFERENCES tax_classes (id) ON DELETE RESTRICT,
  CONSTRAINT ck_products_warranty_months CHECK (warranty_months is null or warranty_months >= 0)
);
CREATE UNIQUE INDEX ix_products_slug ON products (slug);
CREATE INDEX ix_products_category_id_status ON products (category_id, status);
CREATE INDEX ix_products_brand_id ON products (brand_id);
CREATE INDEX ix_products_series_id ON products (series_id);
CREATE INDEX ix_products_status ON products (status);
CREATE INDEX ix_products_tax_class_id ON products (tax_class_id);
CREATE INDEX ix_products_attributes ON products USING gin (attributes jsonb_path_ops);

CREATE TABLE product_skus (
  id                  uuid          NOT NULL DEFAULT uuidv7(),
  sku                 varchar       NOT NULL,
  barcode             varchar(50),
  attributes          jsonb,
  price               numeric(15,0) NOT NULL,
  compare_at_price    numeric(15,0),
  low_stock_threshold integer,
  max_stock_threshold integer,
  sold_count          integer       NOT NULL DEFAULT 0,
  average_cost        numeric(15,0) NOT NULL DEFAULT 0,
  is_default          boolean       NOT NULL DEFAULT false,
  is_service          boolean       NOT NULL DEFAULT false,
  is_pc_build         boolean       NOT NULL DEFAULT false,
  is_active           boolean       NOT NULL DEFAULT true,
  created_by          uuid,
  created_at          timestamptz   NOT NULL DEFAULT now(),
  updated_by          uuid,
  updated_at          timestamptz   NOT NULL DEFAULT now(),
  product_id          uuid          NOT NULL,
  CONSTRAINT pk_product_skus PRIMARY KEY (id),
  CONSTRAINT fk_product_skus_products_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE RESTRICT,
  CONSTRAINT ck_product_skus_compare_at_price CHECK (compare_at_price is null or compare_at_price > price),
  CONSTRAINT ck_product_skus_price CHECK (price >= 0),
  CONSTRAINT ck_product_skus_sold_count CHECK (sold_count >= 0),
  CONSTRAINT ck_product_skus_low_stock_threshold CHECK (low_stock_threshold is null or low_stock_threshold >= 0),
  CONSTRAINT ck_product_skus_max_stock_threshold CHECK (max_stock_threshold is null or max_stock_threshold > 0),
  CONSTRAINT ck_product_skus_stock_threshold_range CHECK (low_stock_threshold is null or max_stock_threshold is null or max_stock_threshold >= low_stock_threshold),
  CONSTRAINT ck_product_skus_pc_build_service CHECK (not (is_pc_build and is_service)),
  CONSTRAINT ck_product_skus_average_cost CHECK (average_cost >= 0)
) WITH (fillfactor = 90);
CREATE UNIQUE INDEX ix_product_skus_sku ON product_skus (sku);
CREATE UNIQUE INDEX ix_product_skus_barcode ON product_skus (barcode);
CREATE UNIQUE INDEX ix_product_skus_product_id_attributes ON product_skus (product_id, attributes) NULLS NOT DISTINCT;
CREATE UNIQUE INDEX ix_product_skus_product_id_default ON product_skus (product_id) WHERE is_default;
CREATE INDEX ix_product_skus_product_id_price_active ON product_skus (product_id, price) WHERE is_active;
CREATE INDEX ix_product_skus_attributes ON product_skus USING gin (attributes jsonb_path_ops);
CREATE OR REPLACE VIEW product_with_effective_price AS
SELECT id AS sku_id,
       product_id,
       price AS effective_price,
       compare_at_price,
       CASE WHEN compare_at_price > price
            THEN round((compare_at_price - price) * 100.0 / compare_at_price) END AS discount_percent
FROM product_skus
WHERE is_active;
CREATE UNIQUE INDEX ix_product_skus_id_product_id ON product_skus (id, product_id);

CREATE TABLE product_images (
  id            uuid        NOT NULL DEFAULT uuidv7(),
  url           text        NOT NULL,
  display_order integer     NOT NULL DEFAULT 0,
  is_primary    boolean     NOT NULL DEFAULT false,
  created_by    uuid,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_by    uuid,
  updated_at    timestamptz NOT NULL DEFAULT now(),
  product_id    uuid        NOT NULL,
  sku_id        uuid,
  CONSTRAINT pk_product_images PRIMARY KEY (id),
  CONSTRAINT fk_product_images_products_product_id FOREIGN KEY (product_id) REFERENCES products (id) ON DELETE CASCADE,
  CONSTRAINT fk_product_images_product_skus_sku_id FOREIGN KEY (sku_id, product_id) REFERENCES product_skus (id, product_id) ON DELETE SET NULL (sku_id)
);
CREATE INDEX ix_product_images_product_id ON product_images (product_id);
CREATE INDEX ix_product_images_sku_id ON product_images (sku_id);
CREATE UNIQUE INDEX ix_product_images_product_id_primary ON product_images (product_id) WHERE is_primary;

CREATE TABLE pc_build_components (
  pc_sku_id        uuid        NOT NULL,
  component_sku_id uuid        NOT NULL,
  quantity         integer     NOT NULL DEFAULT 1,
  created_at       timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_pc_build_components PRIMARY KEY (pc_sku_id, component_sku_id),
  CONSTRAINT fk_pc_build_components_product_skus_pc_sku_id FOREIGN KEY (pc_sku_id) REFERENCES product_skus (id) ON DELETE CASCADE,
  CONSTRAINT fk_pc_build_components_product_skus_component_sku_id FOREIGN KEY (component_sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT ck_pc_build_components_quantity CHECK (quantity > 0),
  CONSTRAINT ck_pc_build_components_not_self CHECK (pc_sku_id <> component_sku_id)
);
CREATE INDEX ix_pc_build_components_component_sku_id ON pc_build_components (component_sku_id);

-- 5. Inventory & Purchasing

CREATE TABLE business_locations (
  id             uuid        NOT NULL DEFAULT uuidv7(),
  name           varchar     NOT NULL,
  phone          varchar(20) NOT NULL,
  email          varchar,
  address_detail text        NOT NULL,
  regular_hours  jsonb       NOT NULL,
  special_hours  jsonb,
  created_by     uuid,
  created_at     timestamptz NOT NULL DEFAULT now(),
  updated_by     uuid,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  province_id    integer     NOT NULL,
  ward_id        integer     NOT NULL,
  CONSTRAINT pk_business_locations PRIMARY KEY (id),
  CONSTRAINT fk_business_locations_provinces_province_id FOREIGN KEY (province_id) REFERENCES provinces (id) ON DELETE RESTRICT,
  CONSTRAINT fk_business_locations_wards_ward_id_province_id FOREIGN KEY (ward_id, province_id) REFERENCES wards (id, province_id) ON DELETE RESTRICT,
  CONSTRAINT ck_business_locations_regular_hours CHECK (jsonb_typeof(regular_hours) = 'array'),
  CONSTRAINT ck_business_locations_special_hours CHECK (special_hours is null or jsonb_typeof(special_hours) = 'array')
);
CREATE UNIQUE INDEX ix_business_locations_name ON business_locations (name);
CREATE INDEX ix_business_locations_province_id ON business_locations (province_id);
CREATE INDEX ix_business_locations_ward_id ON business_locations (ward_id);

CREATE TABLE warehouses (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  name       varchar     NOT NULL,
  type       varchar(20) NOT NULL,
  is_active  boolean     NOT NULL DEFAULT true,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_warehouses PRIMARY KEY (id)
);

CREATE TABLE suppliers (
  id           uuid        NOT NULL DEFAULT uuidv7(),
  name         varchar     NOT NULL,
  contact_info text,
  tax_code     varchar(13),
  opening_payable numeric(15,0) NOT NULL DEFAULT 0,
  is_active    boolean     NOT NULL DEFAULT true,
  created_by   uuid,
  created_at   timestamptz NOT NULL DEFAULT now(),
  updated_by   uuid,
  updated_at   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_suppliers PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_suppliers_tax_code ON suppliers (tax_code);

CREATE TABLE purchase_orders (
  id          uuid        NOT NULL DEFAULT uuidv7(),
  status      varchar(20) NOT NULL DEFAULT 'Draft',
  order_date  timestamptz NOT NULL DEFAULT now(),
  created_by  uuid,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_by  uuid,
  updated_at  timestamptz NOT NULL DEFAULT now(),
  supplier_id uuid        NOT NULL,
  CONSTRAINT pk_purchase_orders PRIMARY KEY (id),
  CONSTRAINT fk_purchase_orders_suppliers_supplier_id FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT
);
CREATE INDEX ix_purchase_orders_supplier_id ON purchase_orders (supplier_id);
CREATE INDEX ix_purchase_orders_status ON purchase_orders (status);
CREATE UNIQUE INDEX ix_purchase_orders_id_supplier_id ON purchase_orders (id, supplier_id);

CREATE TABLE purchase_order_items (
  id                uuid          NOT NULL DEFAULT uuidv7(),
  quantity_ordered  integer       NOT NULL,
  quantity_received integer       NOT NULL DEFAULT 0,
  unit_cost         numeric(15,0) NOT NULL,
  vat_rate          numeric(5,2)  NOT NULL,
  created_by        uuid,
  created_at        timestamptz   NOT NULL DEFAULT now(),
  updated_by        uuid,
  updated_at        timestamptz   NOT NULL DEFAULT now(),
  purchase_order_id uuid          NOT NULL,
  sku_id            uuid          NOT NULL,
  CONSTRAINT pk_purchase_order_items PRIMARY KEY (id),
  CONSTRAINT fk_purchase_order_items_purchase_orders_purchase_order_id FOREIGN KEY (purchase_order_id) REFERENCES purchase_orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_purchase_order_items_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT ck_purchase_order_items_quantity_received_quantity_ordered CHECK (quantity_received <= quantity_ordered),
  CONSTRAINT ck_purchase_order_items_quantity_ordered CHECK (quantity_ordered > 0),
  CONSTRAINT ck_purchase_order_items_quantity_received CHECK (quantity_received >= 0),
  CONSTRAINT ck_purchase_order_items_unit_cost CHECK (unit_cost >= 0),
  CONSTRAINT ck_purchase_order_items_vat_rate CHECK (vat_rate >= 0 and vat_rate <= 100)
);
CREATE UNIQUE INDEX ix_purchase_order_items_purchase_order_id_sku_id_unit_cost ON purchase_order_items (purchase_order_id, sku_id, unit_cost);
CREATE INDEX ix_purchase_order_items_sku_id ON purchase_order_items (sku_id);

CREATE TABLE purchase_receipts (
  id                uuid        NOT NULL DEFAULT uuidv7(),
  quantity          integer     NOT NULL,
  created_by        uuid,
  created_at        timestamptz NOT NULL DEFAULT now(),
  po_item_id        uuid        NOT NULL,
  CONSTRAINT pk_purchase_receipts PRIMARY KEY (id),
  CONSTRAINT fk_purchase_receipts_purchase_order_items_po_item_id FOREIGN KEY (po_item_id) REFERENCES purchase_order_items (id) ON DELETE RESTRICT,
  CONSTRAINT ck_purchase_receipts_quantity CHECK (quantity > 0)
);
CREATE INDEX ix_purchase_receipts_po_item_id ON purchase_receipts (po_item_id);

CREATE TABLE stock_balances (
  sku_id       uuid        NOT NULL,
  warehouse_id uuid        NOT NULL,
  quantity     integer     NOT NULL DEFAULT 0,
  reserved_quantity integer NOT NULL DEFAULT 0,
  created_at   timestamptz NOT NULL DEFAULT now(),
  updated_by   uuid,
  updated_at   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_stock_balances PRIMARY KEY (sku_id, warehouse_id),
  CONSTRAINT fk_stock_balances_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_balances_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses (id) ON DELETE RESTRICT,
  CONSTRAINT ck_stock_balances_quantity CHECK (quantity >= 0),
  CONSTRAINT ck_stock_balances_reserved_quantity CHECK (reserved_quantity >= 0)
) WITH (fillfactor = 90);
CREATE INDEX ix_stock_balances_warehouse_id ON stock_balances (warehouse_id);

CREATE TABLE serial_units (
  id                uuid         NOT NULL DEFAULT uuidv7(),
  serial_number     varchar(100) NOT NULL,
  status            varchar(25)  NOT NULL DEFAULT 'IN_STOCK',
  received_at       timestamptz  NOT NULL,
  condition         varchar(20)  NOT NULL DEFAULT 'NEW',
  condition_note    text,
  resale_price      numeric(15,0),
  resale_warranty_months integer,
  created_by        uuid,
  created_at        timestamptz  NOT NULL DEFAULT now(),
  updated_by        uuid,
  updated_at        timestamptz  NOT NULL DEFAULT now(),
  sku_id            uuid         NOT NULL,
  warehouse_id      uuid,
  supplier_id       uuid,
  purchase_order_id uuid,
  parent_serial_unit_id uuid,
  CONSTRAINT pk_serial_units PRIMARY KEY (id),
  CONSTRAINT fk_serial_units_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_serial_units_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses (id) ON DELETE RESTRICT,
  CONSTRAINT fk_serial_units_suppliers_supplier_id FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT,
  CONSTRAINT fk_serial_units_purchase_orders_purchase_order_id FOREIGN KEY (purchase_order_id, supplier_id) REFERENCES purchase_orders (id, supplier_id) ON DELETE RESTRICT,
  CONSTRAINT fk_serial_units_serial_units_parent_serial_unit_id FOREIGN KEY (parent_serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT ck_serial_units_in_store_warehouse CHECK (status not in ('IN_STOCK', 'RESERVED', 'DEFECTIVE') or warehouse_id is not null),
  CONSTRAINT ck_serial_units_out_of_store_warehouse CHECK (status not in ('SOLD', 'ASSEMBLED', 'IN_WARRANTY', 'RETURNED_TO_SUPPLIER', 'LOST') or warehouse_id is null),
  CONSTRAINT ck_serial_units_parent_serial_unit_id CHECK ((status = 'ASSEMBLED') = (parent_serial_unit_id is not null)),
  CONSTRAINT ck_serial_units_parent_not_self CHECK (parent_serial_unit_id is null or parent_serial_unit_id <> id),
  CONSTRAINT ck_serial_units_new_no_resale CHECK (condition <> 'NEW' or (resale_price is null and resale_warranty_months is null)),
  CONSTRAINT ck_serial_units_resale_pair CHECK ((resale_price is null) = (resale_warranty_months is null)),
  CONSTRAINT ck_serial_units_resale_price CHECK (resale_price is null or resale_price > 0),
  CONSTRAINT ck_serial_units_resale_warranty_months CHECK (resale_warranty_months is null or resale_warranty_months >= 0)
);
CREATE UNIQUE INDEX ix_serial_units_sku_id_serial_number ON serial_units (sku_id, serial_number);
CREATE INDEX ix_serial_units_sku_id_status ON serial_units (sku_id, status);
CREATE INDEX ix_serial_units_serial_number ON serial_units (serial_number);
CREATE INDEX ix_serial_units_warehouse_id ON serial_units (warehouse_id);
CREATE INDEX ix_serial_units_supplier_id ON serial_units (supplier_id);
CREATE INDEX ix_serial_units_purchase_order_id ON serial_units (purchase_order_id);
CREATE INDEX ix_serial_units_parent_serial_unit_id ON serial_units (parent_serial_unit_id);
CREATE UNIQUE INDEX ix_serial_units_id_sku_id ON serial_units (id, sku_id);

CREATE TABLE stock_ledger_entries (
  id              uuid        NOT NULL DEFAULT uuidv7(),
  quantity_change integer     NOT NULL,
  balance_after   integer     NOT NULL,
  unit_cost       numeric(15,0),
  movement_type   varchar(30) NOT NULL,
  reference_type  varchar(30),
  reference_id    uuid,
  note            text,
  created_by      uuid,
  created_at      timestamptz NOT NULL DEFAULT now(),
  sku_id          uuid        NOT NULL,
  warehouse_id    uuid        NOT NULL,
  serial_unit_id  uuid,
  CONSTRAINT pk_stock_ledger_entries PRIMARY KEY (id),
  CONSTRAINT fk_stock_ledger_entries_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_ledger_entries_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_ledger_entries_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id, sku_id) REFERENCES serial_units (id, sku_id) ON DELETE RESTRICT,
  CONSTRAINT ck_stock_ledger_entries_quantity_change CHECK (quantity_change <> 0),
  CONSTRAINT ck_stock_ledger_entries_balance_after CHECK (balance_after >= 0),
  CONSTRAINT ck_stock_ledger_entries_serial_unit_id CHECK (serial_unit_id is null or quantity_change in (1, -1)),
  CONSTRAINT ck_stock_ledger_entries_unit_cost CHECK (unit_cost is null or unit_cost >= 0)
);
CREATE INDEX ix_stock_ledger_entries_sku_id_warehouse_id_created_at ON stock_ledger_entries (sku_id, warehouse_id, created_at);
CREATE INDEX ix_stock_ledger_entries_warehouse_id ON stock_ledger_entries (warehouse_id);
CREATE INDEX ix_stock_ledger_entries_reference_type_reference_id ON stock_ledger_entries (reference_type, reference_id);
CREATE INDEX ix_stock_ledger_entries_serial_unit_id_created_at ON stock_ledger_entries (serial_unit_id, created_at);

CREATE TABLE adjustment_reasons (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  key        varchar(50) NOT NULL,
  label      varchar     NOT NULL,
  is_active  boolean     NOT NULL DEFAULT true,
  created_by uuid,
  created_at timestamptz NOT NULL DEFAULT now(),
  updated_by uuid,
  updated_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_adjustment_reasons PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_adjustment_reasons_key ON adjustment_reasons (key);

CREATE TABLE stock_adjustments (
  id              uuid        NOT NULL DEFAULT uuidv7(),
  quantity_change integer     NOT NULL,
  status          varchar(20) NOT NULL DEFAULT 'Requested',
  created_by      uuid,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_by      uuid,
  updated_at      timestamptz NOT NULL DEFAULT now(),
  sku_id          uuid        NOT NULL,
  warehouse_id    uuid        NOT NULL,
  reason_id       uuid        NOT NULL,
  serial_unit_id  uuid,
  CONSTRAINT pk_stock_adjustments PRIMARY KEY (id),
  CONSTRAINT fk_stock_adjustments_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_adjustments_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_adjustments_adjustment_reasons_reason_id FOREIGN KEY (reason_id) REFERENCES adjustment_reasons (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_adjustments_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id, sku_id) REFERENCES serial_units (id, sku_id) ON DELETE RESTRICT,
  CONSTRAINT ck_stock_adjustments_quantity_change CHECK (quantity_change <> 0)
);
CREATE INDEX ix_stock_adjustments_sku_id ON stock_adjustments (sku_id);
CREATE INDEX ix_stock_adjustments_status ON stock_adjustments (status);
CREATE INDEX ix_stock_adjustments_warehouse_id ON stock_adjustments (warehouse_id);
CREATE INDEX ix_stock_adjustments_reason_id ON stock_adjustments (reason_id);
CREATE INDEX ix_stock_adjustments_serial_unit_id ON stock_adjustments (serial_unit_id);

CREATE TABLE stock_counts (
  id             uuid        NOT NULL DEFAULT uuidv7(),
  status         varchar(20) NOT NULL DEFAULT 'Draft',
  note           text,
  completed_at   timestamptz,
  completed_by   uuid,
  created_by     uuid,
  created_at     timestamptz NOT NULL DEFAULT now(),
  updated_by     uuid,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  warehouse_id   uuid        NOT NULL,
  merged_into_id uuid,
  CONSTRAINT pk_stock_counts PRIMARY KEY (id),
  CONSTRAINT fk_stock_counts_warehouses_warehouse_id FOREIGN KEY (warehouse_id) REFERENCES warehouses (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_counts_stock_counts_merged_into_id FOREIGN KEY (merged_into_id) REFERENCES stock_counts (id) ON DELETE RESTRICT,
  CONSTRAINT ck_stock_counts_completed_at CHECK ((status = 'Completed') = (completed_at is not null)),
  CONSTRAINT ck_stock_counts_merged_into_id CHECK (merged_into_id is null or status = 'Cancelled')
);
CREATE INDEX ix_stock_counts_warehouse_id_status ON stock_counts (warehouse_id, status);
CREATE INDEX ix_stock_counts_merged_into_id ON stock_counts (merged_into_id);

CREATE TABLE stock_count_lines (
  id               uuid         NOT NULL DEFAULT uuidv7(),
  counted_quantity integer      NOT NULL,
  serial_number    varchar(100),
  book_quantity    integer,
  note             text,
  created_by       uuid,
  created_at       timestamptz  NOT NULL DEFAULT now(),
  updated_by       uuid,
  updated_at       timestamptz  NOT NULL DEFAULT now(),
  stock_count_id   uuid         NOT NULL,
  sku_id           uuid         NOT NULL,
  CONSTRAINT pk_stock_count_lines PRIMARY KEY (id),
  CONSTRAINT fk_stock_count_lines_stock_counts_stock_count_id FOREIGN KEY (stock_count_id) REFERENCES stock_counts (id) ON DELETE RESTRICT,
  CONSTRAINT fk_stock_count_lines_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT ck_stock_count_lines_counted_quantity CHECK (counted_quantity >= 0),
  CONSTRAINT ck_stock_count_lines_serial_number CHECK (serial_number is null or counted_quantity = 1),
  CONSTRAINT ck_stock_count_lines_book_quantity CHECK (book_quantity is null or book_quantity >= 0)
);
CREATE UNIQUE INDEX ix_stock_count_lines_stock_count_id_sku_id_serial_number ON stock_count_lines (stock_count_id, sku_id, serial_number) NULLS NOT DISTINCT;
CREATE INDEX ix_stock_count_lines_sku_id ON stock_count_lines (sku_id);

CREATE TABLE supplier_payments (
  id                 uuid          NOT NULL DEFAULT uuidv7(),
  type               varchar(10)   NOT NULL DEFAULT 'PAYMENT',
  method             varchar(20)   NOT NULL,
  amount             numeric(15,0) NOT NULL,
  reference          varchar(100),
  paid_at            timestamptz   NOT NULL DEFAULT now(),
  note               text,
  created_by         uuid,
  created_at         timestamptz   NOT NULL DEFAULT now(),
  supplier_id        uuid          NOT NULL,
  purchase_order_id  uuid,
  CONSTRAINT pk_supplier_payments PRIMARY KEY (id),
  CONSTRAINT fk_supplier_payments_suppliers_supplier_id FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT,
  CONSTRAINT fk_supplier_payments_purchase_orders_purchase_order_id FOREIGN KEY (purchase_order_id, supplier_id) REFERENCES purchase_orders (id, supplier_id) ON DELETE RESTRICT,
  CONSTRAINT ck_supplier_payments_amount CHECK (amount > 0),
  CONSTRAINT ck_supplier_payments_method CHECK (method = 'CASH' or reference is not null)
);
CREATE INDEX ix_supplier_payments_supplier_id_paid_at ON supplier_payments (supplier_id, paid_at);
CREATE INDEX ix_supplier_payments_purchase_order_id ON supplier_payments (purchase_order_id);

-- 7. Orders

CREATE TABLE orders (
  id                  uuid          NOT NULL DEFAULT uuidv7(),
  order_code          varchar(20)   NOT NULL,
  type                varchar(20)   NOT NULL,
  fulfillment_method  varchar(10)   NOT NULL,
  recipient_name      varchar       NOT NULL,
  contact_phone       varchar(20)   NOT NULL,
  status              varchar(20)   NOT NULL DEFAULT 'Pending',
  subtotal            numeric(15,0) NOT NULL,
  discount_amount     numeric(15,0) NOT NULL DEFAULT 0,
  manual_discount_amount numeric(15,0) NOT NULL DEFAULT 0,
  manual_discount_reason text,
  manual_discount_by  uuid,
  shipping_fee        numeric(15,0) NOT NULL DEFAULT 0,
  total_amount        numeric(15,0) NOT NULL,
  shipping_vat_rate   numeric(5,2)  NOT NULL DEFAULT 0,
  idempotency_key     uuid,
  completed_at        timestamptz,
  created_by          uuid,
  created_at          timestamptz   NOT NULL DEFAULT now(),
  updated_by          uuid,
  updated_at          timestamptz   NOT NULL DEFAULT now(),
  customer_id         uuid          NOT NULL,
  sales_staff_id      uuid,
  CONSTRAINT pk_orders PRIMARY KEY (id),
  CONSTRAINT fk_orders_users_customer_id FOREIGN KEY (customer_id) REFERENCES users (id) ON DELETE RESTRICT,
  CONSTRAINT fk_orders_users_sales_staff_id FOREIGN KEY (sales_staff_id) REFERENCES users (id) ON DELETE RESTRICT,
  CONSTRAINT ck_orders_subtotal CHECK (subtotal >= 0),
  CONSTRAINT ck_orders_discount_amount CHECK (discount_amount >= 0),
  CONSTRAINT ck_orders_shipping_fee CHECK (shipping_fee >= 0),
  CONSTRAINT ck_orders_discount_amount_subtotal CHECK (discount_amount <= subtotal),
  CONSTRAINT ck_orders_total_amount CHECK (total_amount = subtotal - discount_amount + shipping_fee),
  CONSTRAINT ck_orders_shipping_vat_rate CHECK (shipping_vat_rate >= 0 and shipping_vat_rate <= 100),
  CONSTRAINT ck_orders_completed_at CHECK ((status = 'Completed') = (completed_at is not null)),
  CONSTRAINT ck_orders_manual_discount_amount CHECK (manual_discount_amount >= 0 and manual_discount_amount <= discount_amount),
  CONSTRAINT ck_orders_manual_discount_reason CHECK ((manual_discount_amount > 0) = (manual_discount_reason is not null)),
  CONSTRAINT ck_orders_manual_discount_by CHECK (manual_discount_amount = 0 or manual_discount_by is not null),
  CONSTRAINT ck_orders_discount_manual_only CHECK (discount_amount = manual_discount_amount)
);
CREATE UNIQUE INDEX ix_orders_order_code ON orders (order_code);
CREATE INDEX ix_orders_customer_id_created_at ON orders (customer_id, created_at);
CREATE INDEX ix_orders_status_created_at ON orders (status, created_at);
CREATE INDEX ix_orders_sales_staff_id ON orders (sales_staff_id);
CREATE UNIQUE INDEX ix_orders_customer_id_idempotency_key ON orders (customer_id, idempotency_key);
CREATE INDEX ix_orders_completed_at ON orders (completed_at);
CREATE UNIQUE INDEX ix_orders_id_customer_id ON orders (id, customer_id);

CREATE TABLE order_invoice_details (
  order_id     uuid         NOT NULL,
  buyer_type   varchar(20)  NOT NULL DEFAULT 'COMPANY',
  company_name varchar      NOT NULL,
  tax_code     varchar(14)  NOT NULL,
  address      text         NOT NULL,
  email        varchar(255),
  created_at   timestamptz  NOT NULL DEFAULT now(),
  CONSTRAINT pk_order_invoice_details PRIMARY KEY (order_id),
  CONSTRAINT fk_order_invoice_details_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT ck_order_invoice_details_tax_code CHECK (tax_code ~ '^([0-9]{10}(-?[0-9]{3})?|[0-9]{12})$'),
  CONSTRAINT ck_order_invoice_details_buyer_type CHECK ((buyer_type = 'HOUSEHOLD_BUSINESS') = (tax_code ~ '^[0-9]{12}$'))
);
CREATE INDEX ix_order_invoice_details_tax_code ON order_invoice_details (tax_code);

CREATE TABLE order_items (
  id                 uuid          NOT NULL DEFAULT uuidv7(),
  quantity           integer       NOT NULL,
  unit_price         numeric(15,0) NOT NULL,
  product_name       varchar       NOT NULL,
  sku_code           varchar       NOT NULL,
  variant_attributes jsonb,
  warranty_months    integer,
  discount_allocated numeric(15,0) NOT NULL DEFAULT 0,
  vat_rate           numeric(5,2)  NOT NULL,
  unit_cost          numeric(15,0),
  item_condition     varchar(20)   NOT NULL DEFAULT 'NEW',
  pc_fulfillment     varchar(10),
  created_at         timestamptz   NOT NULL DEFAULT now(),
  order_id           uuid          NOT NULL,
  sku_id             uuid          NOT NULL,
  serial_unit_id     uuid,
  CONSTRAINT pk_order_items PRIMARY KEY (id),
  CONSTRAINT fk_order_items_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_items_product_skus_sku_id FOREIGN KEY (sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_items_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT ck_order_items_quantity CHECK (quantity > 0),
  CONSTRAINT ck_order_items_unit_price CHECK (unit_price >= 0),
  CONSTRAINT ck_order_items_warranty_months CHECK (warranty_months is null or warranty_months >= 0),
  CONSTRAINT ck_order_items_discount_allocated CHECK (discount_allocated >= 0),
  CONSTRAINT ck_order_items_discount_allocated_quantity CHECK (discount_allocated <= quantity * unit_price),
  CONSTRAINT ck_order_items_vat_rate CHECK (vat_rate >= 0 and vat_rate <= 100),
  CONSTRAINT ck_order_items_unit_cost CHECK (unit_cost is null or unit_cost >= 0),
  CONSTRAINT ck_order_items_item_condition CHECK ((item_condition = 'NEW') = (serial_unit_id is null)),
  CONSTRAINT ck_order_items_serial_unit_id CHECK (serial_unit_id is null or quantity = 1),
  CONSTRAINT ck_order_items_pc_fulfillment CHECK (pc_fulfillment is null or pc_fulfillment in ('STOCK', 'BUILD'))
);
CREATE INDEX ix_order_items_order_id ON order_items (order_id);
CREATE INDEX ix_order_items_sku_id ON order_items (sku_id);
CREATE INDEX ix_order_items_serial_unit_id ON order_items (serial_unit_id);

CREATE TABLE order_item_serials (
  id             uuid        NOT NULL DEFAULT uuidv7(),
  created_by     uuid,
  created_at     timestamptz NOT NULL DEFAULT now(),
  order_item_id  uuid        NOT NULL,
  serial_unit_id uuid        NOT NULL,
  CONSTRAINT pk_order_item_serials PRIMARY KEY (id),
  CONSTRAINT fk_order_item_serials_order_items_order_item_id FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_item_serials_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT
);
CREATE INDEX ix_order_item_serials_order_item_id ON order_item_serials (order_item_id);
CREATE INDEX ix_order_item_serials_serial_unit_id ON order_item_serials (serial_unit_id);
CREATE UNIQUE INDEX ix_order_item_serials_id_order_item_id ON order_item_serials (id, order_item_id);

CREATE TABLE order_item_components (
  id               uuid          NOT NULL DEFAULT uuidv7(),
  quantity         integer       NOT NULL,
  unit_price       numeric(15,0) NOT NULL,
  created_at       timestamptz   NOT NULL DEFAULT now(),
  order_item_id    uuid          NOT NULL,
  component_sku_id uuid          NOT NULL,
  replaced_sku_id  uuid,
  CONSTRAINT pk_order_item_components PRIMARY KEY (id),
  CONSTRAINT fk_order_item_components_order_items_order_item_id FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_item_components_product_skus_component_sku_id FOREIGN KEY (component_sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_item_components_product_skus_replaced_sku_id FOREIGN KEY (replaced_sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT ck_order_item_components_quantity CHECK (quantity > 0),
  CONSTRAINT ck_order_item_components_unit_price CHECK (unit_price >= 0)
);
CREATE INDEX ix_order_item_components_order_item_id ON order_item_components (order_item_id);
CREATE INDEX ix_order_item_components_component_sku_id ON order_item_components (component_sku_id);
CREATE INDEX ix_order_item_components_replaced_sku_id ON order_item_components (replaced_sku_id);

CREATE TABLE pc_assemblies (
  id                uuid          NOT NULL DEFAULT uuidv7(),
  code              varchar(20)   NOT NULL,
  status            varchar(20)   NOT NULL DEFAULT 'Draft',
  unit_cost         numeric(15,0),
  completed_at      timestamptz,
  created_by        uuid,
  created_at        timestamptz   NOT NULL DEFAULT now(),
  updated_by        uuid,
  updated_at        timestamptz   NOT NULL DEFAULT now(),
  pc_sku_id         uuid          NOT NULL,
  order_item_id     uuid,
  pc_serial_unit_id uuid,
  assembled_by      uuid,
  CONSTRAINT pk_pc_assemblies PRIMARY KEY (id),
  CONSTRAINT fk_pc_assemblies_product_skus_pc_sku_id FOREIGN KEY (pc_sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_pc_assemblies_order_items_order_item_id FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
  CONSTRAINT fk_pc_assemblies_serial_units_pc_serial_unit_id FOREIGN KEY (pc_serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT fk_pc_assemblies_users_assembled_by FOREIGN KEY (assembled_by) REFERENCES users (id) ON DELETE RESTRICT,
  CONSTRAINT ck_pc_assemblies_completed_at CHECK ((status = 'Completed') = (completed_at is not null)),
  CONSTRAINT ck_pc_assemblies_completed_output CHECK (status <> 'Completed' or (pc_serial_unit_id is not null and unit_cost is not null)),
  CONSTRAINT ck_pc_assemblies_unit_cost CHECK (unit_cost is null or unit_cost >= 0)
);
CREATE UNIQUE INDEX ix_pc_assemblies_code ON pc_assemblies (code);
CREATE INDEX ix_pc_assemblies_pc_sku_id ON pc_assemblies (pc_sku_id);
CREATE INDEX ix_pc_assemblies_order_item_id ON pc_assemblies (order_item_id);
CREATE UNIQUE INDEX ix_pc_assemblies_pc_serial_unit_id ON pc_assemblies (pc_serial_unit_id);
CREATE INDEX ix_pc_assemblies_assembled_by ON pc_assemblies (assembled_by);
CREATE INDEX ix_pc_assemblies_status ON pc_assemblies (status);

CREATE TABLE pc_assembly_components (
  id               uuid          NOT NULL DEFAULT uuidv7(),
  quantity         integer       NOT NULL,
  unit_cost        numeric(15,0),
  warranty_months  integer,
  created_at       timestamptz   NOT NULL DEFAULT now(),
  assembly_id      uuid          NOT NULL,
  component_sku_id uuid          NOT NULL,
  serial_unit_id   uuid,
  CONSTRAINT pk_pc_assembly_components PRIMARY KEY (id),
  CONSTRAINT fk_pc_assembly_components_pc_assemblies_assembly_id FOREIGN KEY (assembly_id) REFERENCES pc_assemblies (id) ON DELETE RESTRICT,
  CONSTRAINT fk_pc_assembly_components_product_skus_component_sku_id FOREIGN KEY (component_sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_pc_assembly_components_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT ck_pc_assembly_components_quantity CHECK (quantity > 0),
  CONSTRAINT ck_pc_assembly_components_serial_unit_id CHECK (serial_unit_id is null or quantity = 1),
  CONSTRAINT ck_pc_assembly_components_unit_cost CHECK (unit_cost is null or unit_cost >= 0),
  CONSTRAINT ck_pc_assembly_components_warranty_months CHECK (warranty_months is null or warranty_months >= 0)
);
CREATE INDEX ix_pc_assembly_components_assembly_id ON pc_assembly_components (assembly_id);
CREATE INDEX ix_pc_assembly_components_component_sku_id ON pc_assembly_components (component_sku_id);
CREATE INDEX ix_pc_assembly_components_serial_unit_id ON pc_assembly_components (serial_unit_id);

-- 8. After-sales

CREATE TABLE returns (
  id                uuid          NOT NULL DEFAULT uuidv7(),
  reason            text          NOT NULL,
  status            varchar(20)   NOT NULL DEFAULT 'Requested',
  resolution_type   varchar(20),
  refund_amount     numeric(15,0),
  restocking_fee    numeric(15,0) NOT NULL DEFAULT 0,
  restocking_fee_suggested numeric(15,0),
  fee_override_reason      text,
  return_value      numeric(15,0) NOT NULL DEFAULT 0,
  completed_at      timestamptz,
  created_by        uuid,
  created_at        timestamptz   NOT NULL DEFAULT now(),
  updated_by        uuid,
  updated_at        timestamptz   NOT NULL DEFAULT now(),
  order_id          uuid          NOT NULL,
  exchange_order_id uuid,
  CONSTRAINT pk_returns PRIMARY KEY (id),
  CONSTRAINT fk_returns_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_returns_orders_exchange_order_id FOREIGN KEY (exchange_order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT ck_returns_refund_amount CHECK (refund_amount is null or refund_amount >= 0),
  CONSTRAINT ck_returns_restocking_fee CHECK (restocking_fee >= 0),
  CONSTRAINT ck_returns_fee_override_reason CHECK (restocking_fee_suggested is null or restocking_fee = restocking_fee_suggested or fee_override_reason is not null),
  CONSTRAINT ck_returns_return_value CHECK (return_value >= 0 and restocking_fee <= return_value),
  CONSTRAINT ck_returns_refund_amount_return_value CHECK (refund_amount is null or refund_amount = return_value - restocking_fee),
  CONSTRAINT ck_returns_completed_at CHECK ((status = 'Completed') = (completed_at is not null)),
  CONSTRAINT ck_returns_exchange_order_id CHECK (exchange_order_id is null or exchange_order_id <> order_id)
);
CREATE INDEX ix_returns_order_id ON returns (order_id);
CREATE INDEX ix_returns_status ON returns (status);
CREATE INDEX ix_returns_completed_at ON returns (completed_at);
CREATE INDEX ix_returns_exchange_order_id ON returns (exchange_order_id);
CREATE UNIQUE INDEX ix_returns_id_order_id ON returns (id, order_id);

CREATE TABLE return_items (
  id                   uuid        NOT NULL DEFAULT uuidv7(),
  quantity             integer     NOT NULL,
  return_value         numeric(15,0) NOT NULL DEFAULT 0,
  restock_to           varchar(20),
  created_at           timestamptz NOT NULL DEFAULT now(),
  updated_by           uuid,
  updated_at           timestamptz NOT NULL DEFAULT now(),
  return_id            uuid        NOT NULL,
  order_item_id        uuid        NOT NULL,
  order_item_serial_id uuid,
  CONSTRAINT pk_return_items PRIMARY KEY (id),
  CONSTRAINT fk_return_items_returns_return_id FOREIGN KEY (return_id) REFERENCES returns (id) ON DELETE RESTRICT,
  CONSTRAINT fk_return_items_order_items_order_item_id FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
  CONSTRAINT fk_return_items_order_item_serials_order_item_serial_id FOREIGN KEY (order_item_serial_id, order_item_id) REFERENCES order_item_serials (id, order_item_id) ON DELETE RESTRICT,
  CONSTRAINT ck_return_items_quantity CHECK (quantity > 0),
  CONSTRAINT ck_return_items_return_value CHECK (return_value >= 0),
  CONSTRAINT ck_return_items_restock_to CHECK (restock_to <> 'OUTLET' or order_item_serial_id is not null)
);
CREATE INDEX ix_return_items_return_id ON return_items (return_id);
CREATE INDEX ix_return_items_order_item_id ON return_items (order_item_id);
CREATE INDEX ix_return_items_order_item_serial_id ON return_items (order_item_serial_id);

CREATE TABLE warranty_requests (
  id                         uuid        NOT NULL DEFAULT uuidv7(),
  code                       varchar(20) NOT NULL,
  request_type               varchar(20) NOT NULL,
  intake_method              varchar(20) NOT NULL DEFAULT 'IN_STORE',
  inbound_tracking_code      varchar(50),
  return_method              varchar(20),
  outbound_tracking_code     varchar(50),
  customer_issue             text        NOT NULL,
  physical_condition         text        NOT NULL,
  accessories_included       text,
  seal_intact                boolean,
  temporary_solution         text,
  status                     varchar(20) NOT NULL DEFAULT 'Requested',
  resolution_type            varchar(20),
  buyback_amount             numeric(15,0),
  technical_notes            text,
  supplier_rma_code          varchar,
  expected_return_date       date        NOT NULL,
  resolved_at                timestamptz,
  replacement_warranty_ends_at date,
  created_by                 uuid,
  created_at                 timestamptz NOT NULL DEFAULT now(),
  updated_by                 uuid,
  updated_at                 timestamptz NOT NULL DEFAULT now(),
  order_item_serial_id       uuid,
  assigned_to                uuid,
  supplier_id                uuid,
  order_item_id              uuid        NOT NULL,
  replacement_serial_unit_id uuid,
  replacement_sku_id         uuid,
  component_serial_unit_id   uuid,
  CONSTRAINT pk_warranty_requests PRIMARY KEY (id),
  CONSTRAINT fk_warranty_requests_order_item_serials_order_item_serial_id FOREIGN KEY (order_item_serial_id, order_item_id) REFERENCES order_item_serials (id, order_item_id) ON DELETE RESTRICT,
  CONSTRAINT fk_warranty_requests_order_items_order_item_id FOREIGN KEY (order_item_id) REFERENCES order_items (id) ON DELETE RESTRICT,
  CONSTRAINT fk_warranty_requests_serial_units_replacement_serial_unit_id FOREIGN KEY (replacement_serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT fk_warranty_requests_users_assigned_to FOREIGN KEY (assigned_to) REFERENCES users (id) ON DELETE RESTRICT,
  CONSTRAINT fk_warranty_requests_suppliers_supplier_id FOREIGN KEY (supplier_id) REFERENCES suppliers (id) ON DELETE RESTRICT,
  CONSTRAINT fk_warranty_requests_product_skus_replacement_sku_id FOREIGN KEY (replacement_sku_id) REFERENCES product_skus (id) ON DELETE RESTRICT,
  CONSTRAINT fk_warranty_requests_serial_units_component_serial_unit_id FOREIGN KEY (component_serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT ck_warranty_requests_inbound_tracking_code CHECK (intake_method = 'SHIPPED_IN' or inbound_tracking_code is null),
  CONSTRAINT ck_warranty_requests_resolved_at CHECK ((status in ('Resolved', 'Rejected')) = (resolved_at is not null)),
  CONSTRAINT ck_warranty_requests_resolution_type CHECK (status not in ('Resolved', 'Rejected') or resolution_type is not null),
  CONSTRAINT ck_warranty_requests_rejected CHECK ((status = 'Rejected') = (resolution_type = 'REJECTED')),
  CONSTRAINT ck_warranty_requests_replacement_sku_id CHECK ((resolution_type = 'REPLACED') = (replacement_sku_id is not null)),
  CONSTRAINT ck_warranty_requests_buyback_amount CHECK ((resolution_type = 'BUYBACK') = (buyback_amount is not null)),
  CONSTRAINT ck_warranty_requests_buyback_amount_positive CHECK (buyback_amount is null or buyback_amount > 0),
  CONSTRAINT ck_warranty_requests_outbound_tracking_code CHECK (return_method = 'SHIPPED' or outbound_tracking_code is null),
  CONSTRAINT ck_warranty_requests_replacement_warranty_ends_at CHECK ((resolution_type = 'REPLACED') = (replacement_warranty_ends_at is not null))
);
CREATE UNIQUE INDEX ix_warranty_requests_code ON warranty_requests (code);
CREATE INDEX ix_warranty_requests_order_item_serial_id ON warranty_requests (order_item_serial_id);
CREATE INDEX ix_warranty_requests_status ON warranty_requests (status);
CREATE INDEX ix_warranty_requests_assigned_to ON warranty_requests (assigned_to);
CREATE INDEX ix_warranty_requests_supplier_id ON warranty_requests (supplier_id);
CREATE INDEX ix_warranty_requests_order_item_id ON warranty_requests (order_item_id);
CREATE INDEX ix_warranty_requests_replacement_serial_unit_id ON warranty_requests (replacement_serial_unit_id);
CREATE INDEX ix_warranty_requests_replacement_sku_id ON warranty_requests (replacement_sku_id);
CREATE INDEX ix_warranty_requests_component_serial_unit_id ON warranty_requests (component_serial_unit_id);
CREATE INDEX ix_warranty_requests_resolved_at ON warranty_requests (resolved_at);

-- 9. Payments & POS

CREATE TABLE shifts (
  id                    uuid          NOT NULL DEFAULT uuidv7(),
  opening_cash          numeric(15,0) NOT NULL,
  closing_cash_expected numeric(15,0),
  closing_cash_actual   numeric(15,0),
  status                varchar(20)   NOT NULL DEFAULT 'Open',
  created_at            timestamptz   NOT NULL DEFAULT now(),
  updated_by            uuid,
  updated_at            timestamptz   NOT NULL DEFAULT now(),
  staff_id              uuid          NOT NULL,
  CONSTRAINT pk_shifts PRIMARY KEY (id),
  CONSTRAINT fk_shifts_users_staff_id FOREIGN KEY (staff_id) REFERENCES users (id) ON DELETE RESTRICT,
  CONSTRAINT ck_shifts_opening_cash CHECK (opening_cash >= 0),
  CONSTRAINT ck_shifts_closing_cash_expected CHECK (closing_cash_expected is null or closing_cash_expected >= 0),
  CONSTRAINT ck_shifts_closing_cash_actual CHECK (closing_cash_actual is null or closing_cash_actual >= 0)
);
CREATE INDEX ix_shifts_staff_id ON shifts (staff_id);
CREATE UNIQUE INDEX ix_shifts_staff_id_open ON shifts (staff_id) WHERE status = 'Open';

CREATE TABLE cash_accounts (
  id             uuid        NOT NULL DEFAULT uuidv7(),
  name           varchar     NOT NULL,
  type           varchar(10) NOT NULL,
  bank_name      varchar,
  account_number varchar(30),
  is_default     boolean     NOT NULL DEFAULT false,
  is_active      boolean     NOT NULL DEFAULT true,
  created_by     uuid,
  created_at     timestamptz NOT NULL DEFAULT now(),
  updated_by     uuid,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_cash_accounts PRIMARY KEY (id),
  CONSTRAINT ck_cash_accounts_bank CHECK (type <> 'BANK' or (bank_name is not null and account_number is not null))
);
CREATE UNIQUE INDEX ix_cash_accounts_name ON cash_accounts (name);
CREATE UNIQUE INDEX ix_cash_accounts_type_default ON cash_accounts (type) WHERE is_default;

CREATE TABLE cash_voucher_categories (
  id                      uuid        NOT NULL DEFAULT uuidv7(),
  name                    varchar     NOT NULL,
  direction               varchar(10) NOT NULL,
  affects_business_result boolean     NOT NULL DEFAULT true,
  is_system               boolean     NOT NULL DEFAULT false,
  is_active               boolean     NOT NULL DEFAULT true,
  created_by              uuid,
  created_at              timestamptz NOT NULL DEFAULT now(),
  updated_by              uuid,
  updated_at              timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_cash_voucher_categories PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ix_cash_voucher_categories_direction_name ON cash_voucher_categories (direction, name);

CREATE TABLE cash_vouchers (
  id                      uuid          NOT NULL DEFAULT uuidv7(),
  code                    varchar(30)   NOT NULL,
  direction               varchar(10)   NOT NULL,
  amount                  numeric(15,0) NOT NULL,
  occurred_at             timestamptz   NOT NULL DEFAULT now(),
  counterparty_type       varchar(20),
  counterparty_name       varchar,
  description             text,
  document_number         varchar(100),
  affects_business_result boolean       NOT NULL,
  status                  varchar(20)   NOT NULL DEFAULT 'Active',
  cancel_reason           text,
  cancelled_at            timestamptz,
  source_type             varchar(30),
  source_id               uuid,
  created_by              uuid,
  created_at              timestamptz   NOT NULL DEFAULT now(),
  updated_by              uuid,
  updated_at              timestamptz   NOT NULL DEFAULT now(),
  cash_account_id         uuid          NOT NULL,
  counter_account_id      uuid,
  category_id             uuid          NOT NULL,
  CONSTRAINT pk_cash_vouchers PRIMARY KEY (id),
  CONSTRAINT fk_cash_vouchers_cash_accounts_cash_account_id FOREIGN KEY (cash_account_id) REFERENCES cash_accounts (id) ON DELETE RESTRICT,
  CONSTRAINT fk_cash_vouchers_cash_accounts_counter_account_id FOREIGN KEY (counter_account_id) REFERENCES cash_accounts (id) ON DELETE RESTRICT,
  CONSTRAINT fk_cash_vouchers_cash_voucher_categories_category_id FOREIGN KEY (category_id) REFERENCES cash_voucher_categories (id) ON DELETE RESTRICT,
  CONSTRAINT ck_cash_vouchers_amount CHECK (amount > 0),
  CONSTRAINT ck_cash_vouchers_source CHECK ((source_type is null) = (source_id is null)),
  CONSTRAINT ck_cash_vouchers_cancelled_at CHECK ((status = 'Cancelled') = (cancelled_at is not null)),
  CONSTRAINT ck_cash_vouchers_cancel_reason CHECK (status <> 'Cancelled' or cancel_reason is not null),
  CONSTRAINT ck_cash_vouchers_counter_account_id CHECK (counter_account_id is null or counter_account_id <> cash_account_id)
);
CREATE UNIQUE INDEX ix_cash_vouchers_code ON cash_vouchers (code);
CREATE INDEX ix_cash_vouchers_cash_account_id_occurred_at ON cash_vouchers (cash_account_id, occurred_at);
CREATE INDEX ix_cash_vouchers_counter_account_id ON cash_vouchers (counter_account_id);
CREATE INDEX ix_cash_vouchers_category_id ON cash_vouchers (category_id);
CREATE INDEX ix_cash_vouchers_occurred_at ON cash_vouchers (occurred_at);
CREATE UNIQUE INDEX ix_cash_vouchers_source_type_source_id_active ON cash_vouchers (source_type, source_id) WHERE source_type IS NOT NULL AND status = 'Active';

CREATE TABLE shift_cash_adjustments (
  id          uuid          NOT NULL DEFAULT uuidv7(),
  type        varchar(10)   NOT NULL,
  purpose     varchar(20)   NOT NULL,
  amount      numeric(15,0) NOT NULL,
  reason      text          NOT NULL,
  created_by  uuid,
  created_at  timestamptz   NOT NULL DEFAULT now(),
  shift_id    uuid          NOT NULL,
  category_id uuid,
  CONSTRAINT pk_shift_cash_adjustments PRIMARY KEY (id),
  CONSTRAINT fk_shift_cash_adjustments_shifts_shift_id FOREIGN KEY (shift_id) REFERENCES shifts (id) ON DELETE RESTRICT,
  CONSTRAINT fk_shift_cash_adjustments_cash_voucher_categories_category_id FOREIGN KEY (category_id) REFERENCES cash_voucher_categories (id) ON DELETE RESTRICT,
  CONSTRAINT ck_shift_cash_adjustments_amount CHECK (amount > 0),
  CONSTRAINT ck_shift_cash_adjustments_purpose_type CHECK ((type = 'OUT') = (purpose in ('EXPENSE', 'BANK_DEPOSIT', 'TO_SAFE'))),
  CONSTRAINT ck_shift_cash_adjustments_category_id CHECK ((purpose in ('EXPENSE', 'OTHER_INCOME')) = (category_id is not null))
);
CREATE INDEX ix_shift_cash_adjustments_shift_id ON shift_cash_adjustments (shift_id);
CREATE INDEX ix_shift_cash_adjustments_category_id ON shift_cash_adjustments (category_id);

CREATE TABLE order_payments (
  id                  uuid          NOT NULL DEFAULT uuidv7(),
  type                varchar(10)   NOT NULL DEFAULT 'PAYMENT',
  method              varchar(20)   NOT NULL,
  amount              numeric(15,0) NOT NULL,
  status              varchar(20)   NOT NULL DEFAULT 'Pending',
  transaction_id      varchar,
  finance_provider    varchar(50),
  finance_contract_no varchar(50),
  created_by          uuid,
  created_at          timestamptz   NOT NULL DEFAULT now(),
  updated_by          uuid,
  updated_at          timestamptz   NOT NULL DEFAULT now(),
  order_id            uuid          NOT NULL,
  shift_id            uuid,
  return_id           uuid,
  warranty_request_id uuid,
  refund_of_payment_id uuid,
  CONSTRAINT pk_order_payments PRIMARY KEY (id),
  CONSTRAINT fk_order_payments_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_payments_shifts_shift_id FOREIGN KEY (shift_id) REFERENCES shifts (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_payments_returns_return_id FOREIGN KEY (return_id, order_id) REFERENCES returns (id, order_id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_payments_warranty_requests_warranty_request_id FOREIGN KEY (warranty_request_id) REFERENCES warranty_requests (id) ON DELETE RESTRICT,
  CONSTRAINT fk_order_payments_order_payments_refund_of_payment_id FOREIGN KEY (refund_of_payment_id) REFERENCES order_payments (id) ON DELETE RESTRICT,
  CONSTRAINT ck_order_payments_amount CHECK (amount > 0),
  CONSTRAINT ck_order_payments_shift_id CHECK (shift_id is null or method in ('CASH', 'CARD_POS', 'BANK_TRANSFER')),
  CONSTRAINT ck_order_payments_return_id CHECK (return_id is null or type = 'REFUND'),
  CONSTRAINT ck_order_payments_status CHECK (status <> 'Paid' or method in ('CASH', 'COD') or transaction_id is not null),
  CONSTRAINT ck_order_payments_method CHECK ((method = 'INSTALLMENT_FINANCE') = (finance_provider is not null and finance_contract_no is not null)),
  CONSTRAINT ck_order_payments_status_collected CHECK (status <> 'Collected' or method = 'COD'),
  CONSTRAINT ck_order_payments_warranty_request_id CHECK (warranty_request_id is null or type = 'REFUND'),
  CONSTRAINT ck_order_payments_return_id_warranty_request_id CHECK (return_id is null or warranty_request_id is null),
  CONSTRAINT ck_order_payments_refund_of_payment_id CHECK (refund_of_payment_id is null or type = 'REFUND'),
  CONSTRAINT ck_order_payments_refund_method CHECK (type = 'PAYMENT' or method <> 'COD'),
  CONSTRAINT ck_order_payments_refund_original_channel CHECK (type = 'PAYMENT' or method not in ('VNPAY', 'CARD_POS', 'INSTALLMENT_FINANCE') or refund_of_payment_id is not null)
);
CREATE UNIQUE INDEX ix_order_payments_transaction_id ON order_payments (transaction_id);
CREATE INDEX ix_order_payments_order_id ON order_payments (order_id);
CREATE INDEX ix_order_payments_shift_id ON order_payments (shift_id);
CREATE INDEX ix_order_payments_return_id ON order_payments (return_id);
CREATE INDEX ix_order_payments_warranty_request_id ON order_payments (warranty_request_id);
CREATE INDEX ix_order_payments_refund_of_payment_id ON order_payments (refund_of_payment_id);
CREATE UNIQUE INDEX ix_order_payments_order_id_pending ON order_payments (order_id) WHERE status = 'Pending' AND type = 'PAYMENT';

CREATE TABLE e_invoices (
  id                  uuid          NOT NULL DEFAULT uuidv7(),
  invoice_type        varchar(30)   NOT NULL,
  status              varchar(20)   NOT NULL DEFAULT 'Pending',
  provider            varchar(30)   NOT NULL,
  amount              numeric(15,0) NOT NULL,
  invoice_symbol      varchar(20),
  invoice_number      varchar(20),
  tax_authority_code  varchar(50),
  lookup_code         varchar(50),
  provider_ref        varchar(100),
  issued_at           timestamptz,
  attempt_count       integer       NOT NULL DEFAULT 0,
  last_error          text,
  created_by          uuid,
  created_at          timestamptz   NOT NULL DEFAULT now(),
  updated_by          uuid,
  updated_at          timestamptz   NOT NULL DEFAULT now(),
  order_id            uuid          NOT NULL,
  return_id           uuid,
  original_invoice_id uuid,
  CONSTRAINT pk_e_invoices PRIMARY KEY (id),
  CONSTRAINT fk_e_invoices_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_e_invoices_returns_return_id FOREIGN KEY (return_id, order_id) REFERENCES returns (id, order_id) ON DELETE RESTRICT,
  CONSTRAINT fk_e_invoices_e_invoices_original_invoice_id FOREIGN KEY (original_invoice_id) REFERENCES e_invoices (id) ON DELETE RESTRICT,
  CONSTRAINT ck_e_invoices_status CHECK ((status = 'Issued') = (issued_at is not null)),
  CONSTRAINT ck_e_invoices_status_invoice_number CHECK (status <> 'Issued' or invoice_number is not null),
  CONSTRAINT ck_e_invoices_amount CHECK (amount >= 0),
  CONSTRAINT ck_e_invoices_invoice_type CHECK (invoice_type = 'SALE' or original_invoice_id is not null),
  CONSTRAINT ck_e_invoices_return_id CHECK (return_id is null or invoice_type <> 'SALE')
);
CREATE UNIQUE INDEX ix_e_invoices_provider_ref ON e_invoices (provider_ref);
CREATE INDEX ix_e_invoices_order_id ON e_invoices (order_id);
CREATE INDEX ix_e_invoices_return_id ON e_invoices (return_id);
CREATE INDEX ix_e_invoices_original_invoice_id ON e_invoices (original_invoice_id);
CREATE INDEX ix_e_invoices_status_created_at ON e_invoices (status, created_at);
CREATE UNIQUE INDEX ix_e_invoices_invoice_symbol_invoice_number ON e_invoices (invoice_symbol, invoice_number);
CREATE UNIQUE INDEX ix_e_invoices_order_id_sale_active ON e_invoices (order_id) WHERE invoice_type = 'SALE' AND status IN ('Pending', 'Issued');

-- 10. System

CREATE TABLE daily_sequences (
  sequence_key  varchar(30) NOT NULL,
  sequence_date date        NOT NULL,
  last_value    integer     NOT NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_daily_sequences PRIMARY KEY (sequence_key, sequence_date),
  CONSTRAINT ck_daily_sequences_last_value CHECK (last_value > 0)
);

CREATE TABLE import_jobs (
  id           uuid        NOT NULL DEFAULT uuidv7(),
  type         varchar(30) NOT NULL,
  status       varchar(20) NOT NULL DEFAULT 'Validating',
  file_name    varchar     NOT NULL,
  file_hash    varchar(64) NOT NULL,
  total_rows   integer     NOT NULL DEFAULT 0,
  success_rows integer     NOT NULL DEFAULT 0,
  error_rows   integer     NOT NULL DEFAULT 0,
  error_report jsonb,
  committed_at timestamptz,
  created_by   uuid,
  created_at   timestamptz NOT NULL DEFAULT now(),
  updated_by   uuid,
  updated_at   timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_import_jobs PRIMARY KEY (id),
  CONSTRAINT ck_import_jobs_total_rows CHECK (total_rows >= 0 and success_rows >= 0 and error_rows >= 0),
  CONSTRAINT ck_import_jobs_success_rows CHECK (success_rows + error_rows <= total_rows),
  CONSTRAINT ck_import_jobs_committed_at CHECK ((status = 'Committed') = (committed_at is not null))
);
CREATE INDEX ix_import_jobs_type_created_at ON import_jobs (type, created_at);
CREATE INDEX ix_import_jobs_file_hash ON import_jobs (file_hash);

CREATE TABLE status_history (
  id          uuid        NOT NULL DEFAULT uuidv7(),
  from_status varchar(20),
  to_status   varchar(20) NOT NULL,
  note        text,
  transaction_id bigint     NOT NULL DEFAULT pg_current_xact_id()::text::bigint,
  correlation_id uuid,
  created_by  uuid,
  created_at  timestamptz NOT NULL DEFAULT now(),
  order_id            uuid,
  warranty_request_id uuid,
  return_id           uuid,
  CONSTRAINT pk_status_history PRIMARY KEY (id),
  CONSTRAINT fk_status_history_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT fk_status_history_warranty_requests_warranty_request_id FOREIGN KEY (warranty_request_id) REFERENCES warranty_requests (id) ON DELETE RESTRICT,
  CONSTRAINT fk_status_history_returns_return_id FOREIGN KEY (return_id) REFERENCES returns (id) ON DELETE RESTRICT,
  CONSTRAINT ck_status_history_one_parent CHECK (num_nonnulls(order_id, warranty_request_id, return_id) = 1)
);
CREATE INDEX ix_status_history_order_id_created_at ON status_history (order_id, created_at);
CREATE INDEX ix_status_history_warranty_request_id_created_at ON status_history (warranty_request_id, created_at);
CREATE INDEX ix_status_history_return_id_created_at ON status_history (return_id, created_at);
CREATE INDEX ix_status_history_transaction_id ON status_history (transaction_id);

CREATE TABLE attachments (
  id          uuid        NOT NULL DEFAULT uuidv7(),
  kind        varchar(10) NOT NULL,
  file_url    text        NOT NULL,
  created_by  uuid,
  created_at  timestamptz NOT NULL DEFAULT now(),
  serial_unit_id      uuid,
  return_id           uuid,
  warranty_request_id uuid,
  CONSTRAINT pk_attachments PRIMARY KEY (id),
  CONSTRAINT fk_attachments_serial_units_serial_unit_id FOREIGN KEY (serial_unit_id) REFERENCES serial_units (id) ON DELETE RESTRICT,
  CONSTRAINT fk_attachments_returns_return_id FOREIGN KEY (return_id) REFERENCES returns (id) ON DELETE RESTRICT,
  CONSTRAINT fk_attachments_warranty_requests_warranty_request_id FOREIGN KEY (warranty_request_id) REFERENCES warranty_requests (id) ON DELETE RESTRICT,
  CONSTRAINT ck_attachments_one_parent CHECK (num_nonnulls(serial_unit_id, return_id, warranty_request_id) = 1)
);
CREATE INDEX ix_attachments_serial_unit_id_created_at ON attachments (serial_unit_id, created_at);
CREATE INDEX ix_attachments_return_id_created_at ON attachments (return_id, created_at);
CREATE INDEX ix_attachments_warranty_request_id_created_at ON attachments (warranty_request_id, created_at);

CREATE TABLE notifications (
  id              uuid         NOT NULL DEFAULT uuidv7(),
  channel         varchar(20)  NOT NULL DEFAULT 'EMAIL',
  template_key    varchar(100) NOT NULL,
  status          varchar(20)  NOT NULL DEFAULT 'Pending',
  payload         jsonb        NOT NULL DEFAULT '{}'::jsonb,
  attempt_count   integer      NOT NULL DEFAULT 0,
  next_attempt_at timestamptz  NOT NULL DEFAULT now(),
  last_error      text,
  sent_at         timestamptz,
  created_at      timestamptz  NOT NULL DEFAULT now(),
  updated_at      timestamptz  NOT NULL DEFAULT now(),
  user_id         uuid,
  order_id        uuid,
  CONSTRAINT pk_notifications PRIMARY KEY (id),
  CONSTRAINT fk_notifications_users_user_id FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT,
  CONSTRAINT fk_notifications_orders_order_id FOREIGN KEY (order_id) REFERENCES orders (id) ON DELETE RESTRICT,
  CONSTRAINT ck_notifications_one_recipient CHECK (num_nonnulls(user_id, order_id) = 1),
  CONSTRAINT ck_notifications_attempt_count CHECK (attempt_count >= 0),
  CONSTRAINT ck_notifications_sent_at CHECK ((status = 'Sent') = (sent_at is not null))
);
CREATE INDEX ix_notifications_next_attempt_at_pending ON notifications (next_attempt_at) WHERE status = 'Pending';
CREATE INDEX ix_notifications_user_id ON notifications (user_id);
CREATE INDEX ix_notifications_order_id ON notifications (order_id);

CREATE TABLE audit_logs (
  id         uuid        NOT NULL DEFAULT uuidv7(),
  table_name varchar     NOT NULL,
  record_key jsonb       NOT NULL,
  action     varchar(10) NOT NULL,
  old_values jsonb,
  new_values jsonb,
  transaction_id bigint     NOT NULL DEFAULT pg_current_xact_id()::text::bigint,
  correlation_id uuid,
  changed_by uuid,
  changed_at timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT pk_audit_logs PRIMARY KEY (id, changed_at)
);
CREATE INDEX ix_audit_logs_table_name_changed_at ON audit_logs (table_name, changed_at);
CREATE INDEX ix_audit_logs_table_name_record_key ON audit_logs (table_name, record_key);
CREATE INDEX ix_audit_logs_changed_by ON audit_logs (changed_by);
CREATE INDEX ix_audit_logs_transaction_id ON audit_logs (transaction_id);

CREATE OR REPLACE FUNCTION fn_audit_erase_values(p_table_name varchar, p_record_key jsonb) RETURNS integer
LANGUAGE sql SECURITY DEFINER SET search_path = public AS $$
  WITH u AS (
    UPDATE audit_logs
    SET old_values = (SELECT jsonb_object_agg(k, NULL) FROM jsonb_object_keys(old_values) k),
        new_values = (SELECT jsonb_object_agg(k, NULL) FROM jsonb_object_keys(new_values) k)
    WHERE table_name = p_table_name AND record_key = p_record_key
    RETURNING 1)
  SELECT count(*)::integer FROM u
$$;

COMMIT;
