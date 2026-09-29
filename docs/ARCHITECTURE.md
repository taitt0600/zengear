# Architecture

Backend building blocks of ZenGear (arc42 section 5). Decision: DEC-091 in `docs/blueprint/decisions.md`.

## System

| Part | Technology | Hosting |
|---|---|---|
| Web app — storefront, admin, POS | Next.js, TypeScript | Vercel |
| Backend API and background jobs | .NET 10, ASP.NET Core, EF Core 10 + Npgsql | Azure |
| Database | PostgreSQL 18 | Azure |
| External services | Firebase Authentication, Firebase Storage, VNPay (Phase 2), Resend, MISA meInvoice | — |

## Repository structure

One repository for backend, web and specifications (DEC-093). The backend is a 3-Layer (N-Layer) solution: Presentation, Business Logic, Data Access.

```
zengear/
├─ api/
│  ├─ ZenGear.slnx             solution (.slnx is the .NET 10 default; .sln works the same)
│  ├─ src/
│  │  ├─ ZenGear.Api/          Presentation: controllers, middleware, auth, ProblemDetails, hosting of jobs
│  │  ├─ ZenGear.Services/     Business Logic: services, DTOs, validators, integrations, jobs
│  │  └─ ZenGear.Data/         Data Access: entities, ZenGearDbContext, EF configurations, migrations
│  ├─ tests/
│  │  ├─ ZenGear.Services.Tests/        unit tests — NUnit, Shouldly, Moq
│  │  └─ ZenGear.Api.IntegrationTests/  integration tests — NUnit, Testcontainers (postgres:18), Respawn
│  ├─ db/                      database specification: zengear_00–08 SQL files
│  ├─ seed/                    reference data
│  └─ openapi/                 ZenGear.Api.json, generated at build — the contract used by web/
├─ web/                        Next.js app (docs/FRONTEND.md)
├─ docs/                       specifications shared by api/ and web/
└─ tools/                      blueprint export, schema snapshot
```

Inside every project the folders are the business areas below, with the same names; namespaces follow folders (`ZenGear.Services.Sales`, `ZenGear.Data.Entities.Sales`).

## Dependency rules

1. `ZenGear.Api` → `ZenGear.Services` → `ZenGear.Data`. `ZenGear.Data` references no other project.
2. Controllers only call services; they never use `ZenGearDbContext`.
3. The API only returns DTOs, never entities — cost fields and `internal_note` must not leak (R-SEC-09, R-SEC-10).
4. A service writes only the tables of its own area. To change data of another area it calls that area's service (e.g. `PosCheckoutService` calls `StockService.Issue(...)`; it never updates `stock_balances`). Reading other areas' tables for display is allowed.
5. Services use `ZenGearDbContext` directly; no generic repository.
6. External systems are called only through interfaces in `ZenGear.Services/Integrations` (`IEmailSender`, `IEInvoiceClient`, `IPaymentGateway`, `IFileStorage`, `IAuthProvider`); tests use fakes.

## Business areas and table ownership

| Area | Tables written by this area |
|---|---|
| `Identity` | `users`, `customer_profiles`, `staff_profiles`, `roles`, `permissions`, `user_roles`, `role_permissions` |
| `Customers` | `addresses`, `wishlists` |
| `Geography` | `countries`, `provinces`, `wards` |
| `Catalog` | `categories`, `brands`, `product_series`, `products`, `product_skus`, `product_images`, `pc_build_components`, `product_badges`, `product_badge_assignments`, `product_tags`, `url_redirects` |
| `Tax` | `tax_classes`, `tax_rates` |
| `Inventory` | `warehouses`, `business_locations`, `stock_balances`, `stock_ledger_entries`, `serial_units`, `pc_assemblies`, `pc_assembly_components`, `stock_adjustments`, `adjustment_reasons`, `stock_counts`, `stock_count_lines` |
| `Purchasing` | `suppliers`, `purchase_orders`, `purchase_order_items`, `purchase_receipts`, `purchase_returns`, `purchase_return_items`, `supplier_payments` |
| `Sales` | `orders`, `order_items`, `order_item_serials`, `order_item_components`, `order_invoice_details`, `cart_items`, `cart_serial_units`, `daily_sequences` |
| `Shipping` | `carriers`, `shipping_methods`, `shipping_fee_rules`, `order_shipping_details`, `carrier_settlements`, `carrier_settlement_lines` |
| `Payments` | `order_payments` |
| `Invoicing` | `e_invoices` |
| `Pos` | `shifts`, `shift_cash_adjustments` |
| `Accounting` | `cash_accounts`, `cash_voucher_categories`, `cash_vouchers` |
| `AfterSales` | `returns`, `return_items`, `warranty_requests` |
| `Promotions` | `vouchers`, `voucher_codes`, `voucher_targets`, `voucher_usages` |
| `Notifications` | `notifications` |
| `Imports` | `import_jobs` |
| `Common` | `status_history`, `attachments`, `audit_logs` |

`Common` tables are written through shared helpers called by the owning service in the same transaction: status changes (`status_history`, R-HIS-01), evidence files (`attachments`, R-ATT-01) and the EF Core audit interceptor (`audit_logs`, R-AUD-01). `Reporting` owns no tables; it only runs read queries.

## Where business rules live

Column "Enforced At" of `docs/blueprint/business-rules.md`:

| Enforced At | Code location |
|---|---|
| `Service — <Entity> aggregate` | A rule on one entity: a method on the entity in `ZenGear.Data/Entities/<Area>` that is the only way to change that state (e.g. `Order.Confirm()`); services call it |
| `Service — <Handler/Service>` | A rule spanning several entities or needing queries: the service of the owning area |
| `Api — <component>` | Middleware, authorization policies, serializers, query endpoints |
| `Database — <object>` | CHECK, unique, exclusion, FK, trigger in the schema; the service still validates first for a clear error |
| `Scheduled job — <job>` | A `BackgroundService` in `ZenGear.Services/Jobs`, hosted by `ZenGear.Api` |

Every rule has at least one test whose name starts with its ID (`R_INV_01_...`).

## Transactions and concurrency

- One `ZenGearDbContext` for the whole application. One use case = one service method = one transaction; nested service calls share it.
- Optimistic concurrency with `xmin` on every aggregate root; `SELECT … FOR UPDATE` before checking a sum across rows (R-TX-14).
- Stock changes lock the `stock_balances` row, write the ledger row with `balance_after` and `serial_unit_id` (R-INV-01).
- Outbox: `notifications` and `e_invoices` rows are created inside the business transaction; jobs deliver them afterwards (R-NOT-01, R-EIV-01).

## Data access

- EF Core Migrations are the only way the schema changes (DEC-088). Each phase is a group of migrations (`Phase1_Store`, `Phase2_Online`, …).
- `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions); constraint and index names set explicitly to the names in `api/db/` (`pk_`, `fk_`, `ix_`, `ck_`, ≤ 63 characters).
- PostgreSQL-only objects (exclusion constraint, triggers, view, functions) through `migrationBuilder.Sql`.
- CI: `dotnet ef migrations has-pending-model-changes`; build one database from the migrations and one from `api/db/zengear_01_database.sql` plus the phase files up to the phase in progress, run `tools/schema_snapshot.sql` on both and `diff` the output (must be empty); after the last phase `api/db/zengear_07_schema_check.sql` returns 0 rows; `api/db/zengear_06_reconciliation.sql` runs on test data.
- The build writes `api/openapi/ZenGear.Api.json` (Microsoft.AspNetCore.OpenApi); it is committed and CI fails when it is out of date.
- Deploy: migration bundle as a separate step before the new API version starts; then `api/db/zengear_08_roles.sql` and `api/seed/`.
- Behind Azure App Service, `UseForwardedHeaders` runs before authentication and trusts only Azure's front end, so the security log (R-SEC-11) records the client IP (GitHub issue #2).
- The API connects as a login of `zengear_app`, jobs as `zengear_jobs`, reports as `zengear_readonly` — never as the schema owner (DEC-089).

## Background jobs

`BackgroundService` + `PeriodicTimer` in `ZenGear.Services/Jobs`, registered by `ZenGear.Api`: pending order expiry, notification sender, e-invoice issuing, nightly reconciliation (02:00, report only), nightly cleanup. A job takes a PostgreSQL advisory lock (`pg_try_advisory_lock`) so only one API instance runs it at a time.

## Time and money

Timestamps are stored in UTC; every day-based calculation uses `Asia/Ho_Chi_Minh` through one clock service (R-TZ-01). Money is `decimal` in C# and `numeric(15,0)` VND in the database.
