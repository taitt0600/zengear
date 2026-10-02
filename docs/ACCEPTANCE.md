# Acceptance environment

Where the owner tests a finished feature as an end user, and where an unattended session runs the scenarios of
its acceptance report before posting it (`/finish-feature`, step 8). It runs the API in the Development
environment against the Firebase Auth Emulator, PostgreSQL 18 and Redis, all in Docker
(`api/acceptance/compose.yml`). It is separate from the Visual Studio compose project in `api/`; both can run
side by side, except the Firebase Auth Emulator, which takes ports 9099 and 4000 in each: stop the `firebase`
service of one before starting the other.

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
| Redis: the access cache, keys `zengear:access:*` | `localhost:56379`, no password |

pgAdmin and Redis Insight, installed on the machine, connect to these ports. Every port is bound to `127.0.0.1`:
the environment is not reachable from another machine. Its passwords serve this environment only.

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
  `403 /problems/account-not-registered`.
- The first Admin (DEC-096): create the Firebase account, then link it with the one-off command `bootstrap-admin`
  of the API. It refuses when an Admin already exists, so it runs once per environment (again after `down -v`).

  ```bash
  python tools/emulator_token.py owner@zengear.test 'owner-password' --create
  docker compose -f api/acceptance/compose.yml run --rm api bootstrap-admin --email owner@zengear.test --name "Chủ cửa hàng"
  TOKEN=$(python tools/emulator_token.py owner@zengear.test 'owner-password')
  ```

  The command prints the user id and a password reset link; the link is not needed here, since the account already
  has the password chosen above.
- Staff accounts (STF-01): the Admin creates them with `POST /api/v1/staff`; the role ids come from `roles` in
  pgAdmin (`SELECT id, name FROM roles`). A new staff member signs in with the default password of this
  environment, `acceptance` (`Firebase__DefaultStaffPassword` in `api/acceptance/compose.yml`), and must change it
  before anything else: every endpoint but `GET /api/v1/me` answers `403 /problems/password-change-required` until
  the password change of STF-03.

  ```bash
  TOKEN_STAFF=$(python tools/emulator_token.py cashier@zengear.test 'acceptance')
  ```

A feature that needs accounts or data to be tested adds their set-up to this section in the same pull request.

## Stop and reset

```bash
docker compose -f api/acceptance/compose.yml down      # stop; the database and the accounts are kept
docker compose -f api/acceptance/compose.yml down -v   # reset: remove the database and the accounts
```

The emulator exports its accounts when it stops and imports them when it starts, so the users in the database
and the accounts in the emulator stay together; `down -v` removes both. A stop gives it 30 seconds to export
(`stop_grace_period`); a container that is killed (`docker kill`, `docker rm -f`) loses the accounts created
since it started.

## CI

The `acceptance` workflow starts this environment on every pull request that changes `api/`, and checks that the
set-up completes, `/health` answers `Healthy`, `GET /api/v1/me` refuses a request without a token (401) and
accepts an emulator token (403 `account-not-registered` for an unknown user), and that an account of the emulator
still signs in with the same uid after a restart. It is not a required check.
