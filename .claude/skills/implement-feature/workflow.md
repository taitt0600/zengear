# Workflow procedure

The procedure behind the rules of *Workflow* in the root `CLAUDE.md`: how the run skills (`/implement-feature`,
`/finish-feature`, `/fix-defect`, `/design-screen`) follow one another, how branches are named and how the guard of
`.claude/hooks/guard-spec.ps1` judges a command. The rules themselves stay in `CLAUDE.md` (the first sentence of
*Branches* repeats the rule of one branch per issue so that the name forms read whole; a change to it is made in both
files); the review rounds are in `.claude/skills/finish-feature/reviewers.md`.

## Runs

`/implement-feature <ID>` checks that it can start, plans, writes the tests first, then the code and the feature's
Postman folder, writes `out/handoff-<ID>.md` and stops; the owner types `/clear`, then `/finish-feature <ID>` verifies,
reviews, commits, opens the pull request without auto-merge, follows CI and has the owner test the feature before
merge (a test guide of one page, then **Đạt**, **Có lỗi** or **Để sau**), and reports once after that answer. A
feature already done whose screens are no longer blocked and have an approved design runs `/implement-feature <ID>`
again as a web-only run on `feat/<id>-web`. The steps are in `.claude/skills/<name>/SKILL.md` and its reference files;
these skills have `disable-model-invocation`, so a step of one cited by another (`/finish-feature` step 13) is read
from its file and followed, never invoked.

## Branches

One branch per issue from `main`, named by `python tools/branch_name.py make` from the issue's number, Issue type,
labels and Feature ID (DEC-148 (10)): `feat/<feature id>` for a Feature (`feat/aut-04-web` for a web-only run, with
`-2`, … when that name has merged already), `fix/<feature id>-<issue>` for a Bug, `<type>/<issue>-<slug>` for a Task
(its one type label) or a Design (`docs`); a fix goes on the feature's branch while its pull request is open, except a
defect of the API while that is the pull request of a web-only run; nothing is committed on `main`. GitHub deletes the
branch of a merged pull request, and the first step of `/implement-feature` and `/fix-defect` runs
`tools/git_tidy.ps1 -Apply`, which deletes its local copy (DEC-120 (2)).

## Guard

The generated web client `web/src/lib/api/generated/` (DEC-122 (4)) is asked about too when edited by hand: it changes
only through `npm run gen:api`, which the guard lets run; `web/src/components/ui/` (DEC-116 (1)) and the mutator
`web/src/lib/api/api.ts` are code of the app. The guard judges each part of a shell command (split at `;`, `&&`, `||`,
`|`), so a read-only command is not taken for a write because another part of it runs `pwsh` or `python`. The hooks
run on PowerShell 7 (`pwsh`) on Windows and in the cloud.
