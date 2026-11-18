# Decision policy (step 4 of `/implement-feature`)

The policy for every question the specification does not answer. `/finish-feature`, `/fix-defect`, `plan-reviewer`
and `report-reviewer` cite it as "`/implement-feature` step 4"; the steps named below are those of
`.claude/skills/implement-feature/SKILL.md`.

- **Long-term and safe** (DEC-139): every choice of this policy, a reversible one and the fix of a review
  finding, a failed check or a defect included, is the long-term, safe one, never a shortcut (skipping a rule, a
  check or a part of the specification) or a temporary patch. An urgent stopgap (a broken `main`, a
  vulnerability) follows `AGENTS.md`, *Recommendations, designs and requests*; besides, its Task issue (form
  `task.yml`, `python tools/issue.py create`) is opened in the same run and named in the stopgap's own commit,
  whose body gives the long-term fix; its trigger is still to come (an event in the field Trigger, or a blocked-by
  relation on the issue of a feature not done yet, never the feature of this pull request). The other skills
  point here.
- Decide it yourself, and list the choice under "Decisions made" in the report, when it is reversible, stays
  inside the code and agrees with the documents: names of private members, how a service is split within the
  limits of `docs/ARCHITECTURE.md`, the shape of a query, test data, the wording of a message that follows
  `docs/API_CONVENTIONS.md`.
- Make it a Needs decision item when the answer would change a specification (`docs/blueprint/`,
  `docs/DATABASE.md`, `api/db/`, `docs/ARCHITECTURE.md`); concerns money, prices, tax, invoices or accounting, a
  legal duty or personal data, the permissions the seed grants to roles, or data that is hard to migrate (a
  column, a stored enum value); or needs a new business term in `docs/GLOSSARY.md`, a new package, a build file
  that runs code, a new area, a mechanism folder, a technical folder of `ZenGear.Api` or `ZenGear.Data`, a folder in
  `Common`, a suppressed analyzer rule or an exception to an architecture test; or the layout of a screen departs
  from its approved design (a block added, removed or moved, what shows on a narrow screen, another component;
  DEC-119 (4)), whose only options are to follow the design, or to leave the screen unbuilt (a fix: stop) until
  a new Layout pull request (`/design-screen <SCR-ID>`) has merged, since a layout changes only through one
  (DEC-119 (3)). The other new folders of *Folder structure* in `docs/ARCHITECTURE.md` are yours to decide (a
  module of a listed area with its line in the area's `README.md`, and that area's `Controllers/`, `Entities/`,
  `Configurations/` and test folders): list them under "Decisions made". The same applies wherever `AGENTS.md`,
  `CLAUDE.md`, `api/AGENTS.md` or `web/AGENTS.md` says to ask or to stop and ask, and whenever a hook denies a
  change.
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
