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
│  │  ├─ ZenGear.Api/          Presentation: controllers, authorization, exception handling, hosting of jobs
│  │  ├─ ZenGear.Services/     Business Logic: services, models and their mappings, validators, jobs
│  │  └─ ZenGear.Data/         Data Access: entities, ZenGearDbContext, EF configurations, migrations,
│  │                           and the clients of external systems
│  ├─ tests/
│  │  ├─ ZenGear.Services.Tests/        unit tests — NUnit, Shouldly, Moq
│  │  ├─ ZenGear.Api.IntegrationTests/  integration tests — NUnit, Testcontainers (postgres:18), Respawn
│  │  └─ ZenGear.ArchitectureTests/     layers, folders and names — NUnit, NetArchTest (no Docker)
│  ├─ db/                      database specification: zengear_00–08 SQL files
│  ├─ seed/                    reference data
│  ├─ openapi/                 ZenGear.Api.json, generated at build — the contract used by web/
│  └─ acceptance/              acceptance environment: compose file, Firebase Auth Emulator, database set-up
│                              (docs/ACCEPTANCE.md)
├─ web/                        Next.js app (docs/FRONTEND.md)
├─ docs/                       specifications shared by api/ and web/
└─ tools/                      blueprint export, schema snapshot and comparison, PR gates, pull request steps
```

Folders inside the three projects follow [Folder structure](#folder-structure); namespaces follow folders (`ZenGear.Api.Controllers`, `ZenGear.Services.Sales`, `ZenGear.Services.Sales.Models`, `ZenGear.Data.Entities.Sales`).

## Dependency rules

1. `ZenGear.Api` → `ZenGear.Services` → `ZenGear.Data`. `ZenGear.Data` references no other project.
2. Controllers only call services; they never use `ZenGearDbContext`.
3. Controllers receive and return the requests and DTOs of `ZenGear.Services`, never entities — cost fields and `internal_note` must not leak (R-SEC-09, R-SEC-10). A DTO serves one audience: staff and customers get different DTOs (`ProductStaffDto`, `ProductPublicDto`), never one shared type.
4. A service writes only the tables of its own area. A use case lives in the area that owns the aggregate it creates or changes, and calls the services of the other areas for their tables (e.g. the POS sale is in `Sales`: `OrderPlacementService` calls `StockService.Issue(...)` and never updates `stock_balances`; Inventory recalculates the average cost (R-COST-01) and calls `IProductSkuService.SetAverageCost(...)`). Reading other areas' tables is allowed. `ZenGear.ArchitectureTests` scans the IL of the services: creating an entity, calling one of its setters or void methods (an entity method that changes state returns nothing), a static method of it that returns it, `DbSet.Add/Attach/Update/Remove` and `ExecuteUpdate/ExecuteDelete` must target an entity of the caller's area.
5. Services use `ZenGearDbContext` directly; no repository and no unit of work: the context is both, and use cases need `SELECT … FOR UPDATE`, `ExecuteUpdate` and one transaction across areas. A query repeated in three or more services becomes a query class (`<Entity><Purpose>Query`) in the owning area. Repositories are reconsidered only for a second database or data access technology.
6. External systems are called only through the interfaces in `ZenGear.Data/Externals/<System>` (`IEmailSender`, `IEInvoiceClient`, `IPaymentGateway`, `IFileStorage`, `IAuthProvider`, the Redis connection); the packages of those systems are referenced by `ZenGear.Data` only. Business rules built on them stay in `ZenGear.Services` (e.g. the access cache, R-SEC-07). Tests use fakes.
7. Mapping (AutoMapper) exists only in `ZenGear.Services`, from entity to DTO. Each DTO declares its own map in a nested `private sealed class Mapping : Profile`; there is no mapping folder or profile file. There is no map from a DTO or request to an entity: entities change through their methods (see [Where business rules live](#where-business-rules-live)). `ZenGear.Api` has no mapping.

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
| `Audit` | `status_history`, `attachments`, `audit_logs` |
| `Reporting` | none: read queries only |

`Audit` tables are written through shared services called by the owning service in the same transaction: status changes (`status_history`, R-HIS-01), evidence files (`attachments`, R-ATT-01) and the EF Core audit interceptor (`audit_logs`, R-AUD-01).

## Folder structure

`ZenGear.Api` is organised by technical role only. `ZenGear.Services` is organised by business area (table above, same names), with folders by kind inside each area. `ZenGear.Data` keeps its technical folders at the top and the areas inside `Entities` and `Configurations`. No other folder is created without first changing this section; `ZenGear.ArchitectureTests` fails on any folder not listed.

```
ZenGear.Api/
├─ Controllers/          every controller, flat
├─ ExceptionHandling/    IExceptionHandler implementations and the Problem Details they write
├─ Middlewares/          request pipeline middleware
├─ Extensions/           *Setup classes called by Program.cs, extension methods (also for functions on HTTP types)
├─ Attributes/           authorization and endpoint attributes
├─ Authorization/        authorization handlers, requirements, result handler
├─ Options/              options classes bound from configuration
├─ Constants/            claim types and other constant sets
├─ Logging/              security log, masking of sensitive values
├─ Hosting/              environment and pipeline types that are not a *Setup (build-time OpenAPI detection,
│                        custom policies of the request pipeline, one-off commands such as bootstrap-admin)
└─ Program.cs
ZenGear.Services/
├─ <Area>/
│  ├─ Models/            requests, DTOs (each with its nested Mapping) and internal data records
│  ├─ Enums/             enums used only by the area's services
│  ├─ Validators/        validators of the area's requests
│  ├─ <Mechanism>/       five or more types that form one mechanism (e.g. Identity/AccessCache): flat inside,
│  │                     with its own interfaces, records, enums and jobs
│  └─ services and their interfaces side by side, jobs and other classes that do the area's work
├─ Common/               shared code used by two or more areas; not an area
│  ├─ Models/            requests and DTOs of every list endpoint (PageRequest, PagedDto<T>)
│  ├─ Validators/        their validators
│  ├─ Utilities/         pure static functions used by two or more areas
│  └─ IClock and Clock, BusinessRuleException, AdvisoryLockKeys
└─ DependencyInjection.cs
ZenGear.Data/
├─ Common/               building blocks shared by every area's entities and configurations: IAggregateRoot,
│                        EF Core value converters (the conventions themselves are in ZenGearDbContext)
├─ Entities/<Area>/      entities; Enums/ below holds the enums of their columns
├─ Configurations/<Area>/ IEntityTypeConfiguration classes
├─ Externals/<System>/   clients of external systems and their interfaces (Redis, Email, EInvoice, Payments,
│                        Storage, Auth)
├─ Migrations/           EF Core migrations
├─ DependencyInjection.cs
└─ ZenGearDbContext.cs
```

A folder is created with its first file (`Validators/`, `Enums/`, `Utilities/`).

Where a type goes follows from its name:

| Type | Folder |
|---|---|
| `*Controller` | `ZenGear.Api/Controllers` |
| `*ExceptionHandler`, `*Problem` | `ZenGear.Api/ExceptionHandling` |
| `*Middleware` | `ZenGear.Api/Middlewares` |
| `*Setup` (the set-up of one concern, called by `Program.cs`), `*Extensions` (extension methods, including functions on HTTP types) | `ZenGear.Api/Extensions` |
| `*Attribute` | `ZenGear.Api/Attributes` |
| `*AuthorizationHandler`, `*ResultHandler`, `*Requirement` | `ZenGear.Api/Authorization` |
| `*Options` | `ZenGear.Api/Options`; in `ZenGear.Data`, next to the client that reads it |
| `*Claims` and other constant sets | `ZenGear.Api/Constants` |
| security log, log masking (`SecurityLog`, `SensitivePropertyMasker`) | `ZenGear.Api/Logging` |
| environment and pipeline set-up that is not a `*Setup` (`BuildTimeDocumentGeneration`), a one-off command of the host (`bootstrap-admin`, DEC-096) | `ZenGear.Api/Hosting` |
| `*Request`, `*Dto`, and records that only carry data (a record used only inside one mechanism stays in it) | `ZenGear.Services/<Area>/Models`; shared by every list endpoint: `ZenGear.Services/Common/Models` |
| enum of the Services layer | `ZenGear.Services/<Area>/Enums` |
| `*Validator`: the FluentValidation validator of one request; other checks are named after what they check | `ZenGear.Services/<Area>/Validators` (`Common/Validators` for `Common/Models`) |
| `*Service`, `*Query`, `*Job` and other classes that do the area's work; `I*Service` only when another area calls the service, a test replaces it or it has two implementations | `ZenGear.Services/<Area>` |
| `*Utility`: static, pure (values in, values out; no framework, database or DI), used by two or more areas; a pure function of one area is a static class of that area named after what it computes (`AverageCostCalculator`) | `ZenGear.Services/Common/Utilities` |
| entity | `ZenGear.Data/Entities/<Area>` |
| enum of an entity column | `ZenGear.Data/Entities/<Area>/Enums` |
| `*Configuration` | `ZenGear.Data/Configurations/<Area>` |
| `*Converter`, `IAggregateRoot` | `ZenGear.Data/Common` |
| client of an external system and its interface | `ZenGear.Data/Externals/<System>` |
| `DependencyInjection`: the `Add…` method that registers the project's services (`AddZenGearServices`, `AddZenGearData`) | the root of `ZenGear.Services` and of `ZenGear.Data` |

A type of a mechanism folder (interface, record, enum, job) stays in that folder.

Names put the entity first, so a folder sorts by entity: `<Entity><Action>Request` (`StaffCreateRequest`), `<Entity><Shape>Dto` (`StaffDto`, `StaffListItemDto`), `<Request>Validator` (`StaffCreateRequestValidator`), `<Entity>Service` and `I<Entity>Service`, `<Entity><Purpose>Query`, `<Purpose>Job` (`OrderExpiryJob`). A line of a request or DTO is a top-level type (`StockAdjustmentLineRequest`, `OrderItemDto`).

Services are split by audience and by use case, as DTOs are (rule 3): `<Entity>Service` serves the staff screens, `<Entity>PublicService` the storefront and never reads cost fields. An entity with many use cases gets one service per family of use cases, `<Entity><UseCase>Service` (`OrderPlacementService`, `OrderCancellationService`), not one `OrderService` for all of them. A file of `ZenGear.Services` has at most 400 lines of code (blank and comment lines not counted) and a `*Service` at most 10 public methods; a larger one is split before it is merged. An exception is listed in `ServiceSizeTests` with its reason.

- One top-level type per file, the file named after the type. A nested type is private, like the `Mapping` class of a DTO; a public type is never nested.
- Type names are unique across the three projects, `DependencyInjection` excepted: the OpenAPI document names its schemas after the type.
- An interface sits next to its implementation, in its own file (`IStaffService.cs` beside `StaffService.cs`).
- Controllers take `*Request` and return `*Dto` only; the other records of `Models/` never leave the services.
- A utility does one thing, is a static class without state and has unit tests. A shared function that works on HTTP types is an extension method in `ZenGear.Api/Extensions`, not a utility.
- No `Helpers`, `Interfaces`, `Dtos`, `Mappings`, `Repositories`, `Jobs` or `BackgroundServices` folders; `Utilities` only in `ZenGear.Services/Common`; no type named `*Helper` or `*Manager`.
- No type is added without a caller in the same change.

Tests mirror the folders of the code they test: `ZenGear.Services.Tests/<Area>` (and `Data/` for `ZenGear.Data`), `ZenGear.Api.IntegrationTests/<Area>` for the endpoints of an area and `ZenGear.Api.IntegrationTests/<Api folder>` for the pipeline (`Authorization/`, `ExceptionHandling/`, `Logging/`), with shared fixtures in `Infrastructure/`. The assembly-wide NUnit `[SetUpFixture]` classes (`PostgresContainer`, `RedisContainer`) stay at the root of their project: NUnit applies a set-up fixture only to tests in its namespace and below. `ZenGear.Services.Tests` asserts that the AutoMapper configuration of every DTO is valid.

This structure is DEC-097. It is reopened only when `python tools/structure_metrics.py` reports an area root or a mechanism folder above 20 files, a `Models/` folder above 40 files, or more folders touched per `feat` commit in three areas in a row; when `ServiceSizeTests` lists more than three exceptions; or when a second host uses `ZenGear.Services` or the API needs a second version (requests and responses then move to `ZenGear.Api`).

## Where business rules live

Column "Enforced At" of `docs/blueprint/business-rules.md`:

| Enforced At | Code location |
|---|---|
| `Service — <Entity> aggregate` | A rule on one entity: a method on the entity in `ZenGear.Data/Entities/<Area>` that is the only way to change that state (e.g. `Order.Confirm()`); services call it |
| `Service — <Handler/Service>` | A rule spanning several entities or needing queries: the service of the owning area, in `ZenGear.Services/<Area>` |
| `Api — <component>` | Middleware, authorization handlers, serializers, query endpoints (`ZenGear.Api`) |
| `Database — <object>` | CHECK, unique, exclusion, FK, trigger in the schema; the service still validates first for a clear error |
| `Scheduled job — <job>` | A `*Job` in the owning area of `ZenGear.Services`, hosted by `ZenGear.Api` |

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
- CI: `dotnet ef migrations has-pending-model-changes`; build one database from the migrations and one from `api/db/zengear_01_database.sql` plus the phase files up to the phase in progress, run `tools/schema_snapshot.sql` on both and compare them with `tools/schema_compare.py`: every table, view and function the migrations build matches the specification line for line, and the migrations build nothing the specification does not have (the tables of the phase not built yet are left out); after the last phase `api/db/zengear_07_schema_check.sql` returns 0 rows; `api/db/zengear_06_reconciliation.sql` runs on test data.
- The build writes `api/openapi/ZenGear.Api.json` (Microsoft.AspNetCore.OpenApi); it is committed and CI fails when it is out of date.
- Deploy, in this order: migration bundle → `api/db/zengear_08_roles.sql` → `api/seed/` (re-runnable) → start the new API version (DEC-095). The startup permission sync needs the roles of `01_roles.sql`; its guard (nothing is written while a role is missing) is only a safety net for a deploy run out of this order.
- Behind Azure App Service, `UseForwardedHeaders` runs before authentication and trusts only Azure's front end, so the security log (R-SEC-11) records the client IP (GitHub issue #3).
- The API connects as a login of `zengear_app`, jobs as `zengear_jobs`, reports as `zengear_readonly` — never as the schema owner (DEC-089).

## Background jobs

A job is a `*Job` class in the area that owns what it changes (`Sales/OrderExpiryJob`, `Notifications/NotificationSenderJob`, `Invoicing/EInvoiceIssuingJob`, `Reporting/NightlyReconciliationJob`), or in the mechanism folder it serves (`Identity/AccessCache/AccessBumpRetryJob`). `ZenGear.Api` registers every job. A job only orchestrates: it takes the lock, loops and calls the service of its area; business rules stay in that service, and rule 4 applies to jobs as to services.

Periodic jobs are a `BackgroundService` with a `PeriodicTimer`: pending order expiry, notification sender, e-invoice issuing, nightly reconciliation (02:00, report only), nightly cleanup. A job takes a PostgreSQL advisory lock (`pg_try_advisory_lock`) so only one API instance runs it at a time; every lock key is declared once in `ZenGear.Services/Common/AdvisoryLockKeys`, so no two jobs share a key.

Two exceptions:

- `PermissionSyncJob` (`Identity/Permissions`) is a one-shot `IHostedService` at startup. It applies the permission catalog of the code (DEC-095) under a blocking `pg_advisory_xact_lock`, so instances starting together run it one after the other. A failure is logged and the API starts with the grants already stored.
- `AccessBumpRetryJob` (`Identity/AccessCache`) retries the access cache bumps that failed (see Caching).

## Caching

Redis caches the user status and the permissions per role (R-SEC-07), behind `IUserAccessReader`; nothing else is cached yet. The connection is in `ZenGear.Data/Externals/Redis`; the cache and its rules are in `ZenGear.Services/Identity/AccessCache`.

- **Versioned keys:** entries are stored under a version: `zengear:access:user:{id}:{epoch}:{ver}` and `…role:{id}:{epoch}:{ver}`, 5-minute TTL. The versions are `…ver:user:{id}`, `…ver:role:{id}` and the global `…epoch`, with no TTL. Every entry is written under the version read before its query, so data read before a change commits never becomes current.
- **Invalidation:** the version is incremented (INCR) after the commit that changes `users.status`, `users.firebase_uid`, `staff_profiles`, `user_roles` or `role_permissions`. The EF Core interceptor `AccessCacheInvalidationInterceptor` does it for tracked changes. `ExecuteUpdate`, raw SQL and FK cascades bypass it, so their caller calls `IAccessCache.BumpAsync`.
- **Failed bumps:** a bump that fails stays pending. It is retried every second, and the instance reads PostgreSQL only until it succeeds. A restored connection bumps the epoch. A bump pending on an instance that stops, or a partition affecting one instance, is only limited by the 5-minute TTL safety net.
- **Redis unavailable:** requests read PostgreSQL. Commands time out after `Redis:CommandTimeoutMs` (250 ms).
- **Eviction policy:** Redis must run with `maxmemory-policy` `volatile-*` or `noeviction`, never `allkeys-*`. An evicted version key would bring an old entry back.

## Time and money

Timestamps are stored in UTC; every day-based calculation uses `Asia/Ho_Chi_Minh` through one clock service (R-TZ-01). Money is `decimal` in C# and `numeric(15,0)` VND in the database.
