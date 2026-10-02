---
name: env-doctor
description: Diagnose and fix the local ZenGear environment on the developer's machine (Docker, SDK, ports of PostgreSQL, Redis and the Firebase Auth Emulator, the acceptance environment holding the emulator ports, database zengear, migrations, roles, seed, user-secrets, the API /health). Use when the API, Visual Studio, docker compose, the database or the emulator does not start or misbehaves locally.
allowed-tools: Bash(pwsh -NoProfile -File tools/env_doctor.ps1*), Read
---
1. From the repository root: `pwsh -NoProfile -File tools/env_doctor.ps1`. It changes nothing.
2. Rows come in set-up order and a later row often fails because of an earlier one: fix the first FAIL with the
   command of its Hint. Ask before anything that deletes data (`docker compose down -v`, dropping a database) and
   before stopping the acceptance environment (`/finish-feature` or the owner may be using it).
3. Run the check again after each fix; stop when no row is FAIL, then list the rows still WARN.
4. Never print, copy or log the value of a user-secret or a connection string; name keys only.
5. Visual Studio is outside reach: tell the owner what to do there (startup project `docker-compose`, launch profile
   `Infrastructure`, Ctrl+F5), or run the equivalent `docker compose up -d postgres redis firebase` in `api/` when the
   owner agrees.
