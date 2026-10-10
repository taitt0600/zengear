---
description: Implement one blueprint feature end to end with one command (rules → tests → code → Postman folder → pull request → test before merge)
argument-hint: <Feature ID, e.g. POS-01> [--checkpoints]
---
Implement the feature named in $ARGUMENTS of ZenGear and take it to a pull request with green CI that the owner
tests before it merges (CLAUDE.md, Workflow; DEC-100). It runs in Claude Code on the owner's machine only, from the
ready check to the test before merge, without stopping, except to ask a **Needs decision** item (step 4).
`$ARGUMENTS` is the Feature ID, optionally followed by `--checkpoints`: then also stop at the three checkpoints of
DEC-099 marked **Checkpoint** below (two here, one in `/finish-feature`); without it, skip them.
`python3` is `python` on Windows.

1. **Ready check** — if one fails, stop and write only the report of `/finish-feature` (step 14), without a
   pull request:
   - `git fetch origin main` and `git switch -c feat/<id in lowercase> origin/main`. Never `main`. `git status`
     shows no change.
   - The feature is a row of `docs/blueprint/features.md`; for every feature in its Depends On column,
     `python3 tools/pr_gates.py coverage <ID>` exits 0 (every rule has a test, so it is implemented).
   - `dotnet --version` is the SDK of `api/global.json`, `dotnet tool restore` succeeds, `docker info` answers
     (the integration tests start PostgreSQL with Testcontainers), `pwsh -v` answers (without PowerShell 7 the
     hooks of `.claude/settings.json` do not run, and nothing guards the specification) and `postman --version`
     answers (the Postman CLI, `docs/ACCEPTANCE.md`).
2. **Read** the feature dossier: `python3 tools/feature_context.py <ID> --out out/feature-<ID>.md` (skill
   `feature-context`), then `out/feature-<ID>.md`. It holds the row of the feature in `docs/blueprint/features.md`,
   its use cases, every rule of its Rules column (implementation rules — R-DB-, R-TX-, R-NOT-, R-AUD-, R-SEC-07,
   R-ORD-12, R-REC- — from `docs/blueprint/technical-design.md`, section 8), the decisions they cite, its rows of
   `docs/GLOSSARY.md` and the `docs/DATABASE.md` section of each table its rules touch. Then read the owning area of
   each table in `docs/ARCHITECTURE.md`. Open a source document only for what the dossier does not hold; a rule the
   dossier reports as not found is a Needs decision item (step 4). When the code the feature touches is unfamiliar,
   the agent `feature-dev:code-explorer` may map it first (entry points, call paths, the tests that cover it).
3. **Plan**: endpoints (per `docs/API_CONVENTIONS.md`), service methods, entity methods, migrations if any, one or
   more tests per rule, the Postman scenarios (one per acceptance criterion, a refusal per rule) and the open
   questions sorted by the policy of step 4. Give the plan and the path `out/feature-<ID>.md` to the
   `plan-reviewer` agent and fix every Blocking and Should fix item; repeat until it answers READY, at most three
   rounds. A Blocking item left after three rounds becomes a Needs decision item. Keep the final verdict and the
   number of its findings labelled `[over-engineering]` and `[duplication]`, over all rounds, for the report.
   - **Needs decision items**, once plan-reviewer answers READY: ask every item known so far at once,
     with AskUserQuestion. One call takes at most 4 questions, so more items go in consecutive calls at the same
     moment, before any other work. Each question gives the options with their consequences, your recommendation
     first and marked "(Recommended)". An answer becomes a decision made by the owner, listed as such in the report. An
     answer that changes a specification stops the run: draft the Decision with `/decision-draft` and write the
     report of `/finish-feature` (step 14) without a pull request.
   - **Checkpoint 1** (`--checkpoints` only): show the owner the plan (endpoints, service and entity methods,
     migrations, tests per rule, Postman scenarios), the plan-reviewer verdict and the decisions you made; go on
     when the owner approves, after the changes they ask for.
4. **Decision policy** for every question the specification does not answer:
   - Decide it yourself, and list the choice under "Decisions made" in the report, when it is reversible, stays
     inside the code and agrees with the documents: names of private members, how a service is split within the
     limits of `docs/ARCHITECTURE.md`, the shape of a query, test data, the wording of a message that follows
     `docs/API_CONVENTIONS.md`.
   - Make it a Needs decision item when the answer would change a specification (`docs/blueprint/`,
     `docs/DATABASE.md`, `api/db/`, `docs/ARCHITECTURE.md`); concerns money, prices, tax, invoices or accounting, a
     legal duty or personal data, the permissions the seed grants to roles, or data that is hard to migrate (a
     column, a stored enum value); or needs a new business term in `docs/GLOSSARY.md`, a new package, a build file
     that runs code, a new area, a mechanism folder, a technical folder of `ZenGear.Api` or `ZenGear.Data`, a folder in
     `Common`, a suppressed analyzer rule or an exception to an architecture test. The other new folders of
     *Folder structure* in `docs/ARCHITECTURE.md` are yours to decide (a module of a listed area with its line in
     the area's `README.md`, and that area's `Controllers/`, `Entities/`, `Configurations/` and test folders): list
     them under "Decisions made". The same
     applies wherever `CLAUDE.md`, `api/CLAUDE.md` or `web/CLAUDE.md` says to ask or to stop and ask, and whenever a
     hook denies a change.
   - Ask a Needs decision item at once, with AskUserQuestion as in step 3, also when it appears later in the run.
     When the owner leaves an item open, build what does not depend on it and go on: the pull request is then a
     draft, and the report lists the item under "Cần bạn quyết định".
5. **Backend**, following `api/CLAUDE.md`:
   - Tests first (`api/tests/ZenGear.Services.Tests` for entity and service logic,
     `api/tests/ZenGear.Api.IntegrationTests` for transactions, constraints and endpoints), named after rule IDs;
     run them and see them fail for the expected reason.
   - **Checkpoint 2** (`--checkpoints` only): list the tests by rule ID with the reason each one fails, and go on
     when the owner agrees; add or change the tests they ask for first.
   - Code in the owning area folders of `ZenGear.Data`, `ZenGear.Services`, `ZenGear.Api`; never write another
     area's tables directly.
   - From `api/`: `dotnet build ZenGear.slnx`, `dotnet test ZenGear.slnx`, the migration drift check; fix until
     green. The build refreshes `api/openapi/ZenGear.Api.json`.
6. **Postman folder**: skill `api-collection` with the Feature ID writes the folder of the feature in `postman/`
   (one request per acceptance criterion, a refusal per rule, tests on status, Problem Details `type` and fields,
   values unique to the run, dev-only fixtures for a state the API cannot reach yet) and runs it twice with skill
   `api-run`. A request that fails because the code is wrong is a failed test of step 5: fix the code first.
7. **Web**, following `web/CLAUDE.md` — only for the screens of this feature that `docs/SCREENS.md` describes and
   whose cell in the column *Blocked by* of its screen list is empty. The report names every other screen of the
   feature as not built, with what blocks it; a feature with no screen built is tested through the API:
   - From `web/`: `npm run gen:api`, then pages and components in the matching `features/<area>`.
   - `npm run lint` and `npm run build`.
8. Run `/finish-feature` with the Feature ID (and `--checkpoints` when it was given), the plan verdict and its
   counts of `[over-engineering]` and `[duplication]` findings, the decisions made and the Needs decision items: it verifies, commits, opens the pull request, follows CI, has the owner test
   the feature before merge and reports.
