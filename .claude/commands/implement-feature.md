---
description: Implement one blueprint feature end to end, unattended (rules → tests → code → pull request)
argument-hint: <Feature ID, e.g. POS-01>
---
Implement feature $ARGUMENTS of ZenGear and take it to a merged pull request without waiting for anyone
(CLAUDE.md, Workflow). Never stop to ask: decide what the policy of step 4 lets you decide, and turn everything
else into a **Needs decision** item for the report. A cloud session (`CLAUDE_CODE_REMOTE=true`) has nobody watching.

1. **Ready check** — if one fails, stop and write only the report of `/finish-feature` (step 12), without a
   pull request:
   - The branch: in a cloud session, the branch the session was given; locally, `git fetch origin main` and
     `git switch -c feat/<id in lowercase> origin/main`. Never `main`. `git status` shows no change.
   - The feature is a row of `docs/blueprint/features.md`; for every feature in its Depends On column,
     `python3 tools/pr_gates.py coverage <ID>` exits 0 (every rule has a test, so it is implemented).
   - `dotnet --version` is the SDK of `api/global.json`, `dotnet tool restore` succeeds, `docker info` answers
     (the integration tests start PostgreSQL with Testcontainers) and `pwsh -v` answers (without PowerShell 7 the
     hooks of `.claude/settings.json` do not run, and nothing guards the specification).
2. **Read** the row of $ARGUMENTS in `docs/blueprint/features.md`, its use cases in `docs/blueprint/use-cases.md`,
   every rule listed in its Rules column in `docs/blueprint/business-rules.md`, and the related decisions. A rule ID
   that is not in `business-rules.md` (R-DB-, R-TX-, R-NOT-, R-AUD-, R-SEC-07, R-ORD-12, R-REC-) is an
   implementation rule: read it in `docs/blueprint/technical-design.md`, section 8. Look up each table it touches in
   `docs/DATABASE.md` (`#### <table>`), its owning area in `docs/ARCHITECTURE.md`, and the names in `docs/GLOSSARY.md`.
3. **Plan**: endpoints (per `docs/API_CONVENTIONS.md`), service methods, entity methods, migrations if any, one or
   more tests per rule, and the open questions sorted by the policy of step 4. Give the plan to the `plan-reviewer`
   agent and fix every Blocking and Should fix item; repeat until it answers READY, at most three rounds. A Blocking
   item left after three rounds becomes a Needs decision item. Keep the final verdict for the report.
4. **Decision policy** for every question the specification does not answer:
   - Decide it yourself, and list the choice under "Decisions made" in the report, when it is reversible, stays
     inside the code and agrees with the documents: names of private members, how a service is split within the
     limits of `docs/ARCHITECTURE.md`, the shape of a query, test data, the wording of a message that follows
     `docs/API_CONVENTIONS.md`.
   - Make it a Needs decision item, and do not build what depends on it, when the answer would change a
     specification (`docs/blueprint/`, `docs/DATABASE.md`, `api/db/`, `docs/ARCHITECTURE.md`); concerns money,
     prices, tax, invoices or accounting, a legal duty or personal data, the permissions the seed grants to roles,
     or data that is hard to migrate (a column, a stored enum value); or needs a new business term in
     `docs/GLOSSARY.md`, a new package, a build file that runs code, a new folder, a suppressed analyzer rule or an
     exception to an architecture test. The same applies wherever `CLAUDE.md`, `api/CLAUDE.md` or `web/CLAUDE.md`
     says to ask or to stop and ask, and whenever a hook denies a change. Build what does not depend on it.
5. **Backend**, following `api/CLAUDE.md`:
   - Tests first (`api/tests/ZenGear.Services.Tests` for entity and service logic,
     `api/tests/ZenGear.Api.IntegrationTests` for transactions, constraints and endpoints), named after rule IDs;
     run them and see them fail for the expected reason.
   - Code in the owning area folders of `ZenGear.Data`, `ZenGear.Services`, `ZenGear.Api`; never write another
     area's tables directly.
   - From `api/`: `dotnet build ZenGear.slnx`, `dotnet test ZenGear.slnx`, the migration drift check; fix until
     green. The build refreshes `api/openapi/ZenGear.Api.json`.
6. **Web**, following `web/CLAUDE.md` — only when `docs/SCREENS.md` describes the screens of this feature;
   otherwise the report says that the feature is tested through the API:
   - From `web/`: `npm run gen:api`, then pages and components in the matching `features/<area>`.
   - `npm run lint` and `npm run build`.
7. Run `/finish-feature $ARGUMENTS` with the plan verdict, the decisions made and the Needs decision items: it
   verifies, commits, opens the pull request, enables auto-merge, follows CI and reports.
