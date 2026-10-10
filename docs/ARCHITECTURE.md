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

Folders inside the three projects follow [Folder structure](#folder-structure); namespaces follow folders (`ZenGear.Api.Controllers.Identity`, `ZenGear.Services.Identity.StaffAccounts`, `ZenGear.Data.Entities.Identity`). How to write the code inside them is in `docs/CODING_GUIDE.md`.

## Dependency rules

1. `ZenGear.Api` → `ZenGear.Services` → `ZenGear.Data`. `ZenGear.Data` references no other project.
2. Controllers only call services; they never use `ZenGearDbContext`.
3. Controllers receive and return the requests and DTOs of `ZenGear.Services`, never entities — cost fields and `internal_note` must not leak (R-SEC-09, R-SEC-10). A DTO serves one audience: staff and customers get different DTOs (`ProductDto` for staff, `ProductShopDto` for the shop), never one shared type.
4. A service writes only the tables of its own area. A use case lives in the area that owns the aggregate it creates or changes, and calls the services of the other areas for their tables (e.g. the POS sale is in `Sales`: `OrderPlacementService` calls `StockService.Issue(...)` and never updates `stock_balances`; Inventory recalculates the average cost (R-COST-01) and calls `IProductSkuService.SetAverageCost(...)`). Reading other areas' tables is allowed. Another area calls an area only through a service listed under *Public contract* in that area's `README.md` (DEC-106); `ZenGear.ArchitectureTests` checks the constructor dependencies of the services across areas. It also scans the IL of the services: creating an entity, calling one of its setters or void methods (an entity method that changes state returns nothing), a static method of it that returns it, `DbSet.Add/Attach/Update/Remove` and `ExecuteUpdate/ExecuteDelete` must target an entity of the caller's area.
5. Services use `ZenGearDbContext` directly; no repository and no unit of work: the context is both, and use cases need `SELECT … FOR UPDATE`, `ExecuteUpdate` and one transaction across areas. A query repeated in three or more services becomes a query class (`<Entity><Purpose>Query`) in the owning area. Repositories are reconsidered only for a second database or data access technology.
6. External systems are called only through the interfaces in `ZenGear.Data/Externals/<System>` (`IEmailSender`, `IEInvoiceClient`, `IPaymentGateway`, `IFileStorage`, `IAuthProvider`, the Redis connection); the packages of those systems are referenced by `ZenGear.Data` only. Business rules built on them stay in `ZenGear.Services` (e.g. the access cache, R-SEC-07). Tests use fakes.
7. Mapping (AutoMapper) exists only in `ZenGear.Services`, from entity to DTO. Each DTO declares its own map in a nested `private sealed class Mapping : Profile`; there is no mapping folder or profile file. There is no map from a DTO or request to an entity: entities change through their methods (see [Where business rules live](#where-business-rules-live)). `ZenGear.Api` has no mapping.

## Business areas and table ownership

| Area | Tables written by this area |
|---|---|
| `Identity` | `users`, `customer_profiles`, `staff_profiles`, `roles`, `permissions`, `user_roles`, `role_permissions` |
| `Customers` | `addresses`, `wishlists` |
| `Geography` | `countries`, `provinces`, `wards` |
| `Locations` | `business_locations` |
| `Catalog` | `categories`, `brands`, `product_series`, `products`, `product_skus`, `product_images`, `pc_build_components`, `product_badges`, `product_badge_assignments`, `product_tags`, `url_redirects` |
| `Tax` | `tax_classes`, `tax_rates` |
| `Inventory` | `warehouses`, `stock_balances`, `stock_ledger_entries`, `serial_units`, `pc_assemblies`, `pc_assembly_components`, `stock_adjustments`, `adjustment_reasons`, `stock_counts`, `stock_count_lines` |
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

`Locations` is upstream (DEC-106): the website, receipts, emails and other areas read the business location, so its services read `Geography` and depend on no other area; `ZenGear.ArchitectureTests` checks it.

Each area of `ZenGear.Services` has a `README.md`: its purpose, its modules, its public contract (the services other areas may call, rule 4) and its dependencies. The tables an area owns are listed only in the table above.

## Folder structure

`ZenGear.Services` is organised by business area (table above, same names) and, inside an area, by module. `ZenGear.Api` keeps its technical folders, with the controllers by area. `ZenGear.Data` keeps its technical folders at the top and the areas inside `Entities` and `Configurations`: the modules of an area share its entities. Decision: DEC-106, which replaced the folders by kind of DEC-097.

```
ZenGear.Api/
├─ Controllers/<Area>/   one controller per service, named after it, in the area of that service
├─ ExceptionHandling/    IExceptionHandler implementations and the Problem Details they write
├─ Middlewares/          request pipeline middleware
├─ Extensions/           *Setup classes called by Program.cs, extension methods (also for functions on HTTP types)
├─ Attributes/           authorization and endpoint attributes
├─ Authorization/        authorization handlers, requirements, result handler
├─ Options/              options classes bound from configuration
├─ Constants/            claim types and other constant sets
├─ Logging/              security log, masking of sensitive values
├─ Hosting/              environment and pipeline types that are not a *Setup (build-time OpenAPI detection,
│                        custom policies of the request pipeline such as the route prefix convention, one-off
│                        commands such as bootstrap-admin)
└─ Program.cs
ZenGear.Services/
├─ <Area>/
│  ├─ README.md          purpose, modules, public contract and dependencies of the area
│  └─ <Module>/          one aggregate or one group of screens around it, flat: services, *Query, *Job, requests,
│     │                  DTOs (each with its nested Mapping), validators, enums and records of the module
│     └─ <Mechanism>/    five or more types that form one mechanism (Identity/Access/AccessCache): flat, with
│                        its own interfaces, records, enums and jobs
├─ Common/               shared code used by two or more areas; not an area
│  ├─ Models/            requests and DTOs of every list endpoint (PageRequest, PagedDto<T>)
│  ├─ Validators/        their validators
│  ├─ Utilities/         pure static functions used by two or more areas
│  └─ IClock and Clock, BusinessRuleException, AdvisoryLockKeys
└─ DependencyInjection.cs
ZenGear.Data/
├─ Common/               building blocks shared by every area's entities, configurations and migrations:
│                        IAggregateRoot, EF Core value converters (the conventions themselves are in
│                        ZenGearDbContext), MigrationBuilderExtensions
├─ Entities/<Area>/      entities; Enums/ below holds the enums of their columns
├─ Configurations/<Area>/ IEntityTypeConfiguration classes
├─ Externals/<System>/   clients of external systems and their interfaces (Redis, Email, EInvoice, Payments,
│                        Storage, Auth)
├─ Migrations/           EF Core migrations
├─ DependencyInjection.cs
└─ ZenGearDbContext.cs
```

- A module folder exists even when the area has one module; the root of an area holds only its `README.md` and its modules. The name of a type says its kind (`StaffCreateRequest`, `StaffDto`, `StaffCreateRequestValidator`, `StaffService`), so there is no `Models`, `Validators` or `Enums` folder in an area.
- New folders: the feature that needs them creates, without asking, a module of an area of the table above (with its line under *Modules* in the area's `README.md`, and the README with the area's first module), and for such an area its folders `Controllers/<Area>`, `Entities/<Area>` (with `Enums/`), `Configurations/<Area>` and the test folders `<Area>/<Module>`. A new area, a mechanism folder, a technical folder of `ZenGear.Api` or `ZenGear.Data`, or a folder in `Common` is asked first. `ZenGear.ArchitectureTests` fails on any folder this section does not allow.
- A folder is created with its first file.

Where a name still decides the folder:

| Type | Folder |
|---|---|
| `*Controller` | `ZenGear.Api/Controllers/<Area>`, the area of the service it calls |
| `*ExceptionHandler`, `*Problem` | `ZenGear.Api/ExceptionHandling` |
| `*Middleware` | `ZenGear.Api/Middlewares` |
| `*Setup` (the set-up of one concern, called by `Program.cs`), `*Extensions` (extension methods, including functions on HTTP types) | `ZenGear.Api/Extensions` |
| `*Attribute` | `ZenGear.Api/Attributes` |
| `*AuthorizationHandler`, `*ResultHandler`, `*Requirement` | `ZenGear.Api/Authorization` |
| `*Options` | `ZenGear.Api/Options`; in `ZenGear.Data`, next to the client that reads it |
| `*Claims` and other constant sets | `ZenGear.Api/Constants` |
| security log, log masking (`SecurityLog`, `SensitivePropertyMasker`) | `ZenGear.Api/Logging` |
| environment and pipeline set-up that is not a `*Setup` (`BuildTimeDocumentGeneration`, `AudienceRoutePrefixConvention`), a one-off command of the host (`bootstrap-admin`, DEC-096) | `ZenGear.Api/Hosting` |
| requests and DTOs shared by every list endpoint (`PageRequest`, `PagedDto<T>`: list endpoints return `PagedDto<T>`, never a page DTO per entity; a generic DTO lives only there), and their validators | `ZenGear.Services/Common/Models`, `ZenGear.Services/Common/Validators` |
| every other type of an area: `*Service`, `*Query`, `*Job`, `*Request`, `*Dto`, `*Validator` (the FluentValidation validator of one request; other checks are named after what they check), enums and records | `ZenGear.Services/<Area>/<Module>`, or the mechanism folder it belongs to |
| `*Utility`: static, pure (values in, values out; no framework, database or DI), used by two or more areas; a pure function of one area is a static class of that area named after what it computes (`AverageCostCalculator`) | `ZenGear.Services/Common/Utilities` |
| entity | `ZenGear.Data/Entities/<Area>` |
| enum of an entity column | `ZenGear.Data/Entities/<Area>/Enums` |
| `*Configuration` | `ZenGear.Data/Configurations/<Area>` |
| `*Converter`, `IAggregateRoot`, `MigrationBuilderExtensions` and `CompositeForeignKeyOnDelete` (`ZenGear.Data` cannot reach `ZenGear.Api/Extensions`) | `ZenGear.Data/Common` |
| client of an external system and its interface | `ZenGear.Data/Externals/<System>` |
| `DependencyInjection`: the `Add…` method that registers the project's services (`AddZenGearServices`, `AddZenGearData`) | the root of `ZenGear.Services` and of `ZenGear.Data` |

A type of a mechanism folder (interface, record, enum, job) stays in that folder.

Names put the entity first, so a folder sorts by entity: `<Entity><Action>Request` (`StaffCreateRequest`), `<Entity><Shape>Dto` (`StaffDto`, `StaffListItemDto`), `<Request>Validator` (`StaffCreateRequestValidator`), `<Entity>Service` and `I<Entity>Service`, `<Entity><Purpose>Query`, `<Purpose>Job` (`OrderExpiryJob`). A line of a request or DTO is a top-level type (`StockAdjustmentLineRequest`, `OrderItemDto`).

Services are split by audience and by use case, as DTOs are (rule 3): `<Entity>Service` serves the staff screens, `<Entity>ShopService` the shop (`/api/v1/shop/`) and never reads cost fields. An entity with many use cases gets one service per family of use cases, `<Entity><UseCase>Service` (`OrderPlacementService`, `OrderCancellationService`), not one `OrderService` for all of them. A file of `ZenGear.Services` has at most 400 lines of code (blank and comment lines not counted) and a `*Service` at most 10 public methods; a larger one is split before it is merged. An exception is listed in `ServiceSizeTests` with its reason.

- One top-level type per file, the file named after the type. A nested type is private, like the `Mapping` class of a DTO; a public type is never nested.
- Type names are unique across the three projects, `DependencyInjection` excepted: the OpenAPI document names its schemas after the type.
- An interface exists only for a reason of `docs/CODING_GUIDE.md`, section 2 (a test seam, an external system, the public contract of an area, two implementations), and sits next to its implementation, in its own file.
- Controllers take `*Request` and return `*Dto` only; the other records of a module never leave the services.
- A utility does one thing, is a static class without state and has unit tests. A shared function that works on HTTP types is an extension method in `ZenGear.Api/Extensions`, not a utility.
- No `Helpers`, `Interfaces`, `Dtos`, `Mappings`, `Repositories`, `Jobs`, `BackgroundServices`, `Models`, `Validators` or `Enums` folder in an area of `ZenGear.Services`; `Utilities` only in `ZenGear.Services/Common`; no type named `*Helper` or `*Manager`.
- No type is added without a caller in the same change.

Tests mirror the folders of the code they test: `ZenGear.Services.Tests/<Area>/<Module>` (and `Data/` for `ZenGear.Data`), `ZenGear.Api.IntegrationTests/<Area>/<Module>` for the endpoints of a module and `ZenGear.Api.IntegrationTests/<Api folder>` for the pipeline (`Authorization/`, `ExceptionHandling/`, `Logging/`), with shared fixtures in `Infrastructure/`. A test that covers two modules sits with the module of the rule it proves. The assembly-wide NUnit `[SetUpFixture]` classes (`PostgresContainer`, `RedisContainer`) stay at the root of their project: NUnit applies a set-up fixture only to tests in its namespace and below. `ZenGear.Services.Tests` asserts that the AutoMapper configuration of every DTO is valid.

### Evolution thresholds

The backend stays three projects (DEC-091) while an area fits in them. An area becomes a project of its own (a modular monolith, one area at a time) when it passes 8 modules or 60 files of `ZenGear.Services`, when the build and tests of the solution pass 5 minutes, or when a second developer works on one area alone (DEC-106). `python tools/structure_metrics.py` reports the modules and files of each area and the folders touched per `feat` commit at module level (target: at most 5); it is read every month. More than three exceptions in `ServiceSizeTests`, a second host using `ZenGear.Services`, or a second version of the API (requests and responses then move to `ZenGear.Api`) also call for a Decision.

## Where business rules live

Column "Enforced At" of `docs/blueprint/business-rules.md`:

| Enforced At | Code location |
|---|---|
| `Service — <Entity> aggregate` | A rule on one entity: a method on the entity in `ZenGear.Data/Entities/<Area>` that is the only way to change that state (e.g. `Order.Confirm()`); services call it |
| `Service — <Handler/Service>` | A rule spanning several entities or needing queries: the service of the owning area, in `ZenGear.Services/<Area>/<Module>` |
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
- A composite foreign key whose target is a unique index rather than a key (R-DB-09: `(ward_id, province_id)` → `wards (id, province_id)`) is not in the EF model: EF Core needs a principal key for a relationship and would add an alternate key, a constraint the specification does not have. The migration creates it with `AddCompositeForeignKey` (`ZenGear.Data/Common/MigrationBuilderExtensions`, `ON DELETE` from `CompositeForeignKeyOnDelete`) and `Down` drops it with `DropCompositeForeignKey`, from the next migration with a composite foreign key on (the LOC-01 migration, applied in every database, keeps its hand-written SQL); the CI job `schema` checks it against `api/db/`. These two types came before their first migration, an exception to "no type is added without a caller in the same change".
- CI: `dotnet ef migrations has-pending-model-changes`; build one database from the migrations and one from `api/db/zengear_01_database.sql` plus the phase files up to the phase in progress, run `tools/schema_snapshot.sql` on both and compare them with `tools/schema_compare.py`: every table, view and function the migrations build matches the specification line for line, and the migrations build nothing the specification does not have (the tables of the phase not built yet are left out); after the last phase `api/db/zengear_07_schema_check.sql` returns 0 rows; `api/db/zengear_06_reconciliation.sql` runs on test data.
- The build writes `api/openapi/ZenGear.Api.json` (Microsoft.AspNetCore.OpenApi); it is committed and CI fails when it is out of date.
- Deploy, in this order: migration bundle → `api/db/zengear_08_roles.sql` → `api/seed/` (re-runnable) → start the new API version (DEC-095). The startup permission sync needs the roles of `01_roles.sql`; its guard (nothing is written while a role is missing) is only a safety net for a deploy run out of this order.
- Behind Azure App Service, `UseForwardedHeaders` runs before authentication and trusts only Azure's front end, so the security log (R-SEC-11) records the client IP (GitHub issue #3).
- The API connects as a login of `zengear_app`, jobs as `zengear_jobs`, reports as `zengear_readonly` — never as the schema owner (DEC-089).

## Background jobs

A job is a `*Job` class in the module that owns what it changes (`OrderExpiryJob` in a module of `Sales`, `NotificationSenderJob` of `Notifications`, `EInvoiceIssuingJob` of `Invoicing`, `NightlyReconciliationJob` of `Reporting`), or in the mechanism folder it serves (`Identity/Access/AccessCache/AccessBumpRetryJob`). `ZenGear.Api` registers every job. A job only orchestrates: it takes the lock, loops and calls the service of its area; business rules stay in that service, and rule 4 applies to jobs as to services.

Periodic jobs are a `BackgroundService` with a `PeriodicTimer`: pending order expiry, notification sender, e-invoice issuing, nightly reconciliation (02:00, report only), nightly cleanup. A job takes a PostgreSQL advisory lock (`pg_try_advisory_lock`) so only one API instance runs it at a time; every lock key is declared once in `ZenGear.Services/Common/AdvisoryLockKeys`, so no two jobs share a key.

Two exceptions:

- `PermissionSyncJob` (`Identity/Permissions`) is a one-shot `IHostedService` at startup. It applies the permission catalog of the code (DEC-095) under a blocking `pg_advisory_xact_lock`, so instances starting together run it one after the other. A failure is logged and the API starts with the grants already stored.
- `AccessBumpRetryJob` (`Identity/Access/AccessCache`) retries the access cache bumps that failed (see Caching).

## Caching

Redis caches the user status and the permissions per role (R-SEC-07), behind `IUserAccessReader`; nothing else is cached yet. The connection is in `ZenGear.Data/Externals/Redis`; the cache and its rules are in `ZenGear.Services/Identity/Access/AccessCache`.

- **Versioned keys:** entries are stored under a version: `zengear:access:user:{id}:{epoch}:{ver}` and `…role:{id}:{epoch}:{ver}`, 5-minute TTL. The versions are `…ver:user:{id}`, `…ver:role:{id}` and the global `…epoch`, with no TTL. Every entry is written under the version read before its query, so data read before a change commits never becomes current.
- **Invalidation:** the version is incremented (INCR) after the commit that changes `users.status`, `users.firebase_uid`, `staff_profiles`, `user_roles` or `role_permissions`. The EF Core interceptor `AccessCacheInvalidationInterceptor` does it for tracked changes. `ExecuteUpdate`, raw SQL and FK cascades bypass it, so their caller calls `IAccessCache.BumpAsync`.
- **Failed bumps:** a bump that fails stays pending. It is retried every second, and the instance reads PostgreSQL only until it succeeds. A restored connection bumps the epoch. A bump pending on an instance that stops, or a partition affecting one instance, is only limited by the 5-minute TTL safety net.
- **Redis unavailable:** requests read PostgreSQL. Commands time out after `Redis:CommandTimeoutMs` (250 ms).
- **Eviction policy:** Redis must run with `maxmemory-policy` `volatile-*` or `noeviction`, never `allkeys-*`. An evicted version key would bring an old entry back.

## Time and money

Timestamps are stored in UTC; every day-based calculation uses `Asia/Ho_Chi_Minh` through one clock service (R-TZ-01). Money is `decimal` in C# and `numeric(15,0)` VND in the database.
