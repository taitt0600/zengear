---
description: Verify a finished feature, then propose commits and the pull request
argument-hint: <Feature ID, e.g. AUT-04>
---
Finish feature $ARGUMENTS of ZenGear. Stop at the first failing step, report it and fix it before going on.

1. **Branch**: `git branch --show-current` must not be `main`. `git status` lists what will be committed; nothing under `bin/`, `obj/`, secrets or `.env` files.
2. **Build and test** (from `api/`): `dotnet build ZenGear.slnx`, then `dotnet test ZenGear.slnx` (Docker must be running). The build refreshes `api/openapi/ZenGear.Api.json`; it belongs to the commit that changes the API.
3. **Drift check** (from `api/`): `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api` reports no changes.
4. **Schema check** — only when `api/src/ZenGear.Data/Migrations/` changed on this branch. It recreates the local `zengear` database, so tell the user to stop the API first. From `api/` in Bash:
   ```bash
   export MSYS_NO_PATHCONV=1   # Git Bash on Windows: keep /tmp paths for docker exec
   pg=$(docker ps --filter "ancestor=postgres:18" --format "{{.Names}}")
   docker cp db/zengear_01_database.sql "$pg:/tmp/01.sql"
   docker cp db/zengear_02_schema_phase1_store.sql "$pg:/tmp/02.sql"   # add later phase files once that phase starts
   docker cp ../tools/schema_snapshot.sql "$pg:/tmp/snap.sql"
   docker exec "$pg" psql -U postgres -d postgres -v ON_ERROR_STOP=1 -q -f /tmp/01.sql
   docker exec "$pg" psql -U postgres -d zengear -v ON_ERROR_STOP=1 -q -f /tmp/02.sql
   docker exec "$pg" psql -U postgres -d zengear -At -f /tmp/snap.sql -o /tmp/spec.txt
   docker exec "$pg" psql -U postgres -d postgres -v ON_ERROR_STOP=1 -q -f /tmp/01.sql
   dotnet ef database update -p src/ZenGear.Data -s src/ZenGear.Api
   docker exec "$pg" psql -U postgres -d zengear -At -f /tmp/snap.sql -o /tmp/migrated.txt
   ```
   For every table the migrations create (`grep '^relation ' /tmp/migrated.txt` inside the container), the `relation`, `column <table>.`, `constraint <table>.`, `index … ON public.<table> ` and `trigger … ON public.<table> ` lines of `/tmp/spec.txt` and `/tmp/migrated.txt` must be identical. Show the differing lines if any; the remaining difference is the tables not implemented yet.
5. **Specification**: every rule of the feature has a test whose name starts with its ID; if `api/db/`, `docs/DATABASE.md` or `docs/blueprint/` changed, name the approved Decision that allows it, otherwise stop.
6. **Code review**: give the `code-reviewer` agent the feature ID and the branch. Fix every Blocking item and every Should fix item, or say why one is not fixed; after a fix, run steps 2–3 again. Keep its final verdict and the number of Blocking items found and fixed for the pull request.
7. **Commits**: propose the commits per the `Commits` section of `CLAUDE.md` — `feat(db)` for entities, configurations and migrations; `feat(api)` for behavior, endpoints, the OpenAPI document and their tests; `test(api)` or `docs` for the rest — each with body and `Refs:` footer, plus `Closes #<issue>` for issues it resolves. Wait for the user's approval, then create them with `git commit`.
8. **Pull request**: never push. Give the user the PR title (same format as a commit subject, feature ID at the end in parentheses, e.g. `feat(api): add role-based access control (AUT-04)`) and a body: summary, rules covered, how it was verified (steps 2–4 and the code-reviewer verdict with Blocking items found and fixed), follow-ups. When the user says the branch is pushed, create it with `gh pr create --base main --title "…" --body-file <file>`.
