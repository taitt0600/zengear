# ZenGear API

.NET 10, ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL 18 — hosted on Azure. Read `../docs/ARCHITECTURE.md` before adding code. All commands below run from `api/`.

## Layout

```
api/
├─ src/ZenGear.Api/            Presentation: Controllers/ and technical folders (ExceptionHandling/, Middlewares/, Extensions/, …)
├─ src/ZenGear.Services/       Business Logic: <Area>/ (services, Models/, Enums/, Validators/, mechanisms, jobs), Common/
├─ src/ZenGear.Data/           Data Access: Common/, Entities/<Area>/, Configurations/<Area>/, Externals/<System>/, Migrations/
├─ tests/ZenGear.Services.Tests/        unit — NUnit, Shouldly, Moq
├─ tests/ZenGear.Api.IntegrationTests/  integration — NUnit, Testcontainers (postgres:18), Respawn
├─ tests/ZenGear.ArchitectureTests/     layers, folders and names — NUnit, NetArchTest (no Docker)
├─ db/        schema specification (zengear_00–08) — the migrations must produce exactly this
├─ seed/      reference data, run after migrations
├─ openapi/   ZenGear.Api.json, generated at build — commit it, never edit it
├─ acceptance/          acceptance environment (../docs/ACCEPTANCE.md): compose.yml, Firebase Auth Emulator, database set-up
└─ docker-compose.yml   local PostgreSQL 18 and Redis (+ ZenGear.Api container)
```

Folders, file names and type names follow `../docs/ARCHITECTURE.md`, section *Folder structure*: a top-level folder is a business area or a listed technical folder, one type per file, and where a type goes follows from its name. Namespaces follow folders.

## Rules that must not be broken

- `Api` → `Services` → `Data`; `Data` references no other project.
- Never create a folder that *Folder structure* does not allow, nor a `*Helper` or `*Manager` type (`*Utility` only in `ZenGear.Services/Common/Utilities`); `ZenGear.ArchitectureTests` fails `dotnet test` and CI otherwise. If the code needs a new folder, stop and ask.
- SonarAnalyzer.CSharp runs in every build and its warnings are errors: fix the code. Never silence a rule (`#pragma warning disable`, `[SuppressMessage]`, `.editorconfig` severity) without asking; a suppression that is approved carries its reason (`Justification = "…"` or a comment).
- Controllers never touch `ZenGearDbContext`; they take `*Request` and return `*Dto` of `ZenGear.Services` only, one DTO per audience (cost fields and `internal_note` must not leak — R-SEC-09, R-SEC-10).
- Mapping (AutoMapper) only from entity to DTO, declared in the DTO as a nested `private sealed class Mapping : Profile`; no mapping in `ZenGear.Api`, no map to an entity.
- No repository or unit of work: services use `ZenGearDbContext`. A query repeated in three or more services becomes a `*Query` class in the owning area.
- A use case lives in the area that owns the aggregate it creates or changes; it writes only that area's tables and calls the owning service for anything else (an IL scan in `ZenGear.ArchitectureTests` checks it).
- Services are split by audience (`<Entity>Service` for staff, `<Entity>PublicService` for the storefront) and by family of use cases (`<Entity><UseCase>Service`). A file of `ZenGear.Services` has at most 400 lines of code, a `*Service` at most 10 public methods; never add an exception to `ServiceSizeTests` without asking.
- Type names are unique across the three projects and no public type is nested: a line of a request or DTO is a top-level `*LineRequest` or `*Dto`.
- Schema changes only through EF Core migrations. The result must match `db/` exactly (names, types, constraints, indexes). Update `db/` and `../docs/DATABASE.md` in the same change.
- Constraint and index names are set explicitly to the names in `db/` (≤ 63 characters). PostgreSQL-only objects (exclusion constraint, triggers, view, functions) go through `migrationBuilder.Sql`.
- Every business rule implemented gets at least one test whose name starts with its ID (`R_INV_01_...`).
- Enums: members in PascalCase, each declares its stored value with `[JsonStringEnumMemberName("ACTIVE")]` exactly as listed in `../docs/DATABASE.md`. `ZenGearDbContext` stores every enum property through `AttributeEnumConverter<TEnum>`, so the database and the JSON carry the same value; never configure `HasConversion<string>()` by hand.
- Money is `decimal` in C#, `numeric(15,0)` VND in the database. Time comes from the clock service, never `DateTime.Now`.
- External systems only through the interfaces in `ZenGear.Data/Externals/<System>` (`IEmailSender`, `IEInvoiceClient`, `IPaymentGateway`, `IFileStorage`, `IAuthProvider`, the Redis connection); their packages are referenced by `ZenGear.Data` only; tests use fakes.
- A job (`*Job`) lives in the area it serves and only orchestrates; its advisory lock key is declared in `ZenGear.Services/Common/AdvisoryLockKeys`.
- Endpoints follow `../docs/API_CONVENTIONS.md`.
- Never log a token, password or OTP, and never destructure (`{@Dto}`) a DTO or object holding a secret or personal data; log ids and harmless values only (R-SEC-11).

## Commands

- Restore local tools (dotnet-ef): `dotnet tool restore`
- Build and test: `dotnet build ZenGear.slnx` · `dotnet test ZenGear.slnx` — always name the solution: `api/` also holds `docker-compose.dcproj`, so a bare `dotnet build` fails with MSB1011
- New migration: `dotnet ef migrations add <Name> -p src/ZenGear.Data -s src/ZenGear.Api`
- Drift check (must report no changes): `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api`
- Schema check against the specification, on a database built by the migrations:
  `psql -At -d zengear -f ../tools/schema_snapshot.sql -o migrated.txt`, the same into `spec.txt` on a database built from `db/zengear_01_database.sql` then `db/zengear_02…` up to the phase in progress, then `python3 ../tools/schema_compare.py spec.txt migrated.txt`: every table, view and function the migrations build matches the specification line for line (the tables not built yet are left out).

## Local database

1. Start PostgreSQL, Redis and the Firebase Auth Emulator: Visual Studio, startup project `docker-compose`, launch profile `Infrastructure`, Ctrl+F5 — or `docker compose up -d postgres redis firebase` in `api/`. The emulator uses the ports of the acceptance environment (9099, 4000): run one of the two at a time.
2. `db/zengear_01_database.sql` on database `postgres` (creates `zengear` with the Vietnamese ICU collation).
3. `dotnet ef database update -p src/ZenGear.Data -s src/ZenGear.Api`.
4. `db/zengear_08_roles.sql`, then `seed/0*.sql` in file order (see `seed/README.md`).

`pwsh -NoProfile -File ../tools/env_doctor.ps1` checks these steps, the ports and the user-secrets below (by key name, never their values) without changing anything; each failed row says what to run.

Connection strings live in the user-secrets of `ZenGear.Api`, never in `appsettings.json`:

| Key | Used by | Login |
|---|---|---|
| `ConnectionStrings:ZenGear` | the running API and jobs | a login granted `zengear_app` (jobs: `zengear_jobs`) |
| `ConnectionStrings:ZenGearMigrations` | `dotnet ef` (selected when `EF.IsDesignTime`) | the schema owner |
| `ConnectionStrings:Redis` | the access cache (R-SEC-07); `localhost:6379` locally, `redis:6379` inside docker-compose | — (without it every request reads PostgreSQL) |

Until the first migration and `db/zengear_08_roles.sql` have run, both keys use the local `postgres` user. The application never connects as the schema owner outside local work (DEC-089).
