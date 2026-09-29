# ZenGear API

.NET 10, ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL 18 — hosted on Azure. Read `../docs/ARCHITECTURE.md` before adding code. All commands below run from `api/`.

## Layout

```
api/
├─ src/ZenGear.Api/            Presentation: controllers, middleware, auth, ProblemDetails, hosting of jobs
├─ src/ZenGear.Services/       Business Logic: services, DTOs, validators, Integrations/, Jobs/
├─ src/ZenGear.Data/           Data Access: Entities/, ZenGearDbContext, Configurations/, Conventions/, Migrations/
├─ tests/ZenGear.Services.Tests/        unit — NUnit, Shouldly, Moq
├─ tests/ZenGear.Api.IntegrationTests/  integration — NUnit, Testcontainers (postgres:18), Respawn
├─ tests/ZenGear.ArchitectureTests/     layers, folders and names — NUnit, NetArchTest (no Docker)
├─ db/        schema specification (zengear_00–08) — the migrations must produce exactly this
├─ seed/      reference data, run after migrations
├─ openapi/   ZenGear.Api.json, generated at build — commit it, never edit it
└─ docker-compose.yml   local PostgreSQL 18 and Redis (+ ZenGear.Api container)
```

Folders, file names and type names follow `../docs/ARCHITECTURE.md`, section *Folder structure*: a top-level folder is a business area or a listed technical folder, one type per file, and where a type goes follows from its name. Namespaces follow folders.

## Rules that must not be broken

- `Api` → `Services` → `Data`; `Data` references no other project.
- Never create a folder, or a `*Helper`/`*Util`/`*Utility`/`*Manager` type, that *Folder structure* does not allow; `ZenGear.ArchitectureTests` fails `dotnet test` and CI otherwise. If the code needs a new folder, stop and ask.
- SonarAnalyzer.CSharp runs in every build and its warnings are errors: fix the code. Never silence a rule (`#pragma warning disable`, `[SuppressMessage]`, `.editorconfig` severity) without asking; a suppression that is approved carries its reason (`Justification = "…"` or a comment).
- Controllers never touch `ZenGearDbContext`; the API returns DTOs only (cost fields and `internal_note` must not leak — R-SEC-09, R-SEC-10).
- A service writes only the tables of its own area; call the owning service for anything else.
- Schema changes only through EF Core migrations. The result must match `db/` exactly (names, types, constraints, indexes). Update `db/` and `../docs/DATABASE.md` in the same change.
- Constraint and index names are set explicitly to the names in `db/` (≤ 63 characters). PostgreSQL-only objects (exclusion constraint, triggers, view, functions) go through `migrationBuilder.Sql`.
- Every business rule implemented gets at least one test whose name starts with its ID (`R_INV_01_...`).
- Enums: members in PascalCase, each declares its stored value with `[JsonStringEnumMemberName("ACTIVE")]` exactly as listed in `../docs/DATABASE.md`. `ZenGearDbContext` stores every enum property through `AttributeEnumConverter<TEnum>`, so the database and the JSON carry the same value; never configure `HasConversion<string>()` by hand.
- Money is `decimal` in C#, `numeric(15,0)` VND in the database. Time comes from the clock service, never `DateTime.Now`.
- External systems only through the interfaces in `ZenGear.Services/Integrations` (`IEmailSender`, `IEInvoiceClient`, `IPaymentGateway`, `IFileStorage`, `IAuthProvider`); tests use fakes.
- Endpoints follow `../docs/API_CONVENTIONS.md`.
- Never log a token, password or OTP, and never destructure (`{@Dto}`) a DTO or object holding a secret or personal data; log ids and harmless values only (R-SEC-11).

## Commands

- Restore local tools (dotnet-ef): `dotnet tool restore`
- Build and test: `dotnet build ZenGear.slnx` · `dotnet test ZenGear.slnx` — always name the solution: `api/` also holds `docker-compose.dcproj`, so a bare `dotnet build` fails with MSB1011
- New migration: `dotnet ef migrations add <Name> -p src/ZenGear.Data -s src/ZenGear.Api`
- Drift check (must report no changes): `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api`
- Schema check against the specification, on a database built by the migrations:
  `psql -At -d zengear -f ../tools/schema_snapshot.sql > migrated.txt`, the same on a database built from `db/zengear_01_database.sql` then `db/zengear_02…` up to the phase in progress, then `diff` the two files — empty means identical.

## Local database

1. Start PostgreSQL and Redis: Visual Studio, startup project `docker-compose`, launch profile `Infrastructure`, Ctrl+F5 — or `docker compose up -d postgres redis` in `api/`.
2. `db/zengear_01_database.sql` on database `postgres` (creates `zengear` with the Vietnamese ICU collation).
3. `dotnet ef database update -p src/ZenGear.Data -s src/ZenGear.Api`.
4. `db/zengear_08_roles.sql`, then `seed/0*.sql` in file order (see `seed/README.md`).

Connection strings live in the user-secrets of `ZenGear.Api`, never in `appsettings.json`:

| Key | Used by | Login |
|---|---|---|
| `ConnectionStrings:ZenGear` | the running API and jobs | a login granted `zengear_app` (jobs: `zengear_jobs`) |
| `ConnectionStrings:ZenGearMigrations` | `dotnet ef` (selected when `EF.IsDesignTime`) | the schema owner |
| `ConnectionStrings:Redis` | the access cache (R-SEC-07); `localhost:6379` locally, `redis:6379` inside docker-compose | — (without it every request reads PostgreSQL) |

Until the first migration and `db/zengear_08_roles.sql` have run, both keys use the local `postgres` user. The application never connects as the schema owner outside local work (DEC-089).
