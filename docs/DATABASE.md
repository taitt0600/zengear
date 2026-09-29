# DATABASE.md

Database design of ZenGear in DBML (v2 syntax) — paste the ERD block into [dbdiagram.io](https://dbdiagram.io) or publish it with [dbdocs.io](https://dbdocs.io). Application-level technical design (transactions, outbox, audit, cache, jobs, integrations) is in sheet `Technical Design` of `product-domain-blueprint-MVP.xlsx`.

## Conventions

Based on [PostgreSQL Wiki — Don't Do This](https://wiki.postgresql.org/wiki/Don't_Do_This).

| Convention | Rule |
|---|---|
| Primary key | `uuid`, default `uuidv7()`; exceptions: junction tables use the composite key of their FKs, 1-1 extension tables share the parent key, geographic reference tables use the official integer code |
| Naming | `snake_case`, plural table names (`status_history` keeps the uncountable noun) |
| Column order | PK → business columns → audit columns → FKs; a column added by a later phase is appended at the end of the physical table |
| Nullability | Every column declares `null` or `not null` |
| Timestamps | `timestamptz` only |
| Money | `numeric(15,0)`, never `money` |
| Text | `varchar` or `text` by default; `varchar(n)` only for a real format limit or a fixed value set; never `char(n)` |
| Status fields | `varchar` with readable values, listed in `note:`, validated in code |
| CHECK constraint | Only for invariants within one row |
| Audit columns | Editable tables: `created_by`, `created_at`, `updated_by`, `updated_at`; append-only tables: `created_by`, `created_at`; rows maintained by the system or created inside a parent aggregate: `created_at` (+ `updated_by`, `updated_at` when editable); reference data: none; `*_by` is a plain `uuid` without FK |
| Relationships | `>` required many-to-one, `>?` optional many-to-one, `-?` optional one-to-one; many-to-many through a junction table (DBML v2 syntax) |
| Polymorphic reference | `*_type` + `*_id` without FK only where the set of target tables is large or open: `audit_logs` (any table), stock ledger sources (9 document types), cash voucher sources; the owning aggregate validates the reference and a reconciliation query finds orphans. A small fixed set of parents uses an exclusive arc instead: one nullable FK per parent and `CHECK (num_nonnulls(...) = 1)` (`status_history`, `attachments`, `notifications`) |
| Concurrency | Optimistic via PostgreSQL `xmin` as the EF Core concurrency token (no version column); row locks (`FOR UPDATE`) for cross-row checks |
| Indexes | Every FK column is indexed; composite indexes follow real queries |
| Derived columns (R-DB-09) | A column that can be derived through a parent row must match that parent, enforced by a composite FK to `UNIQUE (id, column)` of the parent (`MATCH SIMPLE`: not checked while a column is NULL): `products (series_id, brand_id)`, `product_images (sku_id, product_id)`, `addresses` and `business_locations (ward_id, province_id)`, `addresses (province_id, country_id)`, `e_invoices` and `order_payments (return_id, order_id)`, `purchase_returns`, `supplier_payments` and `serial_units (purchase_order_id, supplier_id)`, `voucher_usages (voucher_code_id, voucher_id)` and `(order_id, customer_id)`, `return_items` and `warranty_requests (order_item_serial_id, order_item_id)`, `stock_adjustments (serial_unit_id, sku_id)`. `voucher_usages.voucher_id` against `orders.voucher_id` is checked by a reconciliation query, because `orders.voucher_id` is added in Phase 3 |
| Document numbers (R-TX-08) | The daily sequence number is taken with `INSERT INTO daily_sequences … ON CONFLICT (sequence_key, sequence_date) DO UPDATE SET last_value = last_value + 1 RETURNING last_value` inside the transaction that creates the document: no duplicates, no retries, no gap on rollback; the date is in Asia/Ho_Chi_Minh; the UNIQUE code is the last safety net |
| Fillfactor | 90 on tables updated in place at every sale (`product_skus`, `stock_balances`) so updates stay on the same page (HOT update); all other tables keep the default 100; checked by `zengear_07_schema_check.sql` |
| Database roles | The application never connects as the schema owner. `zengear_app` (API): read and write business tables; only `SELECT`, `INSERT` on append-only tables (`audit_logs`, `stock_ledger_entries`, `status_history`, `supplier_payments`); no `DELETE` on `notifications`. `zengear_jobs` (background jobs): as `zengear_app` plus `DELETE` on `notifications`. `zengear_readonly` (reports, reconciliation): `SELECT` only. Personal values in `audit_logs` are erased only through `fn_audit_erase_values`. Grants are in `zengear_08_roles.sql` |
| Personal data | Columns holding personal data: `users` (`name`, `email`, `phone`), `customer_profiles` (`gender`, `date_of_birth`, `internal_note`), `staff_profiles.work_phone`, `addresses` (`recipient_name`, `phone`, `address_detail`), `orders` (`recipient_name`, `contact_phone`), `order_shipping_details` (address columns), `order_invoice_details` (`company_name`, `tax_code`, `address`, `email` — a household-business buyer is a person, `tax_code` holds the 12-digit personal ID), `cash_vouchers.counterparty_name`, `notifications.payload`, `audit_logs` (`old_values`, `new_values`). Anonymization (R-USR-09) clears users, profiles, addresses, notification payloads and audit values; order, shipping and invoice snapshots are kept as sales documents. A new column with personal data is added to this list in the same change |
| Reconciliation (R-REC-01 … R-REC-11) | Denormalized values (`sold_count`, `used_count`, stock quantities, received quantities, ledger running balance, average cost, return and refund totals) and polymorphic ledger sources are checked nightly by `zengear_06_reconciliation.sql`; the job reports differences and never corrects them. The rule of each value is in its column description (Database Definition) and in the header of the file |

## Entity Relationship Diagram

```dbml
Project zengear {
  database_type: 'PostgreSQL'
  Note: 'ZenGear — PC & hardware retail (household business, 1 store): storefront, POS, inventory ledger, purchasing, warranty & returns, cash book. PostgreSQL 18. Tables are grouped by development phase; table and column descriptions are in section Database Definition of DATABASE.md.'
}

Table users {
  id uuid [pk, default: `uuidv7()`]
  firebase_uid varchar(128) [null, unique]
  name varchar [not null]
  email varchar(255) [null, unique]
  phone varchar(20) [null, unique, note: 'E.164, e.g. +84901234567']
  phone_verified_at timestamptz [null]
  status varchar(20) [not null, default: 'ACTIVE', note: 'ACTIVE | DISABLED | ANONYMIZED']
  last_login_at timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  checks {
    `email = lower(email)`
    `email is not null or phone is not null`
    `phone is null or phone ~ '^\+[1-9][0-9]{7,14}$'`
    `phone_verified_at is null or phone is not null`
  }
}

Table customer_profiles {
  user_id uuid [pk]
  gender varchar(10) [null, note: 'MALE | FEMALE | OTHER']
  date_of_birth date [null]
  marketing_consent boolean [not null, default: false]
  marketing_consent_at timestamptz [null]
  marketing_consent_source varchar(20) [null, note: 'SIGNUP | CHECKOUT | ACCOUNT | COUNTER']
  marketing_consent_version varchar(20) [null]
  cod_allowed boolean [not null, default: true, note: 'added in Phase 2']
  internal_note text [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  checks {
    `date_of_birth is null or date_of_birth >= '1900-01-01'`
    `marketing_consent = false or marketing_consent_at is not null`
    `marketing_consent = false or (marketing_consent_source is not null and marketing_consent_version is not null)`
  }
}

Table staff_profiles {
  user_id uuid [pk]
  employee_code varchar [null, unique]
  joined_date date [null]
  work_phone varchar(20) [null]
  left_date date [null]
  must_change_password boolean [not null, default: false]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  checks {
    `left_date is null or joined_date is null or left_date >= joined_date`
  }
}

Ref: users.id -? customer_profiles.user_id [delete: cascade]
Ref: users.id -? staff_profiles.user_id [delete: cascade]

Table roles {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null, unique, note: 'Seed: Admin | Store Manager | Sales Consultant | Technical Support | Cashier | Warehouse Keeper | Purchasing | Accountant | Customer — admins can add roles']
  scoping_type varchar(10) [not null, note: 'NONE | SELF']
  is_system_role boolean [not null, default: false]
  max_discount_percent numeric(5,2) [null, note: 'cap for manual discounts at the counter; NULL = not allowed']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  checks {
    `max_discount_percent is null or (max_discount_percent >= 0 and max_discount_percent <= 100)`
  }
}

Table permissions {
  id uuid [pk, default: `uuidv7()`]
  key varchar(100) [not null, unique, note: 'Format resource.action, e.g. order.confirm | price.update — defined in code, not editable in UI']
  description text [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
}

Table user_roles {
  user_id uuid [not null]
  role_id uuid [not null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (user_id, role_id) [pk]
    role_id
  }
}

Table role_permissions {
  role_id uuid [not null]
  permission_id uuid [not null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (role_id, permission_id) [pk]
    permission_id
  }
}

Table countries {
  id integer [pk]
  country_code varchar(2) [not null, unique, note: 'ISO 3166-1 alpha-2, e.g. VN, US']
  country_code3 varchar(3) [null, unique, note: 'ISO 3166-1 alpha-3, e.g. VNM, USA']
  country_number varchar(3) [null, note: 'ISO 3166-1 numeric']
  common_name varchar [null]
  formal_name varchar [null]
  country_type varchar [null]
  country_sub_type varchar [null]
  sovereignty varchar [null]
  capital varchar [null]
  currency_code varchar(3) [null, note: 'ISO 4217']
  currency_name varchar [null]
  telephone_code varchar(10) [null]
  internet_country_code varchar(10) [null, note: 'ccTLD, e.g. .vn']
  flag_url text [null]
  sort_order integer [null]
  is_published boolean [not null, default: true]
  is_deleted boolean [not null, default: false]
}

Table provinces {
  id integer [pk]
  name varchar [not null]
  code varchar(20) [null, unique]
  type varchar(20) [null, note: 'Tỉnh (province) | Thành phố (centrally-run city)']
  zip_code varchar(20) [null]
  phone_code varchar(10) [null]
  is_active boolean [not null, default: true]
  sort_order integer [not null, default: 0]
  country_id integer [not null]

  indexes {
    (id, country_id) [unique]
    country_id
  }
}

Table wards {
  id integer [pk]
  name varchar [not null]
  code varchar(20) [null, unique]
  type varchar(20) [null, note: 'Xã (commune) | Phường (ward) | Đặc khu (special zone)']
  zip_code varchar(20) [null]
  phone_code varchar(10) [null]
  is_active boolean [not null, default: true]
  sort_order integer [not null, default: 0]
  province_id integer [null]

  indexes {
    province_id
    (id, province_id) [unique]
  }
}

Table addresses {
  id uuid [pk, default: `uuidv7()`]
  recipient_name varchar [not null]
  phone varchar(20) [not null]
  address_detail text [not null]
  label varchar [null, note: 'Home | Office']
  is_default boolean [not null, default: false]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  user_id uuid [not null]
  country_id integer [not null]
  province_id integer [null]
  ward_id integer [null]

  indexes {
    country_id
    province_id
    ward_id
    user_id
  }

  checks {
    `ward_id is null or province_id is not null`
  }
}

Ref: provinces.country_id > countries.id [delete: restrict]
Ref: wards.province_id >? provinces.id [delete: restrict]
Ref: business_locations.province_id > provinces.id [delete: restrict]
Ref: business_locations.(ward_id, province_id) > wards.(id, province_id) [delete: restrict]
Ref: addresses.country_id > countries.id [delete: restrict]
Ref: addresses.(province_id, country_id) >? provinces.(id, country_id) [delete: restrict]
Ref: addresses.(ward_id, province_id) >? wards.(id, province_id) [delete: restrict]

Ref: user_roles.user_id > users.id [delete: cascade]
Ref: user_roles.role_id > roles.id [delete: restrict]
Ref: role_permissions.role_id > roles.id [delete: cascade]
Ref: role_permissions.permission_id > permissions.id [delete: cascade]
Ref: addresses.user_id > users.id [delete: cascade]

Table categories {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  slug varchar [not null, unique]
  attribute_schema jsonb [null]
  mega_menu_config jsonb [null, note: 'added in the Future phase']
  doa_exchange_days integer [null, note: '1-for-1 exchange window in days for manufacturer defects']
  return_days integer [null, note: 'window in days for returning a non-defective item']
  return_fee_first_month_percent numeric(5,2) [null]
  return_fee_monthly_percent numeric(5,2) [null]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  parent_category_id uuid [null]

  indexes {
    parent_category_id
  }

  checks {
    `doa_exchange_days is null or doa_exchange_days >= 0`
    `return_days is null or return_days >= 0`
    `return_fee_first_month_percent is null or (return_fee_first_month_percent >= 0 and return_fee_first_month_percent <= 100)`
    `return_fee_monthly_percent is null or (return_fee_monthly_percent >= 0 and return_fee_monthly_percent <= 100)`
    `(return_fee_first_month_percent is null) = (return_fee_monthly_percent is null)`
  }
}

Table brands {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  slug varchar [not null, unique]
  logo_url text [null]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
}

Table product_series {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  slug varchar [not null, unique]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  brand_id uuid [not null]

  indexes {
    (id, brand_id) [unique]
    brand_id
  }
}

Table products {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  slug varchar [not null, unique]
  description text [null]
  attributes jsonb [null]
  variant_options jsonb [null]
  meta jsonb [null]
  requires_serial boolean [not null, default: false]
  warranty_months integer [null]
  status varchar(20) [not null, default: 'DRAFT', note: 'DRAFT | PUBLISHED | DISCONTINUED']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  category_id uuid [not null]
  brand_id uuid [not null]
  series_id uuid [null]
  tax_class_id uuid [not null]

  indexes {
    (category_id, status)
    brand_id
    series_id
    status
    tax_class_id
  }

  checks {
    `warranty_months is null or warranty_months >= 0`
  }
}

Table product_skus {
  id uuid [pk, default: `uuidv7()`]
  sku varchar [not null, unique]
  barcode varchar(50) [null, unique]
  attributes jsonb [null]
  price numeric(15,0) [not null]
  compare_at_price numeric(15,0) [null]
  low_stock_threshold integer [null]
  max_stock_threshold integer [null]
  sold_count integer [not null, default: 0, note: 'gross units sold in Completed orders; reconciled nightly (R-REC-01)']
  average_cost numeric(15,0) [not null, default: 0, note: 'moving weighted average cost across all warehouses; visible only with cost.view']
  is_default boolean [not null, default: false]
  is_service boolean [not null, default: false]
  is_pc_build boolean [not null, default: false, note: 'pre-built PC configuration; components in pc_build_components']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  product_id uuid [not null]

  indexes {
    (id, product_id) [unique]
    (product_id, attributes) [unique, name: 'ix_product_skus_product_id_attributes']
  }

  checks {
    `compare_at_price is null or compare_at_price > price`
    `price >= 0`
    `sold_count >= 0`
    `low_stock_threshold is null or low_stock_threshold >= 0`
    `max_stock_threshold is null or max_stock_threshold > 0`
    `low_stock_threshold is null or max_stock_threshold is null or max_stock_threshold >= low_stock_threshold`
    `average_cost >= 0`
    `not (is_pc_build and is_service)`
  }
}

Table product_images {
  id uuid [pk, default: `uuidv7()`]
  url text [not null]
  display_order integer [not null, default: 0]
  is_primary boolean [not null, default: false]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  product_id uuid [not null]
  sku_id uuid [null]

  indexes {
    product_id
    sku_id
  }
}

Table product_badges {
  id uuid [pk, default: `uuidv7()`]
  code varchar(50) [not null, unique, note: 'Seed: best_seller | featured | hot_deal — admins can add more']
  name varchar [not null]
  bg_color varchar(20) [null]
  text_color varchar(20) [null]
  priority integer [not null, default: 0]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
}

Table product_badge_assignments {
  product_id uuid [not null]
  badge_id uuid [not null]
  starts_at timestamptz [null]
  ends_at timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (product_id, badge_id) [pk]
    badge_id
  }

  checks {
    `starts_at is null or ends_at is null or ends_at > starts_at`
  }
}

Table product_tags {
  id uuid [pk, default: `uuidv7()`]
  title varchar [not null]
  content text [not null]
  type varchar(20) [not null, default: 'PROMOTION', note: 'PROMOTION | POLICY']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  category_id uuid [null]
  product_id uuid [null]

  indexes {
    category_id
    product_id
  }

  checks {
    `(category_id is not null)::int + (product_id is not null)::int = 1`
  }
}
Table pc_build_components {
  pc_sku_id uuid [not null]
  component_sku_id uuid [not null]
  quantity integer [not null, default: 1]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (pc_sku_id, component_sku_id) [pk]
    component_sku_id
  }

  checks {
    `quantity > 0`
    `pc_sku_id <> component_sku_id`
  }
}

Ref: product_tags.category_id >? categories.id [delete: cascade]
Ref: product_tags.product_id >? products.id [delete: cascade]

Ref: categories.parent_category_id >? categories.id [delete: restrict]
Ref: product_series.brand_id > brands.id [delete: restrict]
Ref: products.category_id > categories.id [delete: restrict]
Ref: products.brand_id > brands.id [delete: restrict]
Ref: products.(series_id, brand_id) >? product_series.(id, brand_id) [delete: restrict]
Ref: product_images.product_id > products.id [delete: cascade]
Ref: product_images.(sku_id, product_id) >? product_skus.(id, product_id) [delete: set null]
Ref: product_skus.product_id > products.id [delete: restrict]
Ref: product_badge_assignments.product_id > products.id [delete: cascade]
Ref: product_badge_assignments.badge_id > product_badges.id [delete: cascade]

Table business_locations {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null, unique]
  phone varchar(20) [not null]
  email varchar [null]
  address_detail text [not null]
  regular_hours jsonb [not null, note: 'weekly periods: [{"day": 1-7, "open": "HH:mm", "close": "HH:mm"}]']
  special_hours jsonb [null, note: 'date overrides: [{"date": "yyyy-MM-dd", "closed": true}] or with open/close']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  province_id integer [not null]
  ward_id integer [not null]

  indexes {
    province_id
    ward_id
  }

  checks {
    `jsonb_typeof(regular_hours) = 'array'`
    `special_hours is null or jsonb_typeof(special_hours) = 'array'`
  }
}

Table warehouses {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  type varchar(20) [not null, note: 'MAIN | WARRANTY | DISPLAY | OUTLET — DISPLAY = demo units on the shop floor, not for sale; OUTLET = open-box and ex-display units sold one by one']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
}

Table stock_balances {
  sku_id uuid [not null]
  warehouse_id uuid [not null]
  quantity integer [not null, default: 0, note: '= Σ stock_ledger_entries.quantity_change; reconciled nightly (R-REC-03, R-REC-04)']
  reserved_quantity integer [not null, default: 0, note: 'held by Pending orders; available = quantity - reserved_quantity; reconciled nightly (R-REC-06)']
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  indexes {
    (sku_id, warehouse_id) [pk]
    warehouse_id
  }

  checks {
    `quantity >= 0`
    `reserved_quantity >= 0`
  }
}
Table stock_ledger_entries {
  id uuid [pk, default: `uuidv7()`]
  quantity_change integer [not null]
  balance_after integer [not null, note: 'stock_balances.quantity right after this movement; checked nightly (R-REC-07)']
  unit_cost numeric(15,0) [null, note: 'cost per unit of this movement; basis of stock value and the moving average']
  movement_type varchar(30) [not null, note: 'OPENING | PURCHASE_RECEIPT | ORDER_CONFIRM | ORDER_CANCEL | ADJUSTMENT | RETURN_RESTOCK | WARRANTY_OUT | WARRANTY_IN | SUPPLIER_RETURN | DELIVERY_RETURN | TRANSFER_OUT | TRANSFER_IN | ASSEMBLY_OUT | ASSEMBLY_IN']
  reference_type varchar(30) [null, note: 'purchase_receipts | purchase_returns | orders | stock_adjustments | stock_counts | return_items | warranty_requests | serial_units | pc_assemblies']
  reference_id uuid [null]
  note text [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  sku_id uuid [not null]
  warehouse_id uuid [not null]
  serial_unit_id uuid [null, note: 'the unit that moved; required for SKUs with requires_serial (one row per unit)']

  indexes {
    (sku_id, warehouse_id, created_at)
    warehouse_id
    (reference_type, reference_id)
    (serial_unit_id, created_at)
  }

  checks {
    `quantity_change <> 0`
    `unit_cost is null or unit_cost >= 0`
    `balance_after >= 0`
    `serial_unit_id is null or quantity_change in (1, -1)`
  }
}
Table serial_units {
  id uuid [pk, default: `uuidv7()`]
  serial_number varchar(100) [not null]
  status varchar(25) [not null, default: 'IN_STOCK', note: 'IN_STOCK | RESERVED | SOLD | ASSEMBLED | IN_WARRANTY | DEFECTIVE | RETURNED_TO_SUPPLIER | LOST']
  received_at timestamptz [not null]
  condition varchar(20) [not null, default: 'NEW', note: 'NEW | OPEN_BOX | DISPLAY']
  condition_note text [null]
  resale_price numeric(15,0) [null]
  resale_warranty_months integer [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  sku_id uuid [not null]
  warehouse_id uuid [null]
  supplier_id uuid [null]
  purchase_order_id uuid [null]
  parent_serial_unit_id uuid [null, note: 'the PC unit this component is built into']

  indexes {
    (id, sku_id) [unique]
    (sku_id, serial_number) [unique]
    (sku_id, status)
    serial_number
    warehouse_id
    supplier_id
    purchase_order_id
    parent_serial_unit_id
  }

  checks {
    `status not in ('IN_STOCK', 'RESERVED', 'DEFECTIVE') or warehouse_id is not null`
    `status not in ('SOLD', 'ASSEMBLED', 'IN_WARRANTY', 'RETURNED_TO_SUPPLIER', 'LOST') or warehouse_id is null`
    `(status = 'ASSEMBLED') = (parent_serial_unit_id is not null)`
    `parent_serial_unit_id is null or parent_serial_unit_id <> id`
    `condition <> 'NEW' or (resale_price is null and resale_warranty_months is null)`
    `(resale_price is null) = (resale_warranty_months is null)`
    `resale_price is null or resale_price > 0`
    `resale_warranty_months is null or resale_warranty_months >= 0`
  }
}

Ref: stock_balances.sku_id > product_skus.id [delete: restrict]
Ref: stock_balances.warehouse_id > warehouses.id [delete: restrict]
Ref: stock_ledger_entries.sku_id > product_skus.id [delete: restrict]
Ref: stock_ledger_entries.warehouse_id > warehouses.id [delete: restrict]
Ref: stock_ledger_entries.(serial_unit_id, sku_id) >? serial_units.(id, sku_id) [delete: restrict]
Ref: status_history.order_id >? orders.id [delete: restrict]
Ref: status_history.warranty_request_id >? warranty_requests.id [delete: restrict]
Ref: status_history.return_id >? returns.id [delete: restrict]
Ref: attachments.serial_unit_id >? serial_units.id [delete: restrict]
Ref: attachments.return_id >? returns.id [delete: restrict]
Ref: attachments.warranty_request_id >? warranty_requests.id [delete: restrict]
Ref: notifications.user_id >? users.id [delete: restrict]
Ref: notifications.order_id >? orders.id [delete: restrict]

Table suppliers {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  contact_info text [null]
  tax_code varchar(13) [null, unique]
  opening_payable numeric(15,0) [not null, default: 0, note: 'payable at go-live; negative = prepaid']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
}

Table purchase_orders {
  id uuid [pk, default: `uuidv7()`]
  status varchar(20) [not null, default: 'Draft', note: 'Draft | Ordered | Partially_Received | Received | Cancelled']
  order_date timestamptz [not null, default: `now()`]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  supplier_id uuid [not null]

  indexes {
    (id, supplier_id) [unique]
    supplier_id
    status
  }
}

Table purchase_order_items {
  id uuid [pk, default: `uuidv7()`]
  quantity_ordered integer [not null]
  quantity_received integer [not null, default: 0, note: '= Σ purchase_receipts.quantity; reconciled nightly (R-REC-05)']
  unit_cost numeric(15,0) [not null, note: 'excl. VAT; 0 = free units of the same SKU from a supplier promotion']
  vat_rate numeric(5,2) [not null, note: 'input VAT rate on the supplier invoice']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  purchase_order_id uuid [not null]
  sku_id uuid [not null]

  indexes {
    (purchase_order_id, sku_id, unit_cost) [unique, name: 'ix_purchase_order_items_purchase_order_id_sku_id_unit_cost']
    sku_id
  }

  checks {
    `quantity_received <= quantity_ordered`
    `quantity_ordered > 0`
    `quantity_received >= 0`
    `unit_cost >= 0`
    `vat_rate >= 0 and vat_rate <= 100`
  }
}

Table purchase_receipts {
  id uuid [pk, default: `uuidv7()`]
  quantity integer [not null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  po_item_id uuid [not null]

  indexes {
    po_item_id
  }

  checks {
    `quantity > 0`
  }
}
Table purchase_returns {
  id uuid [pk, default: `uuidv7()`]
  status varchar(20) [not null, default: 'Draft', note: 'Draft | Completed | Cancelled']
  reason text [not null]
  total_amount numeric(15,0) [not null, default: 0, note: 'incl. VAT']
  vat_amount numeric(15,0) [not null, default: 0]
  completed_at timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  supplier_id uuid [not null]
  purchase_order_id uuid [not null]

  indexes {
    supplier_id
    purchase_order_id
    status
  }

  checks {
    `total_amount >= 0`
    `vat_amount >= 0 and vat_amount <= total_amount`
    `(status = 'Completed') = (completed_at is not null)`
  }
}

Table purchase_return_items {
  id uuid [pk, default: `uuidv7()`]
  quantity integer [not null]
  unit_cost numeric(15,0) [not null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  purchase_return_id uuid [not null]
  po_item_id uuid [not null]
  serial_unit_id uuid [null]

  indexes {
    purchase_return_id
    po_item_id
    serial_unit_id
  }

  checks {
    `quantity > 0`
    `unit_cost >= 0`
    `serial_unit_id is null or quantity = 1`
  }
}

Table supplier_payments {
  id uuid [pk, default: `uuidv7()`]
  type varchar(10) [not null, default: 'PAYMENT', note: 'PAYMENT (store pays supplier) | REFUND (supplier pays store back)']
  method varchar(20) [not null, note: 'CASH | BANK_TRANSFER | CARD']
  amount numeric(15,0) [not null]
  reference varchar(100) [null]
  paid_at timestamptz [not null, default: `now()`]
  note text [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  supplier_id uuid [not null]
  purchase_order_id uuid [null]
  purchase_return_id uuid [null, note: 'added in Phase 3']

  indexes {
    (supplier_id, paid_at)
    purchase_order_id
    purchase_return_id
  }

  checks {
    `amount > 0`
    `purchase_return_id is null or type = 'REFUND'`
    `method = 'CASH' or reference is not null`
  }
}

Ref: purchase_orders.supplier_id > suppliers.id [delete: restrict]
Ref: purchase_order_items.purchase_order_id > purchase_orders.id [delete: restrict]
Ref: purchase_order_items.sku_id > product_skus.id [delete: restrict]
Ref: purchase_receipts.po_item_id > purchase_order_items.id [delete: restrict]

Table adjustment_reasons {
  id uuid [pk, default: `uuidv7()`]
  key varchar(50) [not null, unique, note: 'Seed: DAMAGED | LOST | MISCOUNT — admins can add more']
  label varchar [not null]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
}

Table stock_adjustments {
  id uuid [pk, default: `uuidv7()`]
  quantity_change integer [not null]
  status varchar(20) [not null, default: 'Requested', note: 'Requested | Approved | Rejected']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  sku_id uuid [not null]
  warehouse_id uuid [not null]
  reason_id uuid [not null]
  serial_unit_id uuid [null]

  indexes {
    sku_id
    status
    warehouse_id
    reason_id
    serial_unit_id
  }

  checks {
    `quantity_change <> 0`
  }
}
Table stock_counts {
  id uuid [pk, default: `uuidv7()`]
  status varchar(20) [not null, default: 'Draft', note: 'Draft | Completed | Cancelled']
  note text [null]
  completed_at timestamptz [null]
  completed_by uuid [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  warehouse_id uuid [not null]
  merged_into_id uuid [null]

  indexes {
    (warehouse_id, status)
    merged_into_id
  }

  checks {
    `(status = 'Completed') = (completed_at is not null)`
    `merged_into_id is null or status = 'Cancelled'`
  }
}

Table stock_count_lines {
  id uuid [pk, default: `uuidv7()`]
  counted_quantity integer [not null]
  serial_number varchar(100) [null]
  book_quantity integer [null]
  note text [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  stock_count_id uuid [not null]
  sku_id uuid [not null]

  indexes {
    (stock_count_id, sku_id, serial_number) [unique, name: 'ix_stock_count_lines_stock_count_id_sku_id_serial_number']
    sku_id
  }

  checks {
    `counted_quantity >= 0`
    `serial_number is null or counted_quantity = 1`
    `book_quantity is null or book_quantity >= 0`
  }
}

Ref: stock_adjustments.sku_id > product_skus.id [delete: restrict]
Ref: stock_adjustments.warehouse_id > warehouses.id [delete: restrict]
Ref: stock_adjustments.reason_id > adjustment_reasons.id [delete: restrict]
Ref: stock_counts.warehouse_id > warehouses.id [delete: restrict]
Ref: e_invoices.order_id > orders.id [delete: restrict]
Ref: shipping_methods.carrier_id >? carriers.id [delete: restrict]
Ref: shipping_fee_rules.shipping_method_id > shipping_methods.id [delete: cascade]
Ref: shipping_fee_rules.province_id >? provinces.id [delete: restrict]
Ref: warranty_requests.replacement_sku_id >? product_skus.id [delete: restrict]
Ref: order_payments.warranty_request_id >? warranty_requests.id [delete: restrict]
Ref: order_payments.refund_of_payment_id >? order_payments.id [delete: restrict]
Ref: order_shipping_details.carrier_id >? carriers.id [delete: restrict]
Ref: carrier_settlements.shift_id >? shifts.id [delete: restrict]
Ref: carrier_settlements.carrier_id > carriers.id [delete: restrict]
Ref: carrier_settlement_lines.settlement_id > carrier_settlements.id [delete: restrict]
Ref: orders.id -? carrier_settlement_lines.order_id [delete: restrict]
Ref: voucher_codes.voucher_id > vouchers.id [delete: restrict]
Ref: voucher_targets.voucher_id > vouchers.id [delete: cascade]
Ref: voucher_targets.category_id >? categories.id [delete: restrict]
Ref: voucher_targets.product_id >? products.id [delete: restrict]
Ref: voucher_usages.(voucher_code_id, voucher_id) >? voucher_codes.(id, voucher_id) [delete: restrict]
Ref: purchase_returns.supplier_id > suppliers.id [delete: restrict]
Ref: purchase_returns.(purchase_order_id, supplier_id) > purchase_orders.(id, supplier_id) [delete: restrict]
Ref: purchase_return_items.purchase_return_id > purchase_returns.id [delete: restrict]
Ref: purchase_return_items.po_item_id > purchase_order_items.id [delete: restrict]
Ref: purchase_return_items.serial_unit_id >? serial_units.id [delete: restrict]
Ref: supplier_payments.supplier_id > suppliers.id [delete: restrict]
Ref: supplier_payments.(purchase_order_id, supplier_id) >? purchase_orders.(id, supplier_id) [delete: restrict]
Ref: supplier_payments.purchase_return_id >? purchase_returns.id [delete: restrict]
Ref: e_invoices.(return_id, order_id) >? returns.(id, order_id) [delete: restrict]
Ref: e_invoices.original_invoice_id >? e_invoices.id [delete: restrict]
Ref: stock_counts.merged_into_id >? stock_counts.id [delete: restrict]
Ref: stock_count_lines.stock_count_id > stock_counts.id [delete: restrict]
Ref: stock_count_lines.sku_id > product_skus.id [delete: restrict]
Ref: stock_adjustments.(serial_unit_id, sku_id) >? serial_units.(id, sku_id) [delete: restrict]

Table wishlists {
  user_id uuid [not null]
  product_id uuid [not null]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (user_id, product_id) [pk]
    product_id
  }
}

Ref: wishlists.user_id > users.id [delete: cascade]
Ref: wishlists.product_id > products.id [delete: cascade]

Table cart_items {
  user_id uuid [not null]
  sku_id uuid [not null]
  quantity integer [not null]
  created_at timestamptz [not null, default: `now()`]
  updated_at timestamptz [not null, default: `now()`]

  indexes {
    (user_id, sku_id) [pk]
    sku_id
  }

  checks {
    `quantity > 0`
  }
}
Table cart_serial_units {
  user_id uuid [not null]
  serial_unit_id uuid [not null]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    (user_id, serial_unit_id) [pk]
    serial_unit_id
  }
}

Table carriers {
  id uuid [pk, default: `uuidv7()`]
  code varchar(30) [not null, unique, note: 'Seed: GHN | GHTK | VIETTEL_POST | IN_HOUSE (store delivery staff) — also the key future API adapters are registered under']
  name varchar [not null]
  integration_type varchar(10) [not null, default: 'MANUAL', note: 'MANUAL | API — API is future scope; credentials live in the secret store, never in the DB']
  cod_settlement_days integer [null, note: 'days the carrier contract allows to pay COD after delivery']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  checks {
    `cod_settlement_days is null or cod_settlement_days > 0`
  }
}

Table shipping_methods {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  base_fee numeric(15,0) [not null, note: 'fallback fee when no shipping_fee_rules row matches']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  tax_class_id uuid [not null]
  carrier_id uuid [null, note: 'default carrier; IN_HOUSE = delivered by store staff; NULL = chosen when shipped']

  indexes {
    tax_class_id
    carrier_id
  }

  checks {
    `base_fee >= 0`
  }
}
Table shipping_fee_rules {
  id uuid [pk, default: `uuidv7()`]
  min_order_value numeric(15,0) [not null, default: 0]
  fee numeric(15,0) [not null]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  shipping_method_id uuid [not null]
  province_id integer [null, note: 'NULL = every province']

  indexes {
    (shipping_method_id, province_id, min_order_value) [unique, name: 'ix_shipping_fee_rules_method_province_min_value']
    province_id
  }

  checks {
    `min_order_value >= 0`
    `fee >= 0`
  }
}

Table tax_classes {
  id uuid [pk, default: `uuidv7()`]
  code varchar(50) [not null, unique, note: 'Business line for VAT on revenue. Seed: GOODS_DISTRIBUTION | GOODS_RELATED_SERVICE | SERVICE — admins can add more']
  name varchar [not null]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
}

Table tax_rates {
  id uuid [pk, default: `uuidv7()`]
  rate numeric(5,2) [not null, note: 'VAT rate in percent of revenue']
  valid_from timestamptz [not null]
  valid_to timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  tax_class_id uuid [not null]

  indexes {
    (tax_class_id, valid_from)
  }

  checks {
    `rate >= 0 and rate <= 100`
    `valid_to is null or valid_to > valid_from`
  }
}

Table shifts {
  id uuid [pk, default: `uuidv7()`]
  opening_cash numeric(15,0) [not null]
  closing_cash_expected numeric(15,0) [null]
  closing_cash_actual numeric(15,0) [null]
  status varchar(20) [not null, default: 'Open', note: 'Open | Closed']
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  staff_id uuid [not null]

  indexes {
    staff_id
  }

  checks {
    `opening_cash >= 0`
    `closing_cash_expected is null or closing_cash_expected >= 0`
    `closing_cash_actual is null or closing_cash_actual >= 0`
  }
}
Table shift_cash_adjustments {
  id uuid [pk, default: `uuidv7()`]
  type varchar(10) [not null, note: 'IN | OUT']
  purpose varchar(20) [not null, note: 'EXPENSE | OTHER_INCOME | BANK_DEPOSIT | TO_SAFE | FROM_SAFE']
  amount numeric(15,0) [not null]
  reason text [not null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  shift_id uuid [not null]
  category_id uuid [null, note: 'cash book category; required for EXPENSE and OTHER_INCOME']

  indexes {
    shift_id
    category_id
  }

  checks {
    `amount > 0`
    `(type = 'OUT') = (purpose in ('EXPENSE', 'BANK_DEPOSIT', 'TO_SAFE'))`
    `(purpose in ('EXPENSE', 'OTHER_INCOME')) = (category_id is not null)`
  }
}

Ref: shifts.staff_id > users.id [delete: restrict]
Ref: shift_cash_adjustments.shift_id > shifts.id [delete: restrict]

Table cash_accounts {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null, unique]
  type varchar(10) [not null, note: 'CASH | BANK | EWALLET']
  bank_name varchar [null]
  account_number varchar(30) [null]
  is_default boolean [not null, default: false, note: 'default account of its type for automatic vouchers']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  checks {
    `type <> 'BANK' or (bank_name is not null and account_number is not null)`
  }
}

Table cash_voucher_categories {
  id uuid [pk, default: `uuidv7()`]
  name varchar [not null]
  direction varchar(10) [not null, note: 'RECEIPT | PAYMENT']
  affects_business_result boolean [not null, default: true, note: 'default for vouchers of this category']
  is_system boolean [not null, default: false, note: 'used by automatic vouchers']
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  indexes {
    (direction, name) [unique]
  }
}

Table cash_vouchers {
  id uuid [pk, default: `uuidv7()`]
  code varchar(30) [not null, unique]
  direction varchar(10) [not null, note: 'RECEIPT | PAYMENT']
  amount numeric(15,0) [not null]
  occurred_at timestamptz [not null, default: `now()`]
  counterparty_type varchar(20) [null, note: 'CUSTOMER | SUPPLIER | CARRIER | STAFF | OTHER']
  counterparty_name varchar [null]
  description text [null]
  document_number varchar(100) [null, note: 'number of the source invoice or document']
  affects_business_result boolean [not null]
  status varchar(20) [not null, default: 'Active', note: 'Active | Cancelled']
  cancel_reason text [null]
  cancelled_at timestamptz [null]
  source_type varchar(30) [null, note: 'order_payments | carrier_settlements | supplier_payments | shift_cash_adjustments | cash_vouchers; NULL = manual']
  source_id uuid [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  cash_account_id uuid [not null]
  counter_account_id uuid [null, note: 'destination account of an internal transfer']
  category_id uuid [not null]

  indexes {
    (cash_account_id, occurred_at)
    counter_account_id
    category_id
    occurred_at
  }

  checks {
    `amount > 0`
    `(source_type is null) = (source_id is null)`
    `(status = 'Cancelled') = (cancelled_at is not null)`
    `status <> 'Cancelled' or cancel_reason is not null`
    `counter_account_id is null or counter_account_id <> cash_account_id`
  }
}

Ref: cash_vouchers.cash_account_id > cash_accounts.id [delete: restrict]
Ref: cash_vouchers.counter_account_id >? cash_accounts.id [delete: restrict]
Ref: cash_vouchers.category_id > cash_voucher_categories.id [delete: restrict]
Ref: shift_cash_adjustments.category_id >? cash_voucher_categories.id [delete: restrict]

Table orders {
  id uuid [pk, default: `uuidv7()`]
  order_code varchar(20) [not null, unique, note: 'Format ZG + yyMMdd (Vietnam time) + - + 4-digit daily sequence, e.g. ZG260201-0042']
  type varchar(20) [not null, note: 'SELF_SERVICE | ASSISTED']
  fulfillment_method varchar(10) [not null, note: 'SHIP | PICKUP']
  recipient_name varchar [not null]
  contact_phone varchar(20) [not null]
  status varchar(20) [not null, default: 'Pending', note: 'Pending | Confirmed | Processing | Shipped | Ready_for_pickup | Completed | Cancelled | Returned_to_sender']
  subtotal numeric(15,0) [not null]
  discount_amount numeric(15,0) [not null, default: 0, note: 'voucher discount + manual discount']
  manual_discount_amount numeric(15,0) [not null, default: 0]
  manual_discount_reason text [null]
  manual_discount_by uuid [null, note: 'staff whose discount cap covered this discount']
  shipping_fee numeric(15,0) [not null, default: 0]
  total_amount numeric(15,0) [not null]
  shipping_vat_rate numeric(5,2) [not null, default: 0, note: 'VAT-on-revenue rate of the shipping fee business line (snapshot)']
  idempotency_key uuid [null]
  completed_at timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  customer_id uuid [not null]
  voucher_id uuid [null, note: 'added in Phase 3']
  sales_staff_id uuid [null, note: 'salesperson credited with the sale; defaults to the creator for assisted orders']

  indexes {
    (id, customer_id) [unique]
    (customer_id, created_at)
    (status, created_at)
    voucher_id
    sales_staff_id
    (customer_id, idempotency_key) [unique]
    completed_at
  }

  checks {
    `subtotal >= 0`
    `discount_amount >= 0`
    `shipping_fee >= 0`
    `discount_amount <= subtotal`
    `total_amount = subtotal - discount_amount + shipping_fee`
    `shipping_vat_rate >= 0 and shipping_vat_rate <= 100`
    `(status = 'Completed') = (completed_at is not null)`
    `manual_discount_amount >= 0 and manual_discount_amount <= discount_amount`
    `(manual_discount_amount > 0) = (manual_discount_reason is not null)`
    `manual_discount_amount = 0 or manual_discount_by is not null`
    `voucher_id is not null or discount_amount = manual_discount_amount`
  }
}
Table order_shipping_details {
  order_id uuid [pk]
  shipping_country varchar [not null]
  shipping_province varchar [null]
  shipping_ward varchar [null]
  shipping_address_detail text [not null]
  tracking_code varchar(50) [null]
  created_at timestamptz [not null, default: `now()`]
  shipping_method_id uuid [not null]
  carrier_id uuid [null, note: 'carrier that actually took the parcel, required from Shipped on; IN_HOUSE = store delivery staff']

  indexes {
    shipping_method_id
    carrier_id
  }

  checks {
    `tracking_code is null or carrier_id is not null`
  }
}

Table order_invoice_details {
  order_id uuid [pk]
  buyer_type varchar(20) [not null, default: 'COMPANY', note: 'COMPANY | HOUSEHOLD_BUSINESS']
  company_name varchar [not null]
  tax_code varchar(14) [not null]
  address text [not null]
  email varchar(255) [null]
  created_at timestamptz [not null, default: `now()`]

  indexes {
    tax_code
  }

  checks {
    `tax_code ~ '^([0-9]{10}(-?[0-9]{3})?|[0-9]{12})$'`
    `(buyer_type = 'HOUSEHOLD_BUSINESS') = (tax_code ~ '^[0-9]{12}$')`
  }
}

Table order_items {
  id uuid [pk, default: `uuidv7()`]
  quantity integer [not null]
  unit_price numeric(15,0) [not null]
  product_name varchar [not null]
  sku_code varchar [not null]
  variant_attributes jsonb [null]
  warranty_months integer [null]
  discount_allocated numeric(15,0) [not null, default: 0]
  vat_rate numeric(5,2) [not null, note: 'VAT-on-revenue rate of the line business line (snapshot)']
  unit_cost numeric(15,0) [null, note: 'average cost snapshot when stock left at confirmation']
  item_condition varchar(20) [not null, default: 'NEW', note: 'NEW | OPEN_BOX | DISPLAY — snapshot of the unit sold']
  pc_fulfillment varchar(10) [null, note: 'PC build lines only: STOCK = a pre-built PC is reserved; BUILD = components are reserved and assembled for this order']
  created_at timestamptz [not null, default: `now()`]
  order_id uuid [not null]
  sku_id uuid [not null]
  serial_unit_id uuid [null, note: 'the outlet unit chosen at order time']

  indexes {
    order_id
    sku_id
    serial_unit_id
  }

  checks {
    `quantity > 0`
    `unit_price >= 0`
    `warranty_months is null or warranty_months >= 0`
    `discount_allocated >= 0`
    `discount_allocated <= quantity * unit_price`
    `vat_rate >= 0 and vat_rate <= 100`
    `unit_cost is null or unit_cost >= 0`
    `(item_condition = 'NEW') = (serial_unit_id is null)`
    `serial_unit_id is null or quantity = 1`
    `pc_fulfillment is null or pc_fulfillment in ('STOCK', 'BUILD')`
  }
}

Table order_item_serials {
  id uuid [pk, default: `uuidv7()`]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  order_item_id uuid [not null]
  serial_unit_id uuid [not null]

  indexes {
    (id, order_item_id) [unique]
    order_item_id
    serial_unit_id
  }
}
Table order_item_components {
  id uuid [pk, default: `uuidv7()`]
  quantity integer [not null]
  unit_price numeric(15,0) [not null, note: 'retail price of the component at order time']
  created_at timestamptz [not null, default: `now()`]
  order_item_id uuid [not null]
  component_sku_id uuid [not null]
  replaced_sku_id uuid [null, note: 'listed component this one replaces; NULL = listed component kept, or an added one']

  indexes {
    order_item_id
    component_sku_id
    replaced_sku_id
  }

  checks {
    `quantity > 0`
    `unit_price >= 0`
  }
}

Table pc_assemblies {
  id uuid [pk, default: `uuidv7()`]
  code varchar(20) [not null, unique, note: 'LR + yyMMdd + sequence, e.g. LR260210-0002']
  status varchar(20) [not null, default: 'Draft', note: 'Draft | Completed | Cancelled']
  unit_cost numeric(15,0) [null, note: 'Σ component cost; cost of the PC unit produced']
  completed_at timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  pc_sku_id uuid [not null]
  order_item_id uuid [null, note: 'set when built for an order; NULL = built ahead for stock']
  pc_serial_unit_id uuid [null, note: 'the PC unit produced']
  assembled_by uuid [null]

  indexes {
    pc_sku_id
    order_item_id
    pc_serial_unit_id [unique]
    assembled_by
    status
  }

  checks {
    `(status = 'Completed') = (completed_at is not null)`
    `status <> 'Completed' or (pc_serial_unit_id is not null and unit_cost is not null)`
    `unit_cost is null or unit_cost >= 0`
  }
}

Table pc_assembly_components {
  id uuid [pk, default: `uuidv7()`]
  quantity integer [not null]
  unit_cost numeric(15,0) [null, note: 'average cost of the component when it left stock']
  warranty_months integer [null, note: 'component warranty, snapshot at assembly']
  created_at timestamptz [not null, default: `now()`]
  assembly_id uuid [not null]
  component_sku_id uuid [not null]
  serial_unit_id uuid [null]

  indexes {
    assembly_id
    component_sku_id
    serial_unit_id
  }

  checks {
    `quantity > 0`
    `serial_unit_id is null or quantity = 1`
    `unit_cost is null or unit_cost >= 0`
    `warranty_months is null or warranty_months >= 0`
  }
}

Ref: order_item_serials.order_item_id > order_items.id [delete: restrict]
Ref: order_item_serials.serial_unit_id > serial_units.id [delete: restrict]
Ref: serial_units.sku_id > product_skus.id [delete: restrict]
Ref: serial_units.warehouse_id >? warehouses.id [delete: restrict]
Ref: serial_units.supplier_id >? suppliers.id [delete: restrict]
Ref: serial_units.(purchase_order_id, supplier_id) >? purchase_orders.(id, supplier_id) [delete: restrict]
Ref: serial_units.parent_serial_unit_id >? serial_units.id [delete: restrict]
Ref: warranty_requests.component_serial_unit_id >? serial_units.id [delete: restrict]
Ref: pc_build_components.pc_sku_id > product_skus.id [delete: cascade]
Ref: pc_build_components.component_sku_id > product_skus.id [delete: restrict]
Ref: order_item_components.order_item_id > order_items.id [delete: restrict]
Ref: order_item_components.component_sku_id > product_skus.id [delete: restrict]
Ref: order_item_components.replaced_sku_id >? product_skus.id [delete: restrict]
Ref: pc_assemblies.pc_sku_id > product_skus.id [delete: restrict]
Ref: pc_assemblies.order_item_id >? order_items.id [delete: restrict]
Ref: serial_units.id -? pc_assemblies.pc_serial_unit_id [delete: restrict]
Ref: pc_assemblies.assembled_by >? users.id [delete: restrict]
Ref: pc_assembly_components.assembly_id > pc_assemblies.id [delete: restrict]
Ref: pc_assembly_components.component_sku_id > product_skus.id [delete: restrict]
Ref: pc_assembly_components.serial_unit_id >? serial_units.id [delete: restrict]

Table order_payments {
  id uuid [pk, default: `uuidv7()`]
  type varchar(10) [not null, default: 'PAYMENT', note: 'PAYMENT | REFUND']
  method varchar(20) [not null, note: 'CASH | COD | BANK_TRANSFER | CARD_POS | VNPAY | INSTALLMENT_FINANCE']
  amount numeric(15,0) [not null]
  status varchar(20) [not null, default: 'Pending', note: 'Pending | Collected | Paid | Failed — Collected = COD taken by the carrier, not yet paid out to the store']
  transaction_id varchar [null, unique]
  finance_provider varchar(50) [null]
  finance_contract_no varchar(50) [null]
  expires_at timestamptz [null, note: 'VNPay payment deadline sent as vnp_ExpireDate; added in Phase 2']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  order_id uuid [not null]
  shift_id uuid [null]
  return_id uuid [null]
  warranty_request_id uuid [null]
  refund_of_payment_id uuid [null, note: 'the PAYMENT line this REFUND pays back']

  indexes {
    order_id
    shift_id
    return_id
    warranty_request_id
    refund_of_payment_id
  }

  checks {
    `amount > 0`
    `shift_id is null or method in ('CASH', 'CARD_POS', 'BANK_TRANSFER')`
    `return_id is null or type = 'REFUND'`
    `status <> 'Paid' or method in ('CASH', 'COD') or transaction_id is not null`
    `(method = 'INSTALLMENT_FINANCE') = (finance_provider is not null and finance_contract_no is not null)`
    `status <> 'Collected' or method = 'COD'`
    `warranty_request_id is null or type = 'REFUND'`
    `return_id is null or warranty_request_id is null`
    `expires_at is null or method = 'VNPAY'`
    `refund_of_payment_id is null or type = 'REFUND'`
    `type = 'PAYMENT' or method <> 'COD'`
    `type = 'PAYMENT' or method not in ('VNPAY', 'CARD_POS', 'INSTALLMENT_FINANCE') or refund_of_payment_id is not null`
  }
}
Table e_invoices {
  id uuid [pk, default: `uuidv7()`]
  invoice_type varchar(30) [not null, note: 'SALE | ADJUSTMENT_DECREASE | ADJUSTMENT_INCREASE | REPLACEMENT']
  status varchar(20) [not null, default: 'Pending', note: 'Pending | Issued | Failed | Cancelled']
  provider varchar(30) [not null, note: 'e-invoice provider configured for the store, e.g. MISA | VIETTEL | VNPT | FPT']
  amount numeric(15,0) [not null]
  invoice_symbol varchar(20) [null]
  invoice_number varchar(20) [null]
  tax_authority_code varchar(50) [null]
  lookup_code varchar(50) [null]
  provider_ref varchar(100) [null, unique]
  issued_at timestamptz [null]
  attempt_count integer [not null, default: 0]
  last_error text [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  order_id uuid [not null]
  return_id uuid [null]
  original_invoice_id uuid [null]

  indexes {
    order_id
    return_id
    original_invoice_id
    (status, created_at)
    (invoice_symbol, invoice_number) [unique]
  }

  checks {
    `(status = 'Issued') = (issued_at is not null)`
    `status <> 'Issued' or invoice_number is not null`
    `amount >= 0`
    `invoice_type = 'SALE' or original_invoice_id is not null`
    `return_id is null or invoice_type <> 'SALE'`
  }
}
Table carrier_settlements {
  id uuid [pk, default: `uuidv7()`]
  code varchar(20) [not null, unique, note: 'DS + yyMMdd + sequence, e.g. DS260210-0003']
  status varchar(20) [not null, default: 'Draft', note: 'Draft | Confirmed | Paid | Cancelled']
  source varchar(20) [not null, default: 'MANUAL', note: 'MANUAL | FILE_IMPORT | API — FILE_IMPORT and API are future scope']
  external_ref varchar(100) [null, note: 'settlement id on the carrier side; makes future file/API sync idempotent']
  period_from date [not null]
  period_to date [not null]
  total_cod numeric(15,0) [not null, default: 0]
  total_fees numeric(15,0) [not null, default: 0]
  previous_balance numeric(15,0) [not null, default: 0]
  net_amount numeric(15,0) [not null, default: 0]
  bank_reference varchar(100) [null]
  confirmed_at timestamptz [null]
  confirmed_by uuid [null]
  paid_at timestamptz [null]
  note text [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  carrier_id uuid [not null]
  shift_id uuid [null, note: 'counter shift that received cash handed in by IN_HOUSE delivery staff']

  indexes {
    (carrier_id, status)
    (carrier_id, external_ref) [unique]
    shift_id
  }

  checks {
    `period_to >= period_from`
    `total_cod >= 0 and total_fees >= 0`
    `net_amount = total_cod - total_fees + previous_balance`
    `(status = 'Paid') = (paid_at is not null)`
    `status not in ('Confirmed', 'Paid') or confirmed_at is not null`
    `status <> 'Paid' or net_amount = 0 or bank_reference is not null or shift_id is not null`
  }
}

Table carrier_settlement_lines {
  id uuid [pk, default: `uuidv7()`]
  expected_cod numeric(15,0) [not null, default: 0]
  cod_amount numeric(15,0) [not null, default: 0]
  shipping_fee numeric(15,0) [not null, default: 0]
  cod_fee numeric(15,0) [not null, default: 0]
  return_fee numeric(15,0) [not null, default: 0]
  other_fee numeric(15,0) [not null, default: 0]
  note text [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  settlement_id uuid [not null]
  order_id uuid [not null, unique]

  indexes {
    settlement_id
  }

  checks {
    `expected_cod >= 0 and cod_amount >= 0`
    `shipping_fee >= 0 and cod_fee >= 0 and return_fee >= 0 and other_fee >= 0`
  }
}

Ref: cart_items.user_id > users.id [delete: cascade]
Ref: cart_items.sku_id > product_skus.id [delete: cascade]
Ref: cart_serial_units.user_id > users.id [delete: cascade]
Ref: cart_serial_units.serial_unit_id > serial_units.id [delete: cascade]
Ref: order_items.serial_unit_id >? serial_units.id [delete: restrict]
Ref: orders.customer_id > users.id [delete: restrict]
Ref: orders.id -? order_shipping_details.order_id [delete: restrict]
Ref: order_shipping_details.shipping_method_id > shipping_methods.id [delete: restrict]
Ref: orders.id -? order_invoice_details.order_id [delete: restrict]
Ref: tax_rates.tax_class_id > tax_classes.id [delete: restrict]
Ref: products.tax_class_id > tax_classes.id [delete: restrict]
Ref: shipping_methods.tax_class_id > tax_classes.id [delete: restrict]
Ref: order_items.order_id > orders.id [delete: restrict]
Ref: order_items.sku_id > product_skus.id [delete: restrict]
Ref: order_payments.order_id > orders.id [delete: restrict]
Ref: order_payments.shift_id >? shifts.id [delete: restrict]
Ref: order_payments.(return_id, order_id) >? returns.(id, order_id) [delete: restrict]
Ref: orders.id -? voucher_usages.order_id [delete: restrict]
Ref: voucher_usages.voucher_id > vouchers.id [delete: restrict]
Ref: voucher_usages.customer_id >? users.id [delete: restrict]
Ref: voucher_usages.(order_id, customer_id) >? orders.(id, customer_id) [delete: restrict]

Table vouchers {
  id uuid [pk, default: `uuidv7()`]
  code varchar [null, unique, note: 'shared code; NULL when the voucher is a batch of unique codes']
  code_type varchar(20) [not null, default: 'SHARED', note: 'SHARED | UNIQUE_CODES']
  type varchar(20) [not null, note: 'PERCENTAGE | FIXED_AMOUNT']
  value numeric(15,0) [not null]
  max_discount_amount numeric(15,0) [null]
  min_order_value numeric(15,0) [null]
  usage_limit integer [null]
  used_count integer [not null, default: 0, note: 'active usages; reconciled nightly (R-REC-02)']
  starts_at timestamptz [not null]
  ends_at timestamptz [not null]
  applies_to_sale_items boolean [not null, default: true]
  once_per_customer boolean [not null, default: true]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  checks {
    `value > 0`
    `type <> 'PERCENTAGE' or value <= 100`
    `max_discount_amount is null or max_discount_amount > 0`
    `min_order_value is null or min_order_value >= 0`
    `usage_limit is null or usage_limit > 0`
    `used_count >= 0`
    `usage_limit is null or used_count <= usage_limit`
    `ends_at > starts_at`
    `(code_type = 'SHARED') = (code is not null)`
  }
}
Table voucher_codes {
  id uuid [pk, default: `uuidv7()`]
  code varchar(30) [not null, unique]
  is_active boolean [not null, default: true]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  voucher_id uuid [not null]

  indexes {
    (id, voucher_id) [unique]
    voucher_id
  }
}

Table voucher_targets {
  id uuid [pk, default: `uuidv7()`]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  voucher_id uuid [not null]
  category_id uuid [null]
  product_id uuid [null]

  indexes {
    voucher_id
    category_id
    product_id
  }

  checks {
    `(category_id is not null)::int + (product_id is not null)::int = 1`
  }
}

Table voucher_usages {
  id uuid [pk, default: `uuidv7()`]
  revoked_at timestamptz [null]
  once_per_customer boolean [not null, default: true, note: 'copied from vouchers at use time']
  created_at timestamptz [not null, default: `now()`]
  order_id uuid [not null]
  voucher_id uuid [not null]
  customer_id uuid [null, note: 'NULL for the walk-in account']
  voucher_code_id uuid [null]

  indexes {
    (voucher_id, customer_id)
    order_id [unique]
    customer_id
    voucher_code_id
  }
}

Table returns {
  id uuid [pk, default: `uuidv7()`]
  reason text [not null]
  status varchar(20) [not null, default: 'Requested', note: 'Requested | Approved | Rejected | Completed']
  resolution_type varchar(20) [null, note: 'REFUND | EXCHANGE']
  refund_amount numeric(15,0) [null]
  restocking_fee numeric(15,0) [not null, default: 0]
  restocking_fee_suggested numeric(15,0) [null, note: 'fee computed from the category fee table']
  fee_override_reason text [null]
  return_value numeric(15,0) [not null, default: 0, note: 'value of returned goods at the net price paid (after allocated discounts)']
  completed_at timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  order_id uuid [not null]
  exchange_order_id uuid [null]

  indexes {
    (id, order_id) [unique]
    order_id
    status
    completed_at
    exchange_order_id
  }

  checks {
    `refund_amount is null or refund_amount >= 0`
    `restocking_fee >= 0`
    `restocking_fee_suggested is null or restocking_fee = restocking_fee_suggested or fee_override_reason is not null`
    `return_value >= 0 and restocking_fee <= return_value`
    `refund_amount is null or refund_amount = return_value - restocking_fee`
    `(status = 'Completed') = (completed_at is not null)`
    `exchange_order_id is null or exchange_order_id <> order_id`
  }
}

Table return_items {
  id uuid [pk, default: `uuidv7()`]
  quantity integer [not null]
  return_value numeric(15,0) [not null, default: 0, note: 'value of this line at the net price paid']
  restock_to varchar(20) [null, note: 'MAIN | OUTLET | WARRANTY — set after inspection']
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  return_id uuid [not null]
  order_item_id uuid [not null]
  order_item_serial_id uuid [null]

  indexes {
    return_id
    order_item_id
    order_item_serial_id
  }

  checks {
    `quantity > 0`
    `return_value >= 0`
    `restock_to <> 'OUTLET' or order_item_serial_id is not null`
  }
}

Table warranty_requests {
  id uuid [pk, default: `uuidv7()`]
  code varchar(20) [not null, unique, note: 'BH + yyMMdd + sequence, e.g. BH260201-0007']
  request_type varchar(20) [not null, note: 'REPAIR | DOA']
  intake_method varchar(20) [not null, default: 'IN_STORE', note: 'IN_STORE | SHIPPED_IN']
  inbound_tracking_code varchar(50) [null]
  return_method varchar(20) [null, note: 'IN_STORE | SHIPPED']
  outbound_tracking_code varchar(50) [null]
  customer_issue text [not null]
  physical_condition text [not null]
  accessories_included text [null]
  seal_intact boolean [null, note: 'NULL = item has no seal']
  temporary_solution text [null]
  status varchar(20) [not null, default: 'Requested', note: 'Requested | Processing | Resolved | Rejected']
  resolution_type varchar(20) [null, note: 'REPAIRED | REPLACED | NO_FAULT | BUYBACK | REJECTED']
  buyback_amount numeric(15,0) [null]
  technical_notes text [null]
  supplier_rma_code varchar [null]
  expected_return_date date [not null]
  resolved_at timestamptz [null]
  replacement_warranty_ends_at date [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]
  order_item_serial_id uuid [null]
  assigned_to uuid [null]
  supplier_id uuid [null]
  order_item_id uuid [not null]
  replacement_serial_unit_id uuid [null]
  replacement_sku_id uuid [null]
  component_serial_unit_id uuid [null, note: 'faulty component inside a pre-built PC, after the whole-PC exchange window']

  indexes {
    order_item_serial_id
    status
    assigned_to
    supplier_id
    order_item_id
    replacement_serial_unit_id
    replacement_sku_id
    component_serial_unit_id
    resolved_at
  }

  checks {
    `intake_method = 'SHIPPED_IN' or inbound_tracking_code is null`
    `(status in ('Resolved', 'Rejected')) = (resolved_at is not null)`
    `status not in ('Resolved', 'Rejected') or resolution_type is not null`
    `(status = 'Rejected') = (resolution_type = 'REJECTED')`
    `(resolution_type = 'REPLACED') = (replacement_sku_id is not null)`
    `(resolution_type = 'BUYBACK') = (buyback_amount is not null)`
    `buyback_amount is null or buyback_amount > 0`
    `return_method = 'SHIPPED' or outbound_tracking_code is null`
    `(resolution_type = 'REPLACED') = (replacement_warranty_ends_at is not null)`
  }
}

Ref: returns.order_id > orders.id [delete: restrict]
Ref: returns.exchange_order_id >? orders.id [delete: restrict]
Ref: return_items.return_id > returns.id [delete: restrict]
Ref: return_items.order_item_id > order_items.id [delete: restrict]
Ref: return_items.(order_item_serial_id, order_item_id) >? order_item_serials.(id, order_item_id) [delete: restrict]
Ref: warranty_requests.(order_item_serial_id, order_item_id) >? order_item_serials.(id, order_item_id) [delete: restrict]
Ref: warranty_requests.order_item_id > order_items.id [delete: restrict]
Ref: warranty_requests.replacement_serial_unit_id >? serial_units.id [delete: restrict]
Ref: warranty_requests.assigned_to >? users.id [delete: restrict]
Ref: warranty_requests.supplier_id >? suppliers.id [delete: restrict]

Table daily_sequences {
  sequence_key varchar(30) [not null, note: 'ORDER | WARRANTY | CARRIER_SETTLEMENT']
  sequence_date date [not null]
  last_value integer [not null]
  created_at timestamptz [not null, default: `now()`]
  updated_at timestamptz [not null, default: `now()`]

  indexes {
    (sequence_key, sequence_date) [pk]
  }

  checks {
    `last_value > 0`
  }
}

Table import_jobs {
  id uuid [pk, default: `uuidv7()`]
  type varchar(30) [not null, note: 'PRODUCTS | PRODUCT_SPECS | PRICES | STOCK_COUNT | CUSTOMERS']
  status varchar(20) [not null, default: 'Validating', note: 'Validating | Ready | Committed | Failed | Cancelled']
  file_name varchar [not null]
  file_hash varchar(64) [not null, note: 'SHA-256 hex']
  total_rows integer [not null, default: 0]
  success_rows integer [not null, default: 0]
  error_rows integer [not null, default: 0]
  error_report jsonb [null]
  committed_at timestamptz [null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  updated_by uuid [null]
  updated_at timestamptz [not null, default: `now()`]

  indexes {
    (type, created_at)
    file_hash
  }

  checks {
    `total_rows >= 0 and success_rows >= 0 and error_rows >= 0`
    `success_rows + error_rows <= total_rows`
    `(status = 'Committed') = (committed_at is not null)`
  }
}

Table status_history {
  id uuid [pk, default: `uuidv7()`]
  from_status varchar(20) [null, note: 'Per parent — order: Pending | Confirmed | Processing | Shipped | Ready_for_pickup | Completed | Cancelled | Returned_to_sender; warranty request: Requested | Processing | Resolved | Rejected; return: Requested | Approved | Rejected | Completed']
  to_status varchar(20) [not null, note: 'Same value set as from_status, per parent']
  note text [null]
  transaction_id bigint [not null, default: `pg_current_xact_id()::text::bigint`, note: 'database transaction that wrote the row; groups the rows of one operation']
  correlation_id uuid [null, note: 'request id, same as in the application log']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]

  order_id uuid [null]
  warranty_request_id uuid [null]
  return_id uuid [null]

  indexes {
    (order_id, created_at)
    (warranty_request_id, created_at)
    (return_id, created_at)
    transaction_id
  }

  checks {
    `num_nonnulls(order_id, warranty_request_id, return_id) = 1`
  }
}
Table attachments {
  id uuid [pk, default: `uuidv7()`]
  kind varchar(10) [not null, note: 'IMAGE | VIDEO']
  file_url text [not null, note: 'object path in Firebase Storage']
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]
  serial_unit_id uuid [null]
  return_id uuid [null]
  warranty_request_id uuid [null]

  indexes {
    (serial_unit_id, created_at)
    (return_id, created_at)
    (warranty_request_id, created_at)
  }

  checks {
    `num_nonnulls(serial_unit_id, return_id, warranty_request_id) = 1`
  }
}
Table url_redirects {
  id uuid [pk, default: `uuidv7()`]
  old_path varchar(300) [not null, unique]
  new_path varchar(300) [not null]
  created_by uuid [null]
  created_at timestamptz [not null, default: `now()`]

  checks {
    `old_path <> new_path`
  }
}

Table notifications {
  id uuid [pk, default: `uuidv7()`]
  channel varchar(20) [not null, default: 'EMAIL', note: 'EMAIL']
  template_key varchar(100) [not null, note: 'order_confirmed | order_cancelled | order_shipped | order_ready_for_pickup | refund_created | warranty_resolved | warranty_rejected | low_stock_alert | reconciliation_report | marketing — defined in code']
  status varchar(20) [not null, default: 'Pending', note: 'Pending | Sent | Failed']
  payload jsonb [not null, default: `'{}'::jsonb`]
  attempt_count integer [not null, default: 0]
  next_attempt_at timestamptz [not null, default: `now()`]
  last_error text [null]
  sent_at timestamptz [null]
  created_at timestamptz [not null, default: `now()`]
  updated_at timestamptz [not null, default: `now()`]
  user_id uuid [null, note: 'account recipient']
  order_id uuid [null, note: 'order contact recipient (customer without an account email)']

  indexes {
    user_id
    order_id
  }

  checks {
    `attempt_count >= 0`
    `(status = 'Sent') = (sent_at is not null)`
    `num_nonnulls(user_id, order_id) = 1`
  }
}

Table audit_logs {
  id uuid [not null, default: `uuidv7()`]
  table_name varchar [not null, note: 'Any audited table — except those excluded by R-AUD-01']
  record_key jsonb [not null]
  action varchar(10) [not null, note: 'INSERT | UPDATE | DELETE']
  old_values jsonb [null]
  new_values jsonb [null]
  transaction_id bigint [not null, default: `pg_current_xact_id()::text::bigint`, note: 'database transaction that wrote the row; groups the rows of one operation']
  correlation_id uuid [null, note: 'request id, same as in the application log']
  changed_by uuid [null]
  changed_at timestamptz [not null, default: `now()`]

  indexes {
    (id, changed_at) [pk]
    (table_name, changed_at)
    (table_name, record_key)
    changed_by
    transaction_id
  }
}

Ref: orders.voucher_id >? vouchers.id [delete: restrict]
Ref: orders.sales_staff_id >? users.id [delete: restrict]

TableGroup phase1_store [note: 'Phase 1 — Store (in-store sales)'] {
  users
  customer_profiles
  staff_profiles
  roles
  permissions
  user_roles
  role_permissions
  countries
  provinces
  wards
  tax_classes
  tax_rates
  categories
  brands
  product_series
  products
  product_skus
  product_images
  business_locations
  warehouses
  suppliers
  purchase_orders
  purchase_order_items
  purchase_receipts
  stock_balances
  stock_ledger_entries
  serial_units
  adjustment_reasons
  stock_adjustments
  stock_counts
  stock_count_lines
  supplier_payments
  pc_build_components
  pc_assemblies
  pc_assembly_components
  orders
  order_invoice_details
  order_items
  order_item_serials
  order_item_components
  order_payments
  e_invoices
  returns
  return_items
  warranty_requests
  shifts
  shift_cash_adjustments
  cash_accounts
  cash_voucher_categories
  cash_vouchers
  daily_sequences
  import_jobs
  status_history
  attachments
  notifications
  audit_logs
}

TableGroup phase2_online [note: 'Phase 2 — Online sales'] {
  addresses
  cart_items
  carriers
  shipping_methods
  shipping_fee_rules
  order_shipping_details
  carrier_settlements
  carrier_settlement_lines
  url_redirects
  cart_serial_units
}

TableGroup phase3_operations [note: 'Phase 3 — Operations'] {
  purchase_returns
  purchase_return_items
  vouchers
  voucher_codes
  voucher_targets
  voucher_usages
}

TableGroup future [note: 'Future'] {
  wishlists
  product_badges
  product_badge_assignments
  product_tags
}
```

## Database Definition

### Phase 1: Store (in-store sales)

#### users

Login account shared by staff and customers; a customer is identified by email or phone (at least one).

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a1b2c3d4-...` |
| firebase_uid | Firebase Authentication link; NULL = placeholder created by Staff | `fK3nQx8p...`, NULL |
| name | Display name | Nguyễn Văn A |
| email | Login / contact email, lowercase; NULL for a customer identified by phone only | a@gmail.com, NULL |
| phone | Customer phone in E.164 — second identifier (counter lookup, phone sign-in) | +84901234567, NULL |
| phone_verified_at | When the customer proved ownership of the phone by OTP; required before merging by phone | 2026-02-01 10:00:00, NULL |
| status | Account status; ANONYMIZED = personal data erased on request, row kept for order history | ACTIVE, DISABLED, ANONYMIZED |
| last_login_at | Last successful sign-in, updated once per session | 2026-09-20 08:15:00, NULL |

#### customer_profiles

Customer-only profile data, 1-1 with `users`.

| Column | Description | Sample Data |
|---|---|---|
| user_id | Primary key and FK to `users` | `a1b2c3d4-...` |
| gender | Self-declared gender, optional | MALE, FEMALE, OTHER, NULL |
| date_of_birth | Optional; not in the future (validated in code) | 1998-05-20, NULL |
| marketing_consent | Customer agreed to receive marketing emails; transactional emails (order, warranty) do NOT depend on this | true, false |
| marketing_consent_at | When consent was last given — evidence of consent; required when `marketing_consent = true` | 2026-02-01 10:00:00, NULL |
| marketing_consent_source | Where consent was given; required when `marketing_consent = true` | SIGNUP, CHECKOUT, ACCOUNT, COUNTER, NULL |
| marketing_consent_version | Version of the consent wording the customer agreed to; required when `marketing_consent = true` | 2026-01, NULL |
| cod_allowed | false = cash on delivery disabled for this customer; VNPay only — Added in phase 2 | true, false |
| internal_note | Staff-only note, never shown to the customer | Khách quen, mua cho công ty, NULL |

#### staff_profiles

Staff-only profile data, 1-1 with `users`.

| Column | Description | Sample Data |
|---|---|---|
| user_id | Primary key and FK to `users` | `a1b2c3d4-...` |
| employee_code | Employee ID | NV0012, NULL |
| joined_date | Start date | 2025-06-01, NULL |
| work_phone | Internal contact number | 0901234567, NULL |
| left_date | Last working day; NULL = still employed | 2026-12-31, NULL |
| must_change_password | Force password change on first login | true, false |

#### roles

Role with a set of permissions.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b2c3d4e5-...` |
| name | Role name | Store Manager |
| scoping_type | Access scope | NONE, SELF |
| is_system_role | Locked role — permissions not editable via UI | true, false |
| max_discount_percent | Highest manual discount (% of the order after voucher) this role may give at the counter; NULL = cannot discount | 5.00, NULL |

#### permissions

A single grantable action.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c3d4e5f6-...` |
| key | Unique code used in access checks | order.confirm, price.update |
| description | Human-readable label | "Confirm a pending order" |

#### user_roles

Assigns roles to users.

| Column | Description | Sample Data |
|---|---|---|
| user_id | FK to `users` | `a1b2c3d4-...` |
| role_id | FK to `roles` | `b2c3d4e5-...` |

#### role_permissions

Assigns permissions to roles.

| Column | Description | Sample Data |
|---|---|---|
| role_id | FK to `roles` | `b2c3d4e5-...` |
| permission_id | FK to `permissions` | `c3d4e5f6-...` |

#### countries

Country reference data.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key (from seed source) | 238 |
| country_code | ISO 3166-1 alpha-2 | VN |
| country_code3 | ISO 3166-1 alpha-3 | VNM |
| country_number | ISO 3166-1 numeric | 704 |
| common_name | Common name | Vietnam |
| formal_name | Formal name | Socialist Republic of Vietnam |
| country_type | Type | Independent State |
| country_sub_type | Sub-type | NULL |
| sovereignty | Sovereignty | UN Member State |
| capital | Capital | Hanoi |
| currency_code | ISO 4217 | VND |
| currency_name | Currency name | Vietnamese Dong |
| telephone_code | Dialing code | 84 |
| internet_country_code | ccTLD | .vn |
| flag_url | Flag image URL — not emoji (Windows browsers do not render flag emoji) | https://cdn.../vn.svg |
| sort_order | Display order | 1 |
| is_published | Visible for selection | true, false |
| is_deleted | Soft-delete flag | true, false |

#### provinces

Vietnamese provinces and cities.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key (from seed source) | 79 |
| name | Province/city name | Thành phố Hồ Chí Minh |
| code | Administrative code | 79 |
| type | Type | Tỉnh, Thành phố |
| zip_code | Postal code | 700000, NULL |
| phone_code | Area dialing code | 28, NULL |
| is_active | Active flag | true, false |
| sort_order | Display order | 1 |
| country_id | FK to `countries` | 238 |

#### wards

Vietnamese communes, wards and special zones.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key (from seed source — code list of Quyết định 19/2025/QĐ-TTg) | code from the official list |
| name | Ward name | Phường Sài Gòn |
| code | Administrative code (Quyết định 19/2025/QĐ-TTg) | code from the official list |
| type | Type | Xã, Phường, Đặc khu |
| zip_code | Postal code | 700000, NULL |
| phone_code | Area dialing code | 28, NULL |
| is_active | Active flag | true, false |
| sort_order | Display order | 1 |
| province_id | FK to `provinces`; nullable to match the seed source (`ward >? province`) | 79, NULL |

#### categories

Product categories, two levels.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e5f6a7b8-...` |
| name | Category name | Linh kiện PC |
| slug | URL slug | linh-kien-pc |
| attribute_schema | JSON schema of attributes for products in this category (child categories only) | `{"socket": "string"}` |
| mega_menu_config | Mega menu content shown on hover (parent categories only); NULL = falls back to simple menu — Added in the future phase | `{"sections": [...]}` |
| doa_exchange_days | 1-for-1 exchange window for manufacturer defects (leaf categories) | 7, 30, NULL |
| return_days | Window for returning a non-defective item (leaf categories) | 14, NULL |
| return_fee_first_month_percent | Return fee in the first month after the order was completed, % of the returned value (leaf categories) | 20.00, 30.00, NULL |
| return_fee_monthly_percent | Fee added for each month after the first | 10.00, 5.00, NULL |
| is_active | Soft-delete flag: false = retired, still shown in history | true, false |
| parent_category_id | FK to `categories`; NULL = top-level | `e5f6a7b8-...`, NULL |

#### brands

Product brands.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f6a7b8c9-...` |
| name | Brand name | ASUS |
| slug | URL slug | asus |
| logo_url | Logo image URL | https://cdn.../asus.png |
| is_active | Soft-delete flag: false = retired, still shown in history | true, false |

#### product_series

Product lines within a brand.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a7b8c9d0-...` |
| name | Series name | ROG Strix |
| slug | URL slug | rog-strix |
| is_active | Soft-delete flag: false = retired, still shown in history | true, false |
| brand_id | FK to `brands` | `f6a7b8c9-...` |

#### products

Product shown on the storefront.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b8c9d0e1-...` |
| name | Product name | ASUS RTX 4070 OC 12GB |
| slug | URL slug, auto-generated at creation, not synced on rename | asus-rtx-4070-oc |
| description | Product description | "Card đồ họa hiệu năng cao..." |
| attributes | JSONB, validated against `categories.attribute_schema` | `{"socket": "PCIe 4.0"}` |
| variant_options | Ordered option names for the variant buttons; NULL = single-SKU product | `["ram", "ssd"]`, NULL |
| meta | JSONB, SEO metadata | `{"title": "...", "og_image": "..."}` |
| requires_serial | Warranty tracked by Serial/IMEI (CPU/GPU/Mainboard/Laptop) | true, false |
| warranty_months | Warranty period | 36, NULL |
| status | Product lifecycle status | DRAFT, PUBLISHED, DISCONTINUED |
| category_id | FK to `categories`; must be a child (leaf) category | `e5f6a7b8-...` |
| brand_id | FK to `brands` | `f6a7b8c9-...` |
| series_id | FK to `product_series`; optional | `a7b8c9d0-...`, NULL |
| tax_class_id | FK to `tax_classes` — business line for VAT on revenue, shared by every SKU of this product | `f1a2b3c4-...` |

#### product_skus

Sellable variant of a product, with its own price and stock.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c0d1e2f3-...` |
| sku | Business key, immutable after creation | ACER-A715-16GB-512 |
| barcode | Manufacturer barcode (EAN/UPC) for scanning at the counter and at receipt | 4711387123456, NULL |
| attributes | JSONB — the option values of this variant; keys must match exactly `products.variant_options`; unique per Product (no 2 SKUs with the same configuration) | `{"ram": "16GB", "ssd": "512GB"}`, NULL |
| price | Selling price — what the customer actually pays (Shopify convention) | 26890000 |
| compare_at_price | Original price shown struck-through; NULL = not on sale; must be > price (Shopify convention) | 27990000, NULL |
| low_stock_threshold | Minimum stock level; `INV-02` alerts when MAIN stock falls below it | 5, NULL |
| max_stock_threshold | Maximum stock level; `INV-02` alerts when MAIN stock exceeds it (capital tied up) | 30, NULL |
| sold_count | Running total sold, updated at `ORD-09`; reconciled nightly (R-REC-01) | 128 |
| average_cost | Moving weighted average cost, recalculated on every receipt; shown only to roles with cost.view; reconciled nightly (R-REC-10) | 15250000 |
| is_default | The variant pre-selected on the product page — exactly 1 per Product | true, false |
| is_service | Service line: skips stock deduction and serial recording | true, false |
| is_pc_build | Pre-built PC configuration sold as one product; its listed components are in `pc_build_components`; stock comes from assemblies | true, false |
| is_active | Available for sale | true, false |
| product_id | FK to `products` | `b8c9d0e1-...` |

#### product_images

Product images.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c9d0e1f2-...` |
| url | Image URL | https://cdn.../product_001.png |
| display_order | Sort order | 0, 1, 2 |
| is_primary | Marks the cover image — exactly one per product | true, false |
| product_id | FK to `products` | `b8c9d0e1-...` |
| sku_id | FK to `product_skus`; set only for an image specific to 1 variant (e.g. a color), NULL = shared by all variants | `c0d1e2f3-...`, NULL |

#### pc_build_components

Listed components of a pre-built PC configuration.

| Column | Description | Sample Data |
|---|---|---|
| pc_sku_id | FK to `product_skus` — the PC configuration (`is_pc_build`) | `c0d1e2f3-...` |
| component_sku_id | FK to `product_skus` — a component of it | `d1e2f3a4-...` |
| quantity | Units of this component in one PC | 1, 2 |

#### business_locations

The store's business location: address, contact and opening hours shown on the website, receipts and emails. Phase 1 holds exactly one row.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b0c1d2e3-...` |
| name | Location name | ZenGear Quận 10 |
| phone | Hotline | 02838123456, 1900636123 |
| email | Contact email | cskh@zengear.vn, NULL |
| address_detail | Street-level detail | 123 Sư Vạn Hạnh |
| regular_hours | Weekly opening periods, one entry per open period (day 1 = Monday) | `[{"day": 1, "open": "08:30", "close": "21:30"}]` |
| special_hours | Date overrides for holidays; `closed: true` or its own open/close | `[{"date": "2027-02-06", "closed": true}]`, NULL |
| province_id | FK to `provinces` | 79 |
| ward_id | FK to `wards`, must belong to `province_id` | 27000 |

#### warehouses

Store stock areas: main stock, warranty area, demo units on display and the outlet of open-box/ex-display units.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c1d2e3f4-...` |
| name | Warehouse name | Kho tổng, Kho bảo hành, Hàng trưng bày, Máy đổi trả |
| type | MAIN = new stock sold by quantity; WARRANTY = defective and in-repair units; DISPLAY = demo units, not for sale; OUTLET = open-box and ex-display units sold one by one at their own price | MAIN, WARRANTY, DISPLAY, OUTLET |
| is_active | Soft-delete flag: false = retired, still shown in history | true, false |

#### stock_balances

Current quantity of a SKU in a warehouse.

| Column | Description | Sample Data |
|---|---|---|
| sku_id | FK to `product_skus` — composite PK with `warehouse_id` | `c0d1e2f3-...` |
| warehouse_id | FK to `warehouses` | `c1d2e3f4-...` |
| quantity | Quantity on hand at this warehouse; reconciled nightly (R-REC-03, R-REC-04) | 42 |
| reserved_quantity | Units held by Pending orders; available to sell = quantity − reserved_quantity; reconciled nightly (R-REC-06) | 2 |

#### stock_ledger_entries

Append-only record of every stock change.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b3c4d5e6-...` |
| quantity_change | Signed change: + in, − out; never 0 | 20, -1 |
| balance_after | Quantity on hand at this warehouse right after the movement — stock card balance and stock at any point in time; reconciled nightly (R-REC-07) | 42, 0 |
| unit_cost | Cost per unit used for this movement (purchase cost incl. input VAT in, average cost out) | 15250000, NULL |
| movement_type | Why the stock changed; TRANSFER_OUT/TRANSFER_IN move one unit between MAIN, DISPLAY and OUTLET | PURCHASE_RECEIPT, ORDER_CONFIRM, WARRANTY_OUT, SUPPLIER_RETURN, TRANSFER_OUT |
| reference_type | Source document table — polymorphic, no FK | purchase_receipts, orders, return_items, NULL |
| reference_id | Source document id | `e1f2a3b4-...`, NULL |
| note | Free-text explanation (required in practice for OPENING/ADJUSTMENT) | "Tồn đầu kỳ khi go-live" |
| sku_id | FK to `product_skus` | `c0d1e2f3-...` |
| warehouse_id | FK to `warehouses` | `c1d2e3f4-...` |
| serial_unit_id | FK to `serial_units` (with `sku_id`) — the unit that moved; one row per unit for serial-tracked SKUs, NULL otherwise | `d4e5f6a7-...`, NULL |

#### serial_units

Physical units of serialized SKUs, tracked from receipt.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e4f5a6b7-...` |
| serial_number | Manufacturer serial/IMEI; unique per SKU | SN123456789 |
| status | Where the unit is in its life; RESERVED = held by a Pending order for an outlet unit; ASSEMBLED = component built into a PC unit | IN_STOCK, RESERVED, SOLD, ASSEMBLED, IN_WARRANTY, DEFECTIVE, RETURNED_TO_SUPPLIER, LOST |
| received_at | When the store received it — start of the supplier-side warranty | 2026-01-20 09:00:00 |
| condition | NEW; OPEN_BOX = returned by a customer, opened but not faulty; DISPLAY = used as a demo unit | NEW, OPEN_BOX, DISPLAY |
| condition_note | Appearance and accessories of a non-new unit, shown to buyers | Trầy nhẹ nắp lưng, đủ hộp sạc |
| resale_price | Price of this unit when sold from the outlet; NULL for new units | 13990000, NULL |
| resale_warranty_months | Warranty given with this unit when sold from the outlet | 6, NULL |
| sku_id | FK to `product_skus` | `c0d1e2f3-...` |
| warehouse_id | FK to `warehouses` — set while the store physically holds it (IN_STOCK, RESERVED, DEFECTIVE); NULL once it is with the customer or back at the supplier | `c1d2e3f4-...`, NULL |
| supplier_id | FK to `suppliers` — who the store bought it from; decides where to send it for warranty | `a3b4c5d6-...`, NULL |
| purchase_order_id | FK to `purchase_orders` it arrived with | `f2a3b4c5-...`, NULL |
| parent_serial_unit_id | FK to `serial_units` — the PC unit this component is built into; set exactly when ASSEMBLED | `f5a6b7c8-...`, NULL |

#### suppliers

Suppliers of goods.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a3b4c5d6-...` |
| name | Supplier name | Công ty TNHH Phân phối ABC |
| contact_info | Contact details | 028-1234-5678 |
| tax_code | Tax identification number | 0312345678 |
| opening_payable | Amount owed to the supplier at go-live, included in payables; negative = prepaid | 45000000, 0 |
| is_active | Soft-delete flag: false = retired, still shown in history | true, false |

#### purchase_orders

Purchase orders sent to suppliers.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f2a3b4c5-...` |
| status | Order status | Draft, Ordered, Partially_Received, Received, Cancelled |
| order_date | Order date | 2026-01-18 00:00:00 |
| supplier_id | FK to `suppliers` | `a3b4c5d6-...` |

#### purchase_order_items

Lines of a purchase order. One SKU may have several lines at different unit costs, e.g. a free line at cost 0 for a "buy 10 get 1" supplier promotion.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b4c5d6e7-...` |
| quantity_ordered | Quantity ordered | 50 |
| quantity_received | Quantity received so far on this line = Σ `purchase_receipts.quantity` of the line; reconciled nightly (R-REC-05) | 20, 50 |
| unit_cost | Agreed unit cost excluding VAT, as on the supplier invoice; inventory cost = unit_cost plus VAT at vat_rate because a household business does not deduct input VAT; different from `product_skus.price`; 0 = free units of the same SKU from a supplier promotion | 8500000, 0 |
| vat_rate | VAT rate on the supplier invoice, 0 when the invoice shows no VAT; payables = quantity × unit_cost plus VAT | 10.00, 0 |
| purchase_order_id | FK to `purchase_orders` | `f2a3b4c5-...` |
| sku_id | FK to `product_skus` | `c0d1e2f3-...` |

#### purchase_receipts

Goods received from suppliers, recorded against one purchase order line (source of supplier, SKU and unit cost).

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e1f2a3b4-...` |
| quantity | Quantity received | 20 |
| po_item_id | FK to `purchase_order_items` — the line being received | `b4c5d6e7-...` |

#### supplier_payments

Money paid to a supplier, or paid back by the supplier for returned goods.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e1f2a3b4-...` |
| type | PAYMENT = store pays supplier; REFUND = supplier pays store back | PAYMENT |
| method | How the money moved | BANK_TRANSFER, CASH |
| amount | Amount, always positive | 50000000 |
| reference | Bank or card reference; required unless cash | FT26020112345, NULL |
| paid_at | When the money moved | 2026-02-05 15:00:00 |
| note | Free note | Thanh toán đợt 1 |
| supplier_id | FK to `suppliers` | `a7b8c9d0-...` |
| purchase_order_id | FK to `purchase_orders` — the purchase being paid | `b8c9d0e1-...`, NULL |
| purchase_return_id | FK to `purchase_returns` — the return being refunded — Added in phase 3 | NULL |

#### adjustment_reasons

Reasons for stock adjustments.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d6e7f8a9-...` |
| key | Unique code | DAMAGED, LOST, MISCOUNT |
| label | Display label | Hàng hỏng, Thất lạc, Kiểm kê sai lệch |
| is_active | Soft-delete flag: false = retired, still shown in history | true, false |

#### stock_adjustments

Stock correction requests awaiting approval.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c5d6e7f8-...` |
| quantity_change | Amount to add/subtract from `stock_balances.quantity` | -2, 5 |
| status | Approval status | Requested, Approved, Rejected |
| sku_id | FK to `product_skus` | `c0d1e2f3-...` |
| warehouse_id | FK to `warehouses` — usually MAIN, can be WARRANTY | `c1d2e3f4-...` |
| reason_id | FK to `adjustment_reasons` | `d6e7f8a9-...` |
| serial_unit_id | FK to `serial_units` — the exact unit lost or found; required for serialized products (one unit per adjustment) | `e4f5a6b7-...`, NULL |

#### stock_counts

A stock count document for one warehouse: saved as draft while counting, completed to balance stock.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d3e4f5a6-...` |
| status | Draft while counting; Completed = stock balanced; Cancelled = merged or voided | Draft, Completed |
| note | Free note | Kiểm kê cuối quý 1, NULL |
| completed_at | When stock was balanced | 2026-03-31 18:30:00, NULL |
| completed_by | Store Manager who completed the count | `a1b2c3d4-...`, NULL |
| warehouse_id | FK to `warehouses` | `c1d2e3f4-...` |
| merged_into_id | FK to `stock_counts` — the combined draft this count was merged into | `e5f6a7b8-...`, NULL |

#### stock_count_lines

One counted item in a stock count: one line per SKU, or one line per serial for serialized SKUs.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f7a8b9c0-...` |
| counted_quantity | Quantity counted; always 1 on a serial line | 23, 1 |
| serial_number | Serial/IMEI counted; NULL for non-serialized SKUs | SN123456789, NULL |
| book_quantity | Book quantity captured at completion; NULL while draft | 24, NULL |
| note | Reason for a difference | 1 hộp móp, NULL |
| stock_count_id | FK to `stock_counts` | `d3e4f5a6-...` |
| sku_id | FK to `product_skus` | `c0d1e2f3-...` |

#### tax_classes

Business lines for VAT calculated as a percentage of revenue.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f1a2b3c4-...` |
| code | Unique code | GOODS_DISTRIBUTION, GOODS_RELATED_SERVICE, SERVICE |
| name | Display name | Phân phối, cung cấp hàng hóa |
| is_active | Soft-delete flag | true, false |

#### tax_rates

VAT-on-revenue rate of a business line over a period.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a2b3c4d5-...` |
| rate | Rate in percent of revenue | 1.00, 3.00, 5.00 |
| valid_from | Effective from (inclusive) | 2026-01-01 00:00:00+07 |
| valid_to | Effective until (exclusive); NULL = open-ended | NULL |
| tax_class_id | FK to `tax_classes` | `f1a2b3c4-...` |

#### shifts

Cashier shifts for cash reconciliation.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e3f4a5b6-...` |
| opening_cash | Cash at shift start | 2000000 |
| closing_cash_expected | = opening_cash + Σ CASH PAYMENT Paid − Σ CASH REFUND Paid of this shift (cash in minus cash handed back at the counter) | 5500000, NULL |
| closing_cash_actual | Actual counted cash at close | 5480000, NULL |
| status | Shift status | Open, Closed |
| staff_id | FK to `users` — the Cashier this shift belongs to | `a1b2c3d4-...` |

#### shift_cash_adjustments

Cash put into or taken out of the drawer outside of sales.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a6b7c8d9-...` |
| type | IN = cash added, OUT = cash taken out | IN, OUT |
| purpose | EXPENSE / OTHER_INCOME = operating expense or other income (voucher in the cash book); BANK_DEPOSIT = cash taken to the bank (internal transfer vouchers); TO_SAFE / FROM_SAFE = drawer ↔ safe, no voucher — the money stays in the cash fund | EXPENSE, TO_SAFE |
| amount | Amount, always positive | 200000 |
| reason | Why the cash moved | Trả phí ship, Nộp tiền về két |
| shift_id | FK to `shifts` | `e3f4a5b6-...` |
| category_id | FK to `cash_voucher_categories` — required for EXPENSE and OTHER_INCOME | `d2e3f4a5-...`, NULL |

#### cash_accounts

Cash book accounts: the cash fund, bank accounts and e-wallets.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c1d2e3f4-...` |
| name | Display name | Quỹ tiền mặt, VCB 0071000123456 |
| type | Account type | CASH, BANK, EWALLET |
| bank_name | Bank name; required for BANK | Vietcombank, NULL |
| account_number | Bank account number; required for BANK | 0071000123456, NULL |
| is_default | Default account of its type for automatic vouchers (one per type) | true, false |
| is_active | Soft-delete flag | true, false |

#### cash_voucher_categories

Receipt and payment categories of the cash book.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d2e3f4a5-...` |
| name | Category name, unique per direction | Thu tiền bán hàng, Tiền điện, Trả tiền nhà cung cấp |
| direction | RECEIPT or PAYMENT | RECEIPT, PAYMENT |
| affects_business_result | Default for vouchers: counted in business results (operating income or expense) | true, false |
| is_system | Category used by automatic vouchers | true, false |
| is_active | Soft-delete flag | true, false |

#### cash_vouchers

Receipt and payment vouchers of the cash book — automatic from sales, purchasing and payments, or entered manually.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e3f4a5b6-...` |
| code | Voucher number (PT = receipt, PC = payment) | PT260201-0001, PC260201-0003 |
| direction | RECEIPT = money in, PAYMENT = money out | RECEIPT, PAYMENT |
| amount | Amount, always positive | 1500000 |
| occurred_at | When the money moved | 2026-02-01 10:00:00 |
| counterparty_type | Who paid or was paid | SUPPLIER, CUSTOMER, OTHER |
| counterparty_name | Name of the counterparty | Công ty TNHH Điện lực, NULL |
| description | Content of the voucher | Tiền điện tháng 01/2026 |
| document_number | Number of the invoice or document behind the voucher | 00001234, NULL |
| affects_business_result | Counted in business results; false for transfers, owner contributions and withdrawals | true, false |
| status | Active; Cancelled vouchers stay in the book | Active, Cancelled |
| cancel_reason | Why the voucher was cancelled | Nhập sai quỹ, NULL |
| cancelled_at | When it was cancelled | NULL |
| source_type | Table of the business record that created the voucher; NULL = manual | order_payments, NULL |
| source_id | Id of that record | `f4a5b6c7-...`, NULL |
| cash_account_id | FK to `cash_accounts` — account the money moved in or out of | `c1d2e3f4-...` |
| counter_account_id | FK to `cash_accounts` — destination of an internal transfer | NULL |
| category_id | FK to `cash_voucher_categories` | `d2e3f4a5-...` |

#### orders

Customer orders.

Before Phase 3 there are no vouchers: `ck_orders_discount_manual_only` keeps `discount_amount = manual_discount_amount`; Phase 3 replaces it with `ck_orders_discount_without_voucher` when `voucher_id` is added. In Phase 1 orders are created at the counter with `fulfillment_method = PICKUP`.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f8a9b0c1-...` |
| order_code | Human-readable order number for hotline/invoice, generated at creation | ZG260201-0042 |
| type | Order channel | SELF_SERVICE, ASSISTED |
| fulfillment_method | Delivery mode | SHIP, PICKUP |
| recipient_name | Delivery recipient (snapshot) | Trần Thị B |
| contact_phone | Contact phone (snapshot) | 0909123456 |
| status | Order status | Pending, Confirmed, Processing, Shipped, Ready_for_pickup, Completed, Cancelled, Returned_to_sender |
| subtotal | Sum of `order_items` (quantity × unit_price), snapshot | 16990000 |
| discount_amount | Total discount = voucher discount + manual discount | 1500000 |
| manual_discount_amount | Discount given by staff at the counter | 500000, 0 |
| manual_discount_reason | Why the manual discount was given; required when there is one | Khách quen, mua 2 máy, NULL |
| manual_discount_by | Staff whose discount cap covered it | `a1b2c3d4-...`, NULL |
| shipping_fee | Shipping fee at order time — snapshot, NOT read live from `shipping_methods` | 30000, 0 |
| total_amount | = subtotal − discount_amount + shipping_fee (enforced by CHECK) | 16520000 |
| shipping_vat_rate | VAT-on-revenue rate of the business line of the shipping fee (snapshot) — used to split revenue by line | 1.00, 0 |
| idempotency_key | Key generated by the client per checkout; a repeated request returns the existing order | `9f1c2d3e-...`, NULL |
| completed_at | When the order became Completed — basis for revenue date and warranty start | 2026-02-03 16:20:00, NULL |
| customer_id | FK to `users` — order owner | `a1b2c3d4-...` |
| voucher_id | FK to `vouchers`, if applied — Added in phase 3 | `d2e3f4a5-...`, NULL |
| sales_staff_id | FK to `users` — salesperson credited with the sale (staff reports, future commission) | `b2c3d4e5-...`, NULL |

#### order_invoice_details

Buyer details printed on the invoice of an order, for a company or a household business.

| Column | Description | Sample Data |
|---|---|---|
| order_id | Primary key and FK to `orders` | `f8a9b0c1-...` |
| buyer_type | COMPANY = enterprise with a 10- or 13-digit tax code; HOUSEHOLD_BUSINESS = household or individual business, identified by the 12-digit personal ID that replaces its tax code from 2025-07-01 | COMPANY, HOUSEHOLD_BUSINESS |
| company_name | Company or household business name on the invoice | Công ty TNHH ABC, Hộ kinh doanh Net Minh Anh |
| tax_code | 10 digits, or 13 digits (10-3, with or without the dash) for a dependent unit; 12-digit personal ID for a household business (CHECK) | 0312345678, 079090001234 |
| address | Registered business address | 12 Nguyễn Trãi, Phường Sài Gòn, TP.HCM |
| email | Email to receive the invoice | ketoan@abc.vn, NULL |

#### order_items

Lines of an order.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a9b0c1d2-...` |
| quantity | Quantity | 1, 2 |
| unit_price | Price at order time (snapshot) | 15990000 |
| product_name | Product name at order time (snapshot) | Acer Aspire 7 A715 |
| sku_code | SKU code at order time (snapshot) | ACER-A715-16GB-512 |
| variant_attributes | Variant attributes at order time (snapshot) | `{"ram": "16GB"}`, NULL |
| warranty_months | Warranty period at order time (snapshot, never changes afterwards) | 24, NULL |
| discount_allocated | Share of the order-level voucher allocated to this line (pro-rata by line value; rounding remainder goes to the last line) | 480000, 0 |
| vat_rate | VAT-on-revenue rate of the product business line (snapshot); re-read at completion — revenue per rate feeds the tax books | 1.00, 5.00 |
| unit_cost | Average cost per unit when the stock left (order confirmation); basis of gross profit | 15250000, NULL |
| item_condition | Condition of the unit sold (snapshot); non-NEW lines are outlet units priced per unit | NEW, OPEN_BOX, DISPLAY |
| serial_unit_id | FK to `serial_units` — the outlet unit chosen at order time; unit_price and warranty_months are copied from its resale_price and resale_warranty_months | `e4f5a6b7-...`, NULL |
| pc_fulfillment | PC build lines only: STOCK = a pre-built PC of the listed configuration is reserved; BUILD = the chosen components are reserved and a PC is assembled for this order | STOCK, BUILD, NULL |
| order_id | FK to `orders` | `f8a9b0c1-...` |
| sku_id | FK to `product_skus` | `c0d1e2f3-...` |

#### order_item_serials

Physical units sold in an order line.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d1e2f3a4-...` |
| order_item_id | FK to `order_items` | `a9b0c1d2-...` |
| serial_unit_id | FK to `serial_units` — the physical unit sold | `e4f5a6b7-...` |

#### order_item_components

Components of a PC build line as ordered — the listed configuration with the customer's swaps. The line price is the listed PC price plus the retail price difference of the swapped components.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e2f3a4b5-...` |
| quantity | Units of this component in one PC | 1, 2 |
| unit_price | Retail price of the component at order time (snapshot) | 2490000 |
| order_item_id | FK to `order_items` — the PC build line | `a9b0c1d2-...` |
| component_sku_id | FK to `product_skus` — component in the ordered PC | `d1e2f3a4-...` |
| replaced_sku_id | FK to `product_skus` — listed component this one replaces; NULL when kept as listed or added | `f3a4b5c6-...`, NULL |

#### pc_assemblies

Assembly of one PC unit from components — built ahead for stock, or for a specific order line.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a4b5c6d7-...` |
| code | Assembly code | LR260210-0002 |
| status | Draft → Completed (components out, PC in) or Cancelled | Completed |
| unit_cost | Σ component cost — cost of the PC unit | 21350000, NULL |
| completed_at | When the PC unit was put into stock | 2026-02-10 15:00:00, NULL |
| pc_sku_id | FK to `product_skus` — the PC configuration built | `c0d1e2f3-...` |
| order_item_id | FK to `order_items` — the order line it is built for; NULL = built ahead for stock | `a9b0c1d2-...`, NULL |
| pc_serial_unit_id | FK to `serial_units` — the PC unit produced (one unit per assembly) | `f5a6b7c8-...`, NULL |
| assembled_by | FK to `users` — Technical Support who built it | `a1b2c3d4-...`, NULL |

#### pc_assembly_components

Components actually used in an assembly.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b5c6d7e8-...` |
| quantity | Units used; 1 on a serial line | 1, 2 |
| unit_cost | Average cost of the component when it left stock | 2150000, NULL |
| warranty_months | Warranty of this component (snapshot); applies after the whole-PC exchange window | 36, NULL |
| assembly_id | FK to `pc_assemblies` | `a4b5c6d7-...` |
| component_sku_id | FK to `product_skus` | `d1e2f3a4-...` |
| serial_unit_id | FK to `serial_units` — the component unit built in, for serialized components | `a7b8c9d0-...`, NULL |

#### order_payments

Payments and refunds of an order.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e2f3a4b5-...` |
| type | PAYMENT = money in; REFUND = money back to the customer (cancelled prepaid order, return). Amount stays positive, the type gives the direction | PAYMENT, REFUND |
| method | CASH = cash at the counter; COD = cash collected by the carrier on delivery; BANK_TRANSFER; CARD_POS = card on the counter terminal (incl. card installments); VNPAY; INSTALLMENT_FINANCE = loan from a consumer finance company | CASH, COD, CARD_POS |
| amount | Amount for this payment line | 15990000 |
| status | Pending → Paid, or for COD Pending → Collected (carrier took the cash) → Paid (carrier settlement paid out); Failed | Pending, Collected, Paid, Failed |
| transaction_id | Bank, VNPay or card terminal reference, or the finance disbursement reference; required once a non-cash line is Paid | VNP14123456, FT26032123456, NULL |
| expires_at | VNPay payment deadline (vnp_ExpireDate) — Added in phase 2 | 2026-02-01 10:15:00, NULL |
| finance_provider | Consumer finance company, only for INSTALLMENT_FINANCE | HD SAISON, NULL |
| finance_contract_no | Loan contract number, only for INSTALLMENT_FINANCE | HDS-2602-001234, NULL |
| shift_id | FK to `shifts` — every payment taken at the counter (CASH, CARD_POS, BANK_TRANSFER); only CASH counts toward expected cash | `e3f4a5b6-...`, NULL |
| order_id | FK to `orders` | `f8a9b0c1-...` |
| return_id | FK to `returns` — set only on a REFUND row paying back a return | `f4a5b6c7-...`, NULL |
| warranty_request_id | FK to `warranty_requests` — set on a REFUND paying a warranty buyback | `a5b6c7d8-...`, NULL |
| refund_of_payment_id | FK to `order_payments` — the PAYMENT line a REFUND pays back; required for VNPAY, CARD_POS and INSTALLMENT_FINANCE refunds, which go back through the original channel (VNPay refund needs the original transaction reference) | `e2f3a4b5-...`, NULL |

#### e_invoices

Electronic sales invoices (hóa đơn bán hàng) issued through the e-invoice provider: one invoice per order, adjustments for returns.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a9b0c1d2-...` |
| invoice_type | SALE, or an adjustment/replacement of an issued invoice | SALE, ADJUSTMENT_DECREASE |
| status | Pending → Issued; Failed after provider errors; Cancelled | Issued, Pending |
| provider | E-invoice provider used | MISA |
| amount | Invoice total (tax included in the price) | 18990000 |
| invoice_symbol | Invoice symbol; the leading 2 is the sales-invoice form, the letter after the year is M for an invoice generated from a cash register, T for a regular coded invoice | 2C26MAA, 2C26TAA |
| invoice_number | Invoice number | 0000123, NULL |
| tax_authority_code | Code issued by the tax authority | M1-26-ABC12-00000000123, NULL |
| lookup_code | Code the buyer uses to look up the invoice | 8F3K2L, NULL |
| provider_ref | Provider transaction id — makes retries idempotent | a1b2c3, NULL |
| issued_at | When the invoice was issued | 2026-02-01 10:05:00, NULL |
| attempt_count | Send attempts so far | 1 |
| last_error | Last provider error | NULL |
| order_id | FK to `orders` | `f1a2b3c4-...` |
| return_id | FK to `returns` — the return an adjustment is for | NULL |
| original_invoice_id | FK to `e_invoices` — the invoice being adjusted or replaced | NULL |

#### daily_sequences

Per-day counters used to generate readable codes such as `order_code`.

| Column | Description | Sample Data |
|---|---|---|
| sequence_key | Which numbering series | ORDER, WARRANTY, CARRIER_SETTLEMENT |
| sequence_date | Business date (Vietnam time) | 2026-02-01 |
| last_value | Last number issued for that date | 42 |

#### import_jobs

One Excel import run: validated first, then committed.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c9d0e1f2-...` |
| type | What the file imports | PRODUCTS, PRICES, STOCK_COUNT |
| status | Validating → Ready (preview) → Committed; Failed / Cancelled | Ready, Committed |
| file_name | Uploaded file name | bang-gia-2026-02.xlsx |
| file_hash | SHA-256 of the file — warns when the same file is imported again | `9f86d081...` |
| total_rows | Data rows in the file | 500 |
| success_rows | Rows accepted | 480 |
| error_rows | Rows rejected | 20 |
| error_report | Per-row errors | [{"row": 12, "error": "SKU không tồn tại"}], NULL |
| committed_at | When the data was written | 2026-02-01 10:15:00, NULL |

#### status_history

Status change timeline of orders, warranty requests and returns.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b0c1d2e3-...` |
| from_status | Previous status; NULL for the first entry | Pending, NULL |
| to_status | New status | Confirmed, Processing |
| note | Reason — required for Cancelled (order) and Rejected (warranty) | "Khách đổi ý", NULL |
| transaction_id | Database transaction that wrote the row; all rows of one operation share it | 1234567 |
| correlation_id | Request id, also written to the application log | `9f8e7d6c-...`, NULL |
| order_id | FK to `orders` — set when the row belongs to an order (exactly one of the three parents) | `f8a9b0c1-...`, NULL |
| warranty_request_id | FK to `warranty_requests` | NULL |
| return_id | FK to `returns` | NULL |

#### attachments

Photos and videos attached to an outlet unit, a return or a warranty request — condition photos of a unit for sale, unboxing video as return evidence, intake photos of a warranty unit.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c2d3e4f5-...` |
| kind | File type | IMAGE, VIDEO |
| file_url | Object path in Firebase Storage; the file itself is never stored in the database | attachments/returns/f4a5.../unbox.mp4 |
| serial_unit_id | FK to `serial_units` — condition photos of an outlet unit (exactly one of the three parents) | `e4f5a6b7-...`, NULL |
| return_id | FK to `returns` — return evidence | NULL |
| warranty_request_id | FK to `warranty_requests` — intake photos | NULL |

#### returns

Return and exchange cases.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f4a5b6c7-...` |
| reason | Why the customer wants to return/exchange | "Đặt nhầm màu" |
| status | Case status | Requested, Approved, Rejected, Completed |
| resolution_type | Outcome | REFUND, EXCHANGE, NULL |
| refund_amount | Total amount refunded for this case (REFUND only); reconciled nightly (R-REC-09) | 15990000, NULL |
| restocking_fee | Fee kept when a non-defective item is returned | 500000, 0 |
| restocking_fee_suggested | Fee computed from the category fee table: returned value × (first-month % + monthly % × months after the first), capped at 100% | 3798000, NULL |
| fee_override_reason | Why the fee differs from the suggestion; required when it does, approved by the Store Manager | Khách VIP, giảm phí theo duyệt, NULL |
| return_value | Value of the returned goods at the net price actually paid; refund_amount = return_value − restocking_fee | 18990000 |
| completed_at | When the return was completed; used to date returns in revenue reports | 2026-02-12 16:00:00, NULL |
| order_id | FK to `orders` — the original order | `f8a9b0c1-...` |
| exchange_order_id | FK to `orders` — the new order created for EXCHANGE, if any | `a1b2c3d4-...`, NULL |

#### return_items

Lines of a return case.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a5b6c7d8-...` |
| quantity | Quantity returned | 1 |
| return_value | Value of this line at the net price actually paid (unit_price − allocated discount); Σ over the lines = `returns.return_value` | 18990000 |
| restock_to | Set by Warehouse Keeper after inspection: MAIN = sealed, sold as new; OUTLET = opened but not faulty, resold as open-box (serialized units only); WARRANTY = faulty; NULL = not inspected yet | MAIN, OUTLET, WARRANTY, NULL |
| return_id | FK to `returns` | `f4a5b6c7-...` |
| order_item_id | FK to `order_items` | `a9b0c1d2-...` |
| order_item_serial_id | FK to `order_item_serials` — the exact unit returned; required for serialized products (one row per unit) | `d1e2f3a4-...`, NULL |

#### warranty_requests

Warranty cases for a sold unit.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b6c7d8e9-...` |
| code | Ticket code given to the customer; used for public lookup | BH260201-0007 |
| request_type | Type of request | REPAIR, DOA |
| intake_method | How the item arrived: at the store or sent in by carrier | IN_STORE, SHIPPED_IN |
| inbound_tracking_code | Tracking code of the parcel the customer sent in | GHN123456, NULL |
| return_method | How the unit goes back to the customer; SHIPPED = store sends it at its own cost | IN_STORE, SHIPPED, NULL |
| outbound_tracking_code | Tracking code of the parcel sending the unit back | GHN654321, NULL |
| customer_issue | Fault as described by the customer | Không lên nguồn |
| physical_condition | Appearance recorded at intake (scratches, dents, stickers) | Trầy nhẹ cạnh trái |
| accessories_included | Accessories handed in with the item | Sạc, hộp, NULL |
| seal_intact | Warranty seal still intact; NULL = item has no seal | true, NULL |
| temporary_solution | Substitute offered while the unit is under warranty service, or the alternative the customer accepted | Cho mượn chuột G102, Khách không cần |
| status | Case status | Requested, Processing, Resolved, Rejected |
| resolution_type | REPAIRED; REPLACED (same or equivalent SKU); NO_FAULT (nothing found, item returned); BUYBACK (taken back with depreciation, money refunded); REJECTED (not covered) | REPAIRED, NO_FAULT |
| buyback_amount | Amount refunded for a BUYBACK | 12000000, NULL |
| supplier_rma_code | Ticket code issued by the supplier/brand service center, to track the unit sent out | RMA-ASUS-240915-017, NULL |
| expected_return_date | Promised date to hand the item back, set at intake | 2026-02-15 |
| resolved_at | When the ticket was resolved or rejected | 2026-02-14 10:00:00, NULL |
| technical_notes | Technical Support's notes | "Lỗi nguồn, đã thay tụ" |
| order_item_serial_id | FK to `order_item_serials` — the unit sold; NULL for products without serial tracking | `d1e2f3a4-...`, NULL |
| assigned_to | FK to `users` — Technical Support handling it | `a1b2c3d4-...`, NULL |
| supplier_id | FK to `suppliers` — supplier/brand service center the unit was sent to for repair; NULL = handled in-house | `a3b4c5d6-...`, NULL |
| order_item_id | FK to `order_items` — the sale line covered by this case | `a9b0c1d2-...` |
| replacement_serial_unit_id | FK to `serial_units` — new unit given to the customer when resolved as REPLACED | `e4f5a6b7-...`, NULL |
| replacement_sku_id | FK to `product_skus` — SKU given as replacement, same or equivalent | `c0d1e2f3-...`, NULL |
| replacement_warranty_ends_at | Warranty end of the replacement, counted again from the replacement date; required when REPLACED | 2028-02-14, NULL |
| component_serial_unit_id | FK to `serial_units` — the faulty component inside a pre-built PC, once the whole-PC exchange window has passed | `a7b8c9d0-...`, NULL |

#### notifications

Outgoing notification queue.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c7d8e9f0-...` |
| channel | Delivery channel | EMAIL |
| template_key | Which email template to use | order_confirmed, low_stock_alert |
| status | Delivery status | Pending, Sent, Failed |
| payload | Data used to render the message | {"sku": "L-1", "quantity": 2} |
| attempt_count | Delivery attempts so far; Failed after 3 | 0, 2 |
| next_attempt_at | Earliest time of the next attempt (increasing back-off) | 2026-02-01 10:05:00 |
| last_error | Error of the latest failed attempt | SMTP 421 timeout, NULL |
| sent_at | When the message was delivered | 2026-02-01 10:00:03, NULL |
| user_id | FK to `users` — recipient account (exactly one of `user_id`, `order_id`) | `a1b2c3d4-...`, NULL |
| order_id | FK to `orders` — message sent to the order contact | NULL |

#### audit_logs

Field-level change history.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f9a0b1c2-...` |
| table_name | Which table changed — polymorphic, no FK possible | products, vouchers |
| record_key | PK of the changed row as JSON — works for uuid, integer and composite keys | `{"id": "b8c9…"}`, `{"sku_id": "…", "warehouse_id": "…"}` |
| action | Type of change | INSERT, UPDATE, DELETE |
| old_values | Row state before the change (UPDATE/DELETE only) | `{"price": 27990000}`, NULL |
| new_values | Row state after the change (INSERT/UPDATE only) | `{"price": 25990000}`, NULL |
| transaction_id | Database transaction that wrote the row; all rows of one operation share it | 1234567 |
| correlation_id | Request id, also written to the application log | `9f8e7d6c-...`, NULL |
| changed_by | Who made the change | `a1b2c3d4-...`, NULL |
| changed_at | Change time | 2026-02-15 09:00:00 |

### Phase 2: Online sales

#### addresses

A customer's saved delivery addresses.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d4e5f6a7-...` |
| recipient_name | Delivery recipient's name | Trần Thị B |
| phone | Recipient's contact phone | 0909123456 |
| address_detail | Street-level detail | 12 Nguyễn Trãi |
| label | Self-tagged category | Home, Office |
| is_default | Default address for checkout | true, false |
| user_id | FK to `users` — owner of this address | `a1b2c3d4-...` |
| country_id | FK to `countries` | 238 |
| province_id | FK to `provinces` | 79, NULL |
| ward_id | FK to `wards` | 27000, NULL |

#### cart_items

Items in a customer's cart.

| Column | Description | Sample Data |
|---|---|---|
| user_id | FK to `users` | `a1b2c3d4-...` |
| sku_id | FK to `product_skus` — price is NOT snapshotted here, recalculated on display | `b8c9d0e1-...` |
| quantity | Quantity | 2 |

#### cart_serial_units

Specific outlet units a customer put in the cart; each unit is sold once, so it is held by unit rather than by quantity.

| Column | Description | Sample Data |
|---|---|---|
| user_id | FK to `users` | `a1b2c3d4-...` |
| serial_unit_id | FK to `serial_units` — an IN_STOCK unit of the OUTLET warehouse | `e4f5a6b7-...` |

#### carriers

Delivery partners the store hands parcels to, including IN_HOUSE for the store's own delivery staff.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `a3b4c5d6-...` |
| code | Stable carrier code; future API adapters are registered under it; IN_HOUSE = store delivery staff | GHN, GHTK, IN_HOUSE |
| name | Display name | Giao Hàng Nhanh |
| integration_type | MANUAL = tracking and settlements entered by staff; API = synced by an adapter (future) | MANUAL |
| cod_settlement_days | Days the carrier contract allows to pay COD after delivery; COD collected longer than this and not yet on a Paid settlement raises an alert | 5, NULL |
| is_active | Soft-delete flag | true |

#### shipping_methods

Shipping options and fees.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e7f8a9b0-...` |
| name | Method name | Giao hàng tiêu chuẩn |
| base_fee | Fallback fee when no fee rule matches | 30000 |
| is_active | Visible for selection at checkout | true, false |
| tax_class_id | FK to `tax_classes` — business line of the shipping fee | `f1a2b3c4-...` |
| carrier_id | FK to `carriers` — default carrier of this method; IN_HOUSE = store delivery staff; NULL = chosen when shipped | `a3b4c5d6-...`, NULL |

#### shipping_fee_rules

Shipping fee by order value and province, e.g. free above 5 million in selected cities.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d6e7f8a9-...` |
| min_order_value | Rule applies when the order (after discounts) reaches this value | 0, 5000000 |
| fee | Fee charged when the rule applies | 40000, 0 |
| is_active | Rule in use | true |
| shipping_method_id | FK to `shipping_methods` | `e7f8a9b0-...` |
| province_id | FK to `provinces` — NULL = every province | 79, NULL |

#### order_shipping_details

Delivery address of a shipped order.

| Column | Description | Sample Data |
|---|---|---|
| order_id | Primary key and FK to `orders` | `f8a9b0c1-...` |
| shipping_country | Country name (snapshot) | Việt Nam |
| shipping_province | Province/city name (snapshot); NULL only for a foreign address without seed data | TP. Hồ Chí Minh, NULL |
| shipping_ward | Ward name (snapshot) | Phường Sài Gòn, NULL |
| shipping_address_detail | Street-level detail (snapshot) | 12 Nguyễn Trãi |
| tracking_code | Carrier waybill number, entered when the order is shipped | GHN5FK3XY7Q, NULL |
| shipping_method_id | FK to `shipping_methods` — live FK, not a snapshot (needed to print the waybill) | `e7f8a9b0-...` |
| carrier_id | FK to `carriers` — carrier that actually took the parcel, required once Shipped (IN_HOUSE for store delivery); required when a tracking code is entered | `a3b4c5d6-...`, NULL |

#### carrier_settlements

A settlement statement from a carrier for one period: COD it collected minus its fees, paid to the store as one transfer — or, for IN_HOUSE, cash handed in by store delivery staff at the counter.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b4c5d6e7-...` |
| code | Settlement code | DS260210-0003 |
| status | Draft → Confirmed (lines checked) → Paid (money received); Cancelled | Confirmed |
| source | MANUAL now; FILE_IMPORT / API when carrier integration is added | MANUAL |
| external_ref | Settlement id on the carrier side, unique per carrier | GHN-DS-2602-118, NULL |
| period_from | First day covered | 2026-02-03 |
| period_to | Last day covered | 2026-02-09 |
| total_cod | Σ cod_amount of the lines | 125400000 |
| total_fees | Σ shipping, COD, return and other fees of the lines | 1860000 |
| previous_balance | Carry-over from earlier periods (+ carrier owes, − store owes) | 0, -45000 |
| net_amount | total_cod − total_fees + previous_balance | 123540000 |
| bank_reference | Transfer reference of the payout; not needed when IN_HOUSE cash is handed in at the counter | FT26021012345, NULL |
| shift_id | FK to `shifts` — counter shift that received the cash from IN_HOUSE delivery staff; counts toward that shift's expected cash | `e3f4a5b6-...`, NULL |
| confirmed_at | When the lines were checked | 2026-02-10 09:00:00, NULL |
| confirmed_by | Staff who confirmed | `a1b2c3d4-...`, NULL |
| paid_at | When the payout arrived | 2026-02-11 14:00:00, NULL |
| note | Free note | NULL |
| carrier_id | FK to `carriers` | `a3b4c5d6-...` |

#### carrier_settlement_lines

One order on a settlement: what the carrier collected for it and what it charged.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c5d6e7f8-...` |
| expected_cod | COD amount the order should have collected | 20000000, 0 |
| cod_amount | COD amount the carrier reports | 20000000 |
| shipping_fee | Carrier's delivery fee | 45000 |
| cod_fee | Carrier's cash-collection fee | 10000 |
| return_fee | Fee for returning a failed delivery | 0, 22000 |
| other_fee | Any other fee (insurance, remote area) | 0 |
| note | Required explanation when cod_amount differs from expected_cod | NULL |
| settlement_id | FK to `carrier_settlements` | `b4c5d6e7-...` |
| order_id | FK to `orders` — each order is settled once | `f8a9b0c1-...` |

#### url_redirects

Permanent (301) redirects from an old storefront path to the new one, written automatically when a product or category slug changes so saved links and search rankings keep working.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d3e4f5a6-...` |
| old_path | Path that no longer exists; unique | /laptop-acer-aspire-7-a715 |
| new_path | Path to redirect to; chains are collapsed when a slug changes again | /laptop-acer-aspire-7-a715-2026 |

### Phase 3: Operations

#### purchase_returns

A return of received goods to the supplier, created from the original purchase order.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `f6a7b8c9-...` |
| status | Draft → Completed (stock and payables updated) or Cancelled | Completed |
| reason | Why the goods go back | Lỗi quy cách, NCC nhận lại |
| total_amount | Σ quantity × unit_cost of the lines plus the VAT refunded — reduces payables; reconciled nightly (R-REC-08) | 9119000 |
| vat_amount | Input VAT refunded by the supplier on this return | 829000 |
| completed_at | When stock and payables were updated | 2026-02-10 09:00:00, NULL |
| supplier_id | FK to `suppliers` | `a7b8c9d0-...` |
| purchase_order_id | FK to `purchase_orders` — the original purchase | `b8c9d0e1-...` |

#### purchase_return_items

One returned purchase order line; one line per unit for serialized SKUs.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `c9d0e1f2-...` |
| quantity | Units returned; always 1 on a serial line | 1 |
| unit_cost | Cost per unit taken from the purchase order line | 8290000 |
| purchase_return_id | FK to `purchase_returns` | `f6a7b8c9-...` |
| po_item_id | FK to `purchase_order_items` — line of the return's purchase order the goods came from | `b4c5d6e7-...` |
| serial_unit_id | FK to `serial_units` — the unit sent back | `d0e1f2a3-...`, NULL |

#### vouchers

Order-level discount codes.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d2e3f4a5-...` |
| code | Shared code customers type; NULL when the voucher is a batch of unique codes | TET2026, NULL |
| code_type | SHARED = one public code; UNIQUE_CODES = a batch of single-use codes in `voucher_codes` | SHARED, UNIQUE_CODES |
| type | Discount type | PERCENTAGE, FIXED_AMOUNT |
| value | Discount value | 10, 50000 |
| max_discount_amount | Cap on discount when `type=PERCENTAGE` (e.g. "10% off, max 500k") | 500000, NULL |
| min_order_value | Minimum order value to apply | 500000, NULL |
| applies_to_sale_items | Whether items already on sale (compare_at_price) count toward the discount | true |
| once_per_customer | true = each customer may use the voucher only once; false = only the total usage_limit applies | true, false |
| usage_limit | Total usage cap | 100, NULL |
| used_count | Times used so far; reconciled nightly (R-REC-02) | 37 |
| starts_at | Valid from | 2026-06-01 00:00:00 |
| ends_at | Valid until | 2026-06-30 23:59:59 |
| is_active | Manual on/off switch, independent of the date window | true, false |

#### voucher_codes

Single-use codes generated for a voucher batch.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `b2c3d4e5-...` |
| code | Random code handed to one customer | ZG-7K3M9Q |
| is_active | Cancelled codes can no longer be redeemed | true |
| voucher_id | FK to `vouchers` | `c3d4e5f6-...` |

#### voucher_targets

Categories or products a voucher is limited to; no rows means the whole store.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d4e5f6a7-...` |
| voucher_id | FK to `vouchers` | `c3d4e5f6-...` |
| category_id | FK to `categories` — exactly one of category_id / product_id | `e5f6a7b8-...`, NULL |
| product_id | FK to `products` | NULL |

#### voucher_usages

Voucher uses per customer.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e3f4a5b6-...` |
| revoked_at | Set when the order is cancelled after Confirm; NULL = usage still counts | 2026-02-04 09:00:00, NULL |
| once_per_customer | Copy of `vouchers.once_per_customer` at use time; the one-use-per-customer unique index only covers rows where this is true | true |
| order_id | FK to `orders` — the order this usage is tied to | `f8a9b0c1-...` |
| voucher_id | FK to `vouchers` | `d2e3f4a5-...` |
| customer_id | FK to `users` — NULL when used by the walk-in account | `a1b2c3d4-...`, NULL |
| voucher_code_id | FK to `voucher_codes` — the single-use code redeemed | `b2c3d4e5-...`, NULL |

### Future

#### product_badges

Marketing labels shown on products.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `d0e1f2a3-...` |
| code | Unique code used in code | best_seller, hot_deal |
| name | Display name | Bán chạy, Giá sốc |
| bg_color | Background color | #E53E3E, NULL |
| text_color | Text color | #FFFFFF, NULL |
| priority | Display order when a product has multiple badges | 0, 10 |
| is_active | Soft-delete flag: false = retired, still shown in history | true, false |

#### product_badge_assignments

Assigns badges to products, optionally for a period.

| Column | Description | Sample Data |
|---|---|---|
| product_id | FK to `products` | `b8c9d0e1-...` |
| badge_id | FK to `product_badges` | `d0e1f2a3-...` |
| starts_at | Badge becomes visible from this time; NULL = always | 2026-02-20 00:00:00, NULL |
| ends_at | Badge stops showing after this time; NULL = no end date | 2026-02-27 23:59:59, NULL |

#### product_tags

Promotional message banners for a category or a product.

| Column | Description | Sample Data |
|---|---|---|
| id | Primary key | `e5f6a7b8-...` |
| title | Short heading | "Mua kèm giá ưu đãi" |
| content | Full message text | "[Laptop] Giảm 1% tối đa 500k cho HSSV" |
| type | Business type — frontend maps to color (PROMOTION → red border, POLICY → blue border); DB stores meaning, not presentation | PROMOTION, POLICY |
| is_active | Currently shown | true, false |
| category_id | FK to `categories`; applies to all products in it and in its child categories | `e5f6a7b8-...`, NULL |
| product_id | FK to `products`; applies to 1 specific product | `b8c9d0e1-...`, NULL |

#### wishlists

Products saved by a customer.

| Column | Description | Sample Data |
|---|---|---|
| user_id | FK to `users` | `a1b2c3d4-...` |
| product_id | FK to `products` | `b8c9d0e1-...` |

## SQL Scripts

The schema is applied by EF Core migrations (one migration history, `__EFMigrationsHistory`). These files are the design specification: CI applies the migrations to an empty database and compares the result with them, then runs files 07 and 06.

| File | Contents | Run |
|---|---|---|
| `zengear_01_database.sql` | Creates database `zengear` (ICU collation `vi-VN`, time zone UTC) | 1st, connected to `postgres` |
| `zengear_02_schema_phase1_store.sql` | Phase 1 tables, indexes and constraints; extension `btree_gist`; triggers on `user_roles`; view `product_with_effective_price`; interim `ck_orders_discount_manual_only` (orders have no voucher before Phase 3) | 2nd |
| `zengear_03_schema_phase2_online.sql` | Phase 2 tables; `ALTER TABLE` adds `customer_profiles.cod_allowed`, `order_payments.expires_at`; extensions `unaccent`, `pg_trgm`, function `f_unaccent`, index `ix_products_name_trgm` | 3rd, when Phase 2 starts |
| `zengear_04_schema_phase3_operations.sql` | Phase 3 tables; `ALTER TABLE` adds `orders.voucher_id` (replaces the interim check with `ck_orders_discount_without_voucher`) and `supplier_payments.purchase_return_id` | 4th, when Phase 3 starts |
| `zengear_05_schema_future.sql` | Future tables; `ALTER TABLE` adds `categories.mega_menu_config` | when scheduled |
| `zengear_00_schema_full.sql` | All phases in one file for a fresh full install; drops existing tables first; equals running the four phase files in order | instead of the phase files |
| `zengear_06_reconciliation.sql` | Nightly reconciliation queries, grouped by phase (R-REC-01 … R-REC-11 and the cash book, payment and settlement checks) | nightly job |
| `zengear_07_schema_check.sql` | Lists objects not shown in the ERD (partial and expression indexes, exclusion constraint, triggers, extensions) that are missing; objects of phases not yet deployed are listed too | after each migration |
| `zengear_08_roles.sql` | Group roles `zengear_app`, `zengear_jobs`, `zengear_readonly`, their grants and default privileges; login users with passwords are created by operations, not in this file | after the migrations, as an administrator |
