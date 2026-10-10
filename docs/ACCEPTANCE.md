# Testing on the development environment

Where the owner tests a finished feature as an end user before its pull request merges, and where `/finish-feature`
runs the Postman collection first (DEC-100, `/finish-feature` steps 7 and 13). It is the development environment of
`api/CLAUDE.md` (*Local database*): PostgreSQL 18, Redis and the Firebase Auth Emulator of `api/docker-compose.yml`,
and the API run from the branch with the launch profile `http`. Each feature has a folder in the Postman collection
`postman/collections/ZenGear`, whose tests check the status codes, the Problem Details types and the fields of every
acceptance criterion and rule.

Requirements: Docker Desktop, the .NET SDK of `api/global.json`, PowerShell 7, Python 3, the Postman app v12 and the
Postman CLI pinned to 1.69.0 (`npm install -g postman-cli@1.69.0`; `postman --version`). Every command below runs from
the root of the repository.

## Test a feature

`/finish-feature` leaves the environment ready (reset, API of the branch running) and posts a test guide of one page
on the pull request. Then:

1. Open the Postman app on the repository folder (once per machine: **Files** → **Open folder** → the repository,
   then **Local View**: the app reads `postman/` directly and follows the branch checked out in the folder).
2. Make sure the branch of the pull request is the one checked out (`git switch <branch>`; Postman shows it).
3. Choose the environment `dev`.
4. On the folder of the feature (for example `STF-01`), **Run folder**: every test is green.
5. Do the manual checks of the guide, at most three.

The same folder runs from the terminal: `postman collection run postman/collections/ZenGear -e
postman/environments/dev.environment.yaml -i <Feature ID> --no-report-events` (skill `api-run`). It needs no Postman
sign-in or API key.

## Start or reset the environment

When the environment is not running, or to start from a clean state:

```bash
pwsh -NoProfile -File tools/dev_reset.ps1        # -WhatIf prints what it would do
dotnet run --project api/src/ZenGear.Api --launch-profile http
curl http://localhost:5256/health                # Healthy
```

`tools/dev_reset.ps1` brings the environment back to the state right after the seed: it recreates the database
`zengear` (`api/db/zengear_01_database.sql`, the migrations, `api/db/zengear_08_roles.sql`, `api/seed/0*.sql`),
deletes every account of the emulator, creates the Admin (below), applies the dev-only fixtures of `postman/fixtures/`
and clears Redis. Stop the API before running it: the API synchronizes the permissions when it starts, so it starts
after the reset. Exit code 1 names a missing prerequisite and its fix; 2 means a connection string or the emulator is
not the local one of `api/docker-compose.yml` (it never touches another database).

| What | Address |
|---|---|
| API | http://localhost:5256 |
| Scalar: API reference and requests | http://localhost:5256/scalar |
| Firebase Emulator UI: accounts | http://localhost:4000/auth |
| PostgreSQL (database `zengear`) | `localhost:5432` (pgAdmin) |
| Redis: the access cache, keys `zengear:access:*` | `localhost:6379` (Redis Insight) |

## Accounts

- **Admin** `owner@zengear.test`: created by `tools/dev_reset.ps1` in the emulator, then linked with the one-off
  command `bootstrap-admin` (DEC-096). The command's password reset link is not printed; the account is for this
  environment only.
- **Sign-in in Postman**: a request names its account in the header `X-ZenGear-As`; the collection script looks the
  account up in the emulator and signs in with an unsigned custom token, so no password is stored or typed anywhere.
  This works only against the emulator.
- **Fixtures**: state that no endpoint can reach yet comes from `postman/fixtures/<Feature ID>.sql`, with its emulator
  accounts in `postman/fixtures/accounts.txt`. STF-01: `manager.fixture@zengear.test`, a Store Manager holding
  `staff.create` with no pending password change, for the refusal of R-TX-11. STF-02: Store Manager also holds
  `staff.disable` (the same refusal when disabling the Admin; `staff.read` comes from the permission sync at the API
  start after the reset), and an ANONYMIZED staff account with the fixed id
  `anonymizedStaffId` of the environments and no Firebase account, for the refusal of R-USR-03. STF-03:
  `customer.fixture@zengear.test`, a customer with the Customer role, for the refusal of the staff password change.
- **Role ids** are fixed in `api/seed/01_roles.sql` and named in the environments.
- **By hand**: `python tools/emulator_token.py <email> <password> [--create]` signs up or signs in an emulator account
  and prints its ID token, for Scalar (Bearer) or `curl`. A Firebase account is not yet a ZenGear account: until a
  user row links its uid, the API answers `403 /problems/account-not-registered`. A staff account created through
  `POST /api/v1/admin/staff` signs in with the default password of the user-secret `Firebase:DefaultStaffPassword` and must
  change it first (`403 /problems/password-change-required`, STF-03).

A feature that needs accounts or data adds them as fixtures, or as requests of its folder, in the same pull request
(skill `api-collection`).

## The acceptance compose (CI only)

`api/acceptance/compose.yml` runs the API image with the emulator, PostgreSQL and Redis on other ports (API 8180) for
CI; the Postman environment `ci` points to it. The job `acceptance` of `.github/workflows/pipeline.yml` builds the
images and the migration bundle, prepares the data as `tools/dev_reset.ps1` does (Admin, fixtures) before the API
starts, and runs the whole collection. It is not run on the owner's machine: its emulator takes the same ports
(9099, 4000) as the development one. If it is up, `docker compose -p zengear-acceptance down -v`.
