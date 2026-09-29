# ZenGear API

.NET 10, ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL 18 — hosted on Azure. Read `../docs/ARCHITECTURE.md` before adding code. All commands below run from `api/`.

## Layout

```
api/
├─ src/ZenGear.Api/            Presentation: controllers, middleware, auth, ProblemDetails, hosting of jobs
├─ src/ZenGear.Services/       Business Logic: services, DTOs, validators, Integrations/, Jobs/
├─ src/ZenGear.Data/           Data Access: Entities/, ZenGearDbContext, Configurations/, Migrations/
├─ tests/ZenGear.Services.Tests/        unit — NUnit, Shouldly, Moq
├─ tests/ZenGear.Api.IntegrationTests/  integration — NUnit, Testcontainers (postgres:18), Respawn
├─ db/        schema specification (zengear_00–08) — the migrations must produce exactly this
├─ seed/      reference data, run after migrations
└─ openapi/   ZenGear.Api.json, generated at build — commit it, never edit it
```

Folders inside each project are the business areas of `../docs/ARCHITECTURE.md`, with the same names; namespaces follow folders.

## Rules that must not be broken

- `Api` → `Services` → `Data`; `Data` references no other project.
- Controllers never touch `ZenGearDbContext`; the API returns DTOs only (cost fields and `internal_note` must not leak — R-SEC-09, R-SEC-10).
- A service writes only the tables of its own area; call the owning service for anything else.
- Schema changes only through EF Core migrations. The result must match `db/` exactly (names, types, constraints, indexes). Update `db/` and `../docs/DATABASE.md` in the same change.
- Constraint and index names are set explicitly to the names in `db/` (≤ 63 characters). PostgreSQL-only objects (exclusion constraint, triggers, view, functions) go through `migrationBuilder.Sql`.
- Every business rule implemented gets at least one test whose name starts with its ID (`R_INV_01_...`).
- Money is `decimal` in C#, `numeric(15,0)` VND in the database. Time comes from the clock service, never `DateTime.Now`.
- External systems only through the interfaces in `ZenGear.Services/Integrations` (`IEmailSender`, `IEInvoiceClient`, `IPaymentGateway`, `IFileStorage`, `IAuthProvider`); tests use fakes.
- Endpoints follow `../docs/API_CONVENTIONS.md`.

## Commands

- Build and test: `dotnet build` · `dotnet test`
- New migration: `dotnet ef migrations add <Name> -p src/ZenGear.Data -s src/ZenGear.Api`
- Drift check (must report no changes): `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api`
- Schema check against the specification, on a database built by the migrations:
  `psql -At -d zengear -f ../tools/schema_snapshot.sql > migrated.txt`, the same on a database built from `db/zengear_01_database.sql` then `db/zengear_02…` up to the phase in progress, then `diff` the two files — empty means identical.

## Local database

1. `docker compose up -d` at the repository root.
2. `db/zengear_01_database.sql` on database `postgres` (creates `zengear` with the Vietnamese ICU collation).
3. `dotnet ef database update -p src/ZenGear.Data -s src/ZenGear.Api`.
4. `db/zengear_08_roles.sql`, then `seed/0*.sql` in file order (see `seed/README.md`).

The application connects as a login granted `zengear_app`, jobs as `zengear_jobs`, never as the schema owner (DEC-089).
