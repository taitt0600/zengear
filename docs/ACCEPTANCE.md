# Acceptance environment

Where the owner tests a finished feature as an end user, and where an unattended session runs the scenarios of
its acceptance report before posting it (`/finish-feature`, step 8). It runs the API in the Development
environment against the Firebase Auth Emulator, PostgreSQL 18 and Redis, all in Docker
(`api/acceptance/compose.yml`). It is separate from the Visual Studio compose project in `api/`, so both can
run side by side.

Requirements: Docker Desktop (WSL 2 on Windows), Python 3 and Git Bash. Every command below runs in Git Bash from
the root of the repository.

## Start

```bash
docker compose -f api/acceptance/compose.yml up -d --build
docker compose -f api/acceptance/compose.yml logs init   # ends with "init: done"
curl http://localhost:8180/health                         # Healthy
```

The first build takes several minutes (images, NuGet restore, migration bundle); later starts take seconds. After
pulling new code, run the same `up -d --build`: the database set-up applies the new migrations and seed.

| What | Address |
|---|---|
| API | http://localhost:8180 |
| Scalar: API reference and requests | http://localhost:8180/scalar |
| Firebase Emulator UI: accounts | http://localhost:4000/auth |
| PostgreSQL (database `zengear`) | `localhost:55432`, user `postgres`, password `acceptance` |

Every port is bound to `127.0.0.1`: the environment is not reachable from another machine. Its passwords serve
this environment only.

## What the set-up does

The one-off `init` service runs, in the deploy order of `docs/ARCHITECTURE.md`: `api/db/zengear_01_database.sql`
when the database does not exist yet, the EF Core migration bundle, `api/db/zengear_08_roles.sql`, then
`api/seed/0*.sql` in file order. Phase 1 is migrated table by table, so a script that needs a table or function
of the specification that is not migrated yet is skipped as a whole, with the line
`init: skipped <file>: <object> is not migrated yet`; any other error stops the set-up. Until
`zengear_08_roles.sql` runs, the API connects as the schema owner (`api/CLAUDE.md`, Local database).

## Accounts and ID tokens

- A Firebase account lives in the emulator. Create one in the Emulator UI, or with the script, which signs up
  (`--create`) or signs in and prints the uid on stderr and the ID token on stdout:

  ```bash
  python tools/emulator_token.py staff@zengear.test 'any-password' --create
  TOKEN=$(python tools/emulator_token.py staff@zengear.test 'any-password')
  curl -H "Authorization: Bearer $TOKEN" http://localhost:8180/api/v1/me
  ```

- In Scalar, choose the Bearer authentication and paste the token; it is valid for one hour.
- A Firebase account is not yet a ZenGear account: until a user row links its uid, the API answers
  `403 /problems/account-not-registered`. The first Admin is created with `bootstrap-admin` (DEC-096), which
  comes with STF-01; its steps are added here then.

A feature that needs accounts or data to be tested adds their set-up to this section in the same pull request.

## Stop and reset

```bash
docker compose -f api/acceptance/compose.yml down      # stop; the database and the accounts are kept
docker compose -f api/acceptance/compose.yml down -v   # reset: remove the database and the accounts
```

The emulator exports its accounts when it stops and imports them when it starts, so the users in the database
and the accounts in the emulator stay together; `down -v` removes both.

## CI

The `acceptance` workflow starts this environment on every pull request that changes `api/`, and checks that the
set-up completes, `/health` answers `Healthy`, `GET /api/v1/me` refuses a request without a token (401) and
accepts an emulator token (403 `account-not-registered` for an unknown user). It is not a required check.
