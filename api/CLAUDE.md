# ZenGear API

.NET 10, ASP.NET Core, EF Core 10 + Npgsql, PostgreSQL 18 — hosted on Azure. Read `../docs/ARCHITECTURE.md` (where code goes) and `../docs/CODING_GUIDE.md` (how to write it) before adding code. All commands below run from `api/`.

## Layout

```
api/
├─ src/ZenGear.Api/            Presentation: Controllers/<Area>/ and technical folders (ExceptionHandling/, Middlewares/, Extensions/, …)
├─ src/ZenGear.Services/       Business Logic: <Area>/README.md, <Area>/<Module>/ (flat: services, requests, DTOs, validators, jobs; a mechanism folder inside), Common/
├─ src/ZenGear.Data/           Data Access: Common/, Entities/<Area>/, Configurations/<Area>/, Externals/<System>/, Migrations/
├─ tests/ZenGear.Services.Tests/        unit — NUnit, Shouldly, Moq
├─ tests/ZenGear.Api.IntegrationTests/  integration — NUnit, Testcontainers (postgres:18), Respawn
├─ tests/ZenGear.ArchitectureTests/     layers, folders and names — NUnit, NetArchTest (no Docker)
├─ db/        schema specification (zengear_00–08) — the migrations must produce exactly this
├─ seed/      reference data, run after migrations
├─ openapi/   ZenGear.Api.json, generated at build — commit it, never edit it
├─ acceptance/          acceptance compose of CI only (../docs/ACCEPTANCE.md): compose.yml, Firebase Auth Emulator, database set-up
└─ docker-compose.yml   local PostgreSQL 18 and Redis (+ ZenGear.Api container)
```

Folders, file names and type names follow `../docs/ARCHITECTURE.md`, section *Folder structure*: `ZenGear.Services/<Area>/<Module>/` with a `README.md` per area, `ZenGear.Api/Controllers/<Area>/`, the listed technical folders, one type per file. Namespaces follow folders. Tests mirror `<Area>/<Module>/`.

## Rules that must not be broken

- `Api` → `Services` → `Data`; `Data` references no other project.
- New folders: the feature that needs them creates, without asking, a module of an area listed in `../docs/ARCHITECTURE.md` (with its line under *Modules* in the area's `README.md`, and the README with the area's first module), and for such an area its folders `Controllers/<Area>`, `Entities/<Area>` (with `Enums/`), `Configurations/<Area>` and the test folders `<Area>/<Module>`. A new area, a mechanism folder, a technical folder of `ZenGear.Api` or `ZenGear.Data`, or a folder in `Common`: stop and ask. Never a `*Helper` or `*Manager` type (`*Utility` only in `ZenGear.Services/Common/Utilities`); `ZenGear.ArchitectureTests` fails `dotnet test` and CI otherwise.
- SonarAnalyzer.CSharp runs in every build and its warnings are errors: fix the code. Never silence a rule (`#pragma warning disable`, `[SuppressMessage]`, `.editorconfig` severity) without asking; a suppression that is approved carries its reason (`Justification = "…"` or a comment).
- Controllers never touch `ZenGearDbContext`; they take `*Request` and return `*Dto` of `ZenGear.Services` only, one DTO per audience (cost fields and `internal_note` must not leak — R-SEC-09, R-SEC-10).
- Mapping (AutoMapper) only from entity to DTO, declared in the DTO as a nested `private sealed class Mapping : Profile`; no mapping in `ZenGear.Api`, no map to an entity.
- No repository or unit of work: services use `ZenGearDbContext`. A query repeated in three or more services becomes a `*Query` class in the owning area.
- A use case lives in the area that owns the aggregate it creates or changes; it writes only that area's tables and calls the owning service for anything else (an IL scan in `ZenGear.ArchitectureTests` checks it). Another area is called only through a service listed under *Public contract* in its `README.md`; adding one there is part of the change that needs it. `Locations` depends on no area but reads of `Geography`.
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

## Analyzer rules that often fail the build

SonarAnalyzer rules that turned builds red in earlier features. After each edit of a `.cs` file, `.claude/hooks/analyzer-check.ps1` builds its project and reports them at once (trial of DEC-100); `/finish-feature` adds a line here for each new rule it meets.

- `S6667` — a log call inside a `catch` must pass the exception, so the stack trace is not lost. Pass it (`logger.LogWarning(ex, …)`); when the exception may hold personal data (R-SEC-11), keep what you need inside the `catch` and log after it.
- `S3358` — no nested ternary operator: it hides branches. Use `if`/`else` or a `switch` expression.
- `S2325` — a method that uses no instance member must be static. It also fails a stub (`=> throw new NotImplementedException()`) on an entity or service written before its tests: write the real method body, or let the test fail on a missing endpoint or an empty validator instead of a stub.
- `S2139` — a `catch` that logs an exception and rethrows it logs it twice (the host logs the unhandled exception). Either handle it, or rethrow it wrapped with context (`throw new InvalidOperationException("…", ex)`) without logging; keep the message to ids (R-SEC-11).
- `S6960` — a controller whose actions use disjoint dependencies has several responsibilities. Give a new family of use cases its own controller on the same route prefix (`StaffStatusController` beside `StaffController`), matching its `<Entity><UseCase>Service`.
- `S8969` — a null-forgiving operator (`!`) where the compiler already knows the value is not null (an earlier `x!.y` in the same flow counts). Leave it out; state the assumption in a comment if it matters.
- `S3236` — passing an argument to a parameter marked `[CallerArgumentExpression]` (`ArgumentNullException.ThrowIfNull(value, paramName)`) hides the caller information the compiler fills in. Call `ThrowIfNull(value)` alone, or check the value yourself (`value?.Trim() ?? ""`) and throw with the name you need.
- `S1144` — an unused private member. It also fires between two edits when a private method is replaced by a shared one: remove the private copy in the same edit as its last caller.

## Commands

- Restore local tools (dotnet-ef): `dotnet tool restore`
- Build and test: `dotnet build ZenGear.slnx` · `dotnet test ZenGear.slnx` — always name the solution: `api/` also holds `docker-compose.dcproj`, so a bare `dotnet build` fails with MSB1011
- New migration: `dotnet ef migrations add <Name> -p src/ZenGear.Data -s src/ZenGear.Api`
- Drift check (must report no changes): `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api`
- Schema check against the specification, on a database built by the migrations:
  `psql -At -d zengear -f ../tools/schema_snapshot.sql -o migrated.txt`, the same into `spec.txt` on a database built from `db/zengear_01_database.sql` then `db/zengear_02…` up to the phase in progress, then `python3 ../tools/schema_compare.py spec.txt migrated.txt`: every table, view and function the migrations build matches the specification line for line (the tables not built yet are left out).

## Local database

1. Start PostgreSQL, Redis and the Firebase Auth Emulator: Visual Studio, startup project `docker-compose`, launch profile `Infrastructure`, Ctrl+F5 — or `docker compose up -d postgres redis firebase` in `api/`. Steps 2–4, plus the Admin account and the dev-only fixtures of `../postman/fixtures`, are what `pwsh -NoProfile -File ../tools/dev_reset.ps1` does, against this local environment only (`../docs/ACCEPTANCE.md`).
2. `db/zengear_01_database.sql` on database `postgres` (creates `zengear` with the Vietnamese ICU collation).
3. `dotnet ef database update -p src/ZenGear.Data -s src/ZenGear.Api`.
4. `db/zengear_08_roles.sql`, then `seed/0*.sql` in file order (see `seed/README.md`).

`pwsh -NoProfile -File ../tools/env_doctor.ps1` checks these steps, the ports and the user-secrets below (by key name, never their values) without changing anything; each failed row says what to run.

Connection strings and the default staff password live in the user-secrets of `ZenGear.Api`, never in `appsettings.json`:

| Key | Used by | Login |
|---|---|---|
| `ConnectionStrings:ZenGear` | the running API and jobs | a login granted `zengear_app` (jobs: `zengear_jobs`) |
| `ConnectionStrings:ZenGearMigrations` | `dotnet ef` (selected when `EF.IsDesignTime`) | the schema owner |
| `ConnectionStrings:Redis` | the access cache (R-SEC-07); `localhost:6379` locally, `redis:6379` inside docker-compose | — (without it every request reads PostgreSQL) |
| `Firebase:DefaultStaffPassword` | the password of every staff account created through `POST /api/v1/staff` (STF-01); at least 6 characters; in production the environment variable `Firebase__DefaultStaffPassword` | — (without it creating a staff account fails with 500 and creates nothing) |

Until the first migration and `db/zengear_08_roles.sql` have run, both keys use the local `postgres` user. The application never connects as the schema owner outside local work (DEC-089).
