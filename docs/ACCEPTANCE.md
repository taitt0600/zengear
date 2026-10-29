# Testing on the development environment

Where the owner tests a finished feature as an end user before its pull request merges, and where `/finish-feature`
runs the Postman collection first (DEC-100, `/finish-feature` steps 7 and 13). It is the development environment of
`api/CLAUDE.md` (*Local database*): PostgreSQL 18, Redis and the Firebase Auth Emulator of `api/docker-compose.yml`,
and the API run from the branch with the launch profile `http`. Each feature has a folder in the Postman collection
`postman/collections/ZenGear`, whose tests check the status codes, the Problem Details types and the fields of every
acceptance criterion and rule.

Requirements: Docker Desktop, the .NET SDK of `api/global.json`, PowerShell 7, Python 3, the Postman app v12 and the
Postman CLI pinned to 1.69.0 (`npm install -g postman-cli@1.69.0`; `postman --version`), and for the web Node.js of
`web/.nvmrc` with its own npm. Every command below runs from the root of the repository.

## Test a feature

`/finish-feature` leaves the environment ready (reset, API of the branch running and, for a branch that changes `web/`,
the web with `npm run dev`) and posts a test guide of one page on the pull request. Before that it has run the
Postman collection and, for a branch that changes `web/`, the Playwright flows of `web/e2e/` (below, *Screen flows*),
which CI runs again. Then:

1. The branch of the pull request is already checked out (`/finish-feature` leaves it so; otherwise the guide's
   appendix gives `git switch <branch>`).
2. When the API changed (the branch changes `api/` or `postman/`): open the Postman app on the repository folder
   (once per machine: **Files** → **Open folder** → the repository, then **Local View**: the app reads `postman/`
   directly and follows the branch checked out in the folder, which it shows), choose the environment `dev`, and on
   the folder of the feature (for example `STF-01`) **Run folder**: every test is green. A web-only pull request
   (the screens of a feature already done, `/implement-feature` step 1) has no Postman step.
3. When the branch changes `web/`: open the screens of the guide at http://localhost:3000 (the admin under `/admin`)
   and sign in with the account it names (*Accounts*, below); its screenshots show the expected result, each beside
   the link of the screen's approved design to compare with (DEC-119 (5); a screen built before DEC-119 says it has
   none). When the
   web is not running: `npm run dev` in `web/` (Node.js of `web/.nvmrc`; `npm ci` in `web/` first, and again
   whenever `web/package-lock.json` changed).
4. Do the manual checks of the guide, at most three in all, screens included.

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
| Web (admin), `npm run dev` in `web/` | http://localhost:3000/admin |
| API | http://localhost:5256 |
| Scalar: API reference and requests | http://localhost:5256/scalar |
| Firebase Emulator UI: accounts | http://localhost:4000/auth |
| PostgreSQL (database `zengear`) | `localhost:5432` (pgAdmin) |
| Redis: the access cache, keys `zengear:access:*` | `127.0.0.1:6379` (Redis Insight; not `localhost`, which resolves to `::1` first) |

## Accounts

- **Admin** `owner@zengear.test`: created by `tools/dev_reset.ps1` in the emulator, then linked with the one-off
  command `bootstrap-admin` (DEC-096). The command's password reset link is not printed; the account is for this
  environment only.
- **Sign-in in Postman**: a request names its account in the header `X-ZenGear-As`; the collection script looks the
  account up in the emulator and signs in with an unsigned custom token, so no password is stored or typed anywhere.
  This works only against the emulator.
- **Sign-in on the web**: the Admin and the fixture accounts have the emulator password `dev-reset-only` that
  `tools/dev_reset.ps1` gives them (a test value of this environment only; `ci-only` in CI).
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

## Screen flows

`npm run e2e` in `web/` runs the Playwright flows of `web/e2e/` against this environment: it starts `npm run dev` on
http://localhost:3000 (or reuses a running one) and needs the API and the emulator above. Once per machine,
`npm exec --no -- playwright install chromium` downloads the browser. The flows sign in with the password above
(`E2E_PASSWORD`, default `dev-reset-only`) and create the staff accounts they need through the API and the emulator,
with unique emails: each run leaves those accounts in the database `zengear` and in the emulator until the next
`tools/dev_reset.ps1`. `/finish-feature` runs them (step 7) after the whole Postman collection when the branch
changes `web/`, and the reset of its test before merge removes those accounts. CI runs the same flows in the job
`acceptance` on a production build.

## The acceptance compose (CI only)

`api/acceptance/compose.yml` runs the API image with the emulator, PostgreSQL and Redis on other ports (API 8180) for
CI; the Postman environment `ci` points to it. The job `acceptance` of `.github/workflows/pipeline.yml` builds the
images and the migration bundle, prepares the data as `tools/dev_reset.ps1` does (Admin, fixtures) before the API
starts, runs the whole collection, then, when `web/`, `api/` or `postman/` changed, the Playwright flows of `web/e2e/`
on a production build of the web (`next start` on http://localhost:3000). It is not run on the owner's machine: its
emulator takes the same ports (9099, 4000) as the development one. If it is up, `docker compose -p zengear-acceptance down -v`.
