---
description: Implement one blueprint feature (rules → tests → code → Postman folder), then hand it to /finish-feature in a new session
argument-hint: <Feature ID, e.g. POS-01> [--checkpoints]
---
Implement the feature named in $ARGUMENTS of ZenGear up to green tests and its Postman folder (only its screens in
a web-only run, step 1), and hand it to
`/finish-feature`, which the owner starts after `/clear` (CLAUDE.md, Workflow; DEC-100; DEC-112 (4)). It runs in
Claude Code on the owner's machine only, from the ready check to the handoff (step 8), without stopping, except to
ask a **Needs decision** item (step 4) and to wait for the owner to merge a package approval (step 4, **Package
approval**).
Builds, tests and Postman runs write their output to `out/logs/` and bring only a summary into the context, as
**Output** of `/finish-feature` says (DEC-112 (5)).
`$ARGUMENTS` is the Feature ID, optionally followed by `--checkpoints`: then also stop at the three checkpoints of
DEC-099 marked **Checkpoint** below (two here, one in `/finish-feature`); without it, skip them.
`python3` is `python` on Windows.

1. **Ready check** — if one fails, stop and write only the report of `/finish-feature` (step 14), without a
   pull request:
   - When `out/paused-<ID>.md` names this command, switch to the branch it names and resume at item 4 of
     **Package approval** (step 4), from that file, instead of the items below.
   - `git status` shows no change; `git fetch origin main` and `git switch --detach origin/main`, so that the
     items below read the current `main` (never another feature's branch, where the last run left the session).
   - `pwsh -NoProfile -File tools/git_tidy.ps1 -Apply` (DEC-120 (2)): it prunes, then deletes the local branches
     whose pull request merged (after the detach above, the branch of the last run is no longer the current one).
     Report the branches it deleted and kept and its exit code; a failure of it is reported, never a reason to stop.
   - The feature is a row of `docs/blueprint/features.md`, and every Feature ID in its Depends On column is done
     (an Epic name there is not checked). A feature is done when a merged `feat` pull request ends its title with
     `(<ID>)`: `gh pr list --state merged --search "<ID> in:title" --json number,title` gives candidates only; keep
     the titles that match `^feat(\([^)]*\))?!?:.*\(<ID>\)$`. And `python3 tools/pr_gates.py coverage <ID>` exits 0,
     which is necessary, not sufficient (DEC-110 (1)).
   - The feature itself, by the same test: a done feature stops here, unless `docs/SCREENS.md` describes a screen
     of it whose cell in the column *Blocked by* is empty and that `web/` does not have yet (its route has no page,
     or still opens one of the placeholders that the paragraph under the screen list of `docs/SCREENS.md` names).
     Then the run is a **web-only run**: the plan, the tests and the code cover only those screens, and steps 5 and
     6 are skipped; it changes no API, migration or Postman folder (a screen that needs one is a Needs decision
     item, step 4); its branch is `feat/<id in lowercase>-web` and its pull request title
     `feat(web): <subject> (<ID>)`.
   - `dotnet --version` is the SDK of `api/global.json`, `dotnet tool restore` succeeds, `docker info` answers
     (the integration tests start PostgreSQL with Testcontainers), `pwsh -v` answers (without PowerShell 7 the
     hooks of `.claude/settings.json` do not run, and nothing guards the specification), `postman --version`
     answers (the Postman CLI, `docs/ACCEPTANCE.md`) and `node --version` is within the `engines` range of
     `web/package.json` (which `npm ci` enforces; a change of the API contract runs the web checks, step 5).
   - Last, the branch: `git switch -c <name> origin/main`, where `<name>` is `feat/<id in lowercase>` (`-web`
     added for a web-only run), never `main`. Take the first of `<name>`, `<name>-2`, … for which
     `gh pr list --state merged --head <name> --json number` returns nothing (an earlier run of the same feature may
     have merged under it, and `tools/token_usage.py --branch` would count both runs in one Token line).
     When the chosen name exists already (`git branch --list <name>`, `git ls-remote --heads origin <name>`), stop:
     it is work in flight (CLAUDE.md, *Work state*). A ready check that stops leaves HEAD detached at
     `origin/main`; the next run starts there too.
2. **Read** the feature dossier: `python3 tools/feature_context.py <ID> --out out/feature-<ID>.md` (skill
   `feature-context`), then `out/feature-<ID>.md`. It holds the row of the feature in `docs/blueprint/features.md`,
   its rows of `docs/STATUS.md` (section *Work state*: a Next row keyed by its ID, a Waiting row triggered
   `before <ID>`), its use cases, every rule of its Rules column (implementation rules — R-DB-, R-TX-, R-NOT-,
   R-AUD-, R-SEC-07, R-ORD-12, R-REC- — from `docs/blueprint/technical-design.md`, section 8), the decisions they
   cite, its rows of `docs/GLOSSARY.md` and the `docs/DATABASE.md` section of each table its rules touch. Then read the owning area of
   each table in `docs/ARCHITECTURE.md`. Open a source document only for what the dossier does not hold; a rule the
   dossier reports as not found is a Needs decision item (step 4). When the code the feature touches is unfamiliar,
   the agent `feature-dev:code-explorer` may map it first (entry points, call paths, the tests that cover it).
3. **Plan**: endpoints (per `docs/API_CONVENTIONS.md`), service methods, entity methods, migrations if any, one or
   more tests per rule, the Postman scenarios (one per acceptance criterion, a refusal per rule), how each row of
   the dossier's *Work state* section is handled (done in this pull request, or a Needs decision item of step 4)
   and the open questions sorted by the policy of step 4. A web-only run plans the screens, their components, the
   Vitest tests and the Playwright flows instead of endpoints, methods, migrations and Postman scenarios. Give the
   plan and the path `out/feature-<ID>.md` to the `plan-reviewer` agent and fix every Blocking and Should fix
   item; repeat until it answers READY, at most three rounds. A Blocking item left after three rounds becomes a
   Needs decision item. Keep the final verdict and the
   number of its findings labelled `[over-engineering]` and `[duplication]`, over all rounds, for the report.
   - **Needs decision items**, once plan-reviewer answers READY: ask every item known so far at once,
     with AskUserQuestion. One call takes at most 4 questions, so more items go in consecutive calls at the same
     moment, before any other work. Each question gives the options with their consequences, your recommendation
     first and marked "(Recommended)". An answer becomes a decision made by the owner, listed as such in the report. An
     answer that changes a specification stops the run: draft the Decision with `/decision-draft` and write the
     report of `/finish-feature` (step 14) without a pull request.
   - **Checkpoint 1** (`--checkpoints` only): show the owner the plan (endpoints, service and entity methods,
     migrations, tests per rule, Postman scenarios; for a web-only run the screens, components, Vitest tests and
     Playwright flows), the plan-reviewer verdict and the decisions you made; go on when the owner approves, after
     the changes they ask for.
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
   - **Package approval** (DEC-111 (5)): a package that `web/approved-packages.json` does not list is a Needs
     decision item. It is approved by a pull request of its own when an official installer of an approved package
     adds it (`create-next-app` at the pinned version, `npm exec --no -- shadcn init` or `add` of an item of the
     official registry, the install command in the documentation of an approved package), or when it is an
     `@types/<x>` whose `<x>` is listed (DEC-111 (1)). When the owner answers yes:
     1. `git worktree add -b build/approve-<id in lowercase> out/worktrees/approve-<id in lowercase> origin/main`;
        there add each package to `web/approved-packages.json` with `DEC-111`, keeping the keys sorted, and commit
        that file alone as `build(web): approve packages for <ID>` (no parentheses, so that neither the rule
        coverage of the PR gates nor the routines take it for the feature), its body naming the packages, the Needs
        decision item and the owner's answer, `Refs: <ID>, DEC-111`; `git push -u origin build/approve-<id>`.
     2. Write the body of the pull request to `out/approve-<id>.md`, in English: the packages and which installer
        adds them, the Needs decision item with the owner's answer, and "Tóm tắt cho chủ dự án" (`CLAUDE.md`,
        Workflow). Give the owner the link
        `https://github.com/taitt0600/zengear/compare/main...build/approve-<id>?quick_pull=1&title=<title>&body=<body>`,
        both URL-encoded (documented query parameters of GitHub); when the link is long (browsers and GitHub cut
        long URLs; a limit of about 8000 characters is assumed, not checked), give
        `https://github.com/taitt0600/zengear/pull/new/build/approve-<id>` and the file to paste instead. Never open
        that pull request: it is an owner path, and the owner opens and merges it.
     3. Write `out/paused-<ID>.md` (the command that runs, its branch, the plan, the owner's answers, what is done so
        far, the approval branch) and stop. Say that the run goes on when the owner says the pull request merged, and
        that after a closed session the owner starts the same command again — `/implement-feature <ID>` (step 1),
        `/finish-feature <ID>` (its **Resume**) or `/fix-defect <ID> …` (its step 1) — which switches to the branch
        that file names and resumes at item 4.
     4. When the owner says it merged: `git fetch origin main`; `git show origin/main:web/approved-packages.json`
        lists the packages (otherwise say so and stop again); `git worktree remove out/worktrees/approve-<id>`,
        `git branch -D build/approve-<id>`, each skipped when `tools/git_tidy.ps1` already removed it; on the
        feature branch `git rebase --autostash origin/main`; delete `out/paused-<ID>.md` and go on where the run
        stopped.
     Any other package (one that no official installer adds) needs a Decision of its own: draft it with
     `/decision-draft`, and the run stops as for an answer that changes a specification. The icon library of an
     official block that `web/approved-packages.json` does not list (`@tabler/icons-react` of `dashboard-01`) is
     neither: it is not approved, and step 7 removes it after the `add` and switches the icons to lucide
     (DEC-116 (3), skill `shadcn`).
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
     green. The build refreshes `api/openapi/ZenGear.Api.json`; when it changed, also run the web checks of
     `/finish-feature` (step 2), since the CI job `web-test` fails on a stale `web/src/lib/api/schema.d.ts`.
6. **Postman folder**: skill `api-collection` with the Feature ID writes the folder of the feature in `postman/`
   (one request per acceptance criterion, a refusal per rule, tests on status, Problem Details `type` and fields,
   values unique to the run, dev-only fixtures for a state the API cannot reach yet) and runs it twice with skill
   `api-run`. A request that fails because the code is wrong is a failed test of step 5: fix the code first.
7. **Web**, following `web/CLAUDE.md` — only for the screens of this feature that `docs/SCREENS.md` describes and
   whose cell in the column *Blocked by* of its screen list is empty. The report names every other screen of the
   feature as not built, with what blocks it; a feature with no screen built is tested through the API:
   - From `web/`: `npm ci` as `/finish-feature` step 2 says, unless step 5 ran it (the branch is new; it counts as
     the first `npm ci` of the web checks), `npm run gen:api`;
     tests first, seen failing: Vitest (Zod schemas, the mapping of Problem Details
     onto the form, hooks; DEC-104 (7)); then pages and components in the matching `features/<area>/<module>`, and the
     Playwright flows of the screens in `web/e2e/`.
     **A `shadcn add`** (skill `shadcn`) runs before any file it may update is edited, `web/src/app/globals.css`
     included, and is committed alone before any file it wrote is edited (DEC-116 (1)):
     1. Its packages: each package it added to `web/package.json` is listed in `web/approved-packages.json`. If not,
        undo the add (delete the files it created; `git restore` the files it updated, `web/package.json` and
        `web/package-lock.json`; `npm ci` from `web/`) and take the package approval path (step 4); but the unlisted
        icon library of a block is removed with `npm uninstall <package>` from `web/` (skill `shadcn`).
     2. From the repository root: `git config user.name taitt0600`, `git config user.email taitt.dev@gmail.com`
        and `git config core.hooksPath .githooks`, as `/finish-feature` step 9 does; `git add --` exactly the files
        `add` lists as created or updated (it prints them relative to `web/`), `web/src/app/globals.css` when it
        reports CSS variables, and `web/package.json` and `web/package-lock.json` when they changed; never
        `git add -A` or `.` (the tree holds the tests and code not committed yet); check
        `git diff --cached --name-only`.
     3. Commit `feat(web): add the shadcn <items> as generated` with a body and `Refs: <ID>, DEC-116` plus the
        Decision that approved each package it adds (a commit that adds a package names an accepted Decision,
        `CLAUDE.md`, Workflow). The edits of those files come in later commits. In a web-only run, which skips step 5, **Checkpoint 2** comes
     here (`--checkpoints` only): the failing Vitest tests, each with the reason it fails.
   - The web checks of `/finish-feature` (step 2), under its **Output** rule. The flows run in `/finish-feature`
     (step 7), with the API, the emulator and the web running.
8. **Handoff**, once the steps of 5–7 that ran are green: write `out/handoff-<ID>.md` (ignored by git) with the
   branch, whether the run is web-only (and then its pull request title), the plan-reviewer verdict and its counts
   of `[over-engineering]` and `[duplication]` findings, the Needs decision items with the owner's answers (and
   those left open), how the plan handled each Work state row (step 3), the decisions made, the deviations from the
   approved plan, the results of the build, the tests, the Postman runs and each web check, whether `--checkpoints`
   was given, and, while the trial of the plugin `security-guidance` runs (item (3) of the
   Waiting row of `docs/STATUS.md` on DEC-112), its warnings and review findings so far, each with whether it was
   acted on. Then stop: tell the owner to type `/clear`, then `/finish-feature <ID>`
   (with `--checkpoints` when it was given), which verifies, commits, opens the pull request, follows CI, has the
   owner test the feature before merge and reports. End with the section "Tóm tắt cho chủ dự án".
