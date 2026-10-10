---
description: Verify a finished feature, commit it, open the pull request, follow CI, have the owner test it before merge and report
argument-hint: <Feature ID, e.g. AUT-04> [--checkpoints]
---
Finish the feature named in $ARGUMENTS of ZenGear (CLAUDE.md, Workflow; DEC-100). It runs on the owner's machine and
stops only to ask a Needs decision item, for the owner's test before merge (step 13), and, with `--checkpoints` in
$ARGUMENTS, at **Checkpoint 3** (step 9). When a step fails, fix it and run the steps it depends on again; what
cannot be fixed under the policy of `/implement-feature` (step 4) becomes a Needs decision item, asked at once.
A command, skill or agent run inside a step (`/code-review`, `/code-review:code-review`, `/security-review`,
`api-run`, `api-collection`, the reviewer agents) returns its report to this run: keep its numbers for the report of
step 14 and go on with the run (the fixes of that step, or the next step) in the same turn. It never ends the run;
only the stops above do.
`python3` is `python` on Windows.

**Resume**: when the branch already has an open pull request whose CI is green and nothing changed since the guide
was posted (the owner answered **Để sau** earlier), go straight to step 13: run skill `api-run` again first (the
folder, then the whole collection, leaving the API running), then show the guide and ask again.

1. **Branch**: `git branch --show-current` is not `main`. `git status` lists what will be committed; nothing under
   `bin/`, `obj/`, `out/`, secrets or `.env` files.
2. **Build and test** (from `api/`): `dotnet build ZenGear.slnx`, then `dotnet test ZenGear.slnx`. The build refreshes
   `api/openapi/ZenGear.Api.json`; it belongs to the commit that changes the API. The schema check against `api/db/`
   is not run here: the `schema` job of CI runs it.
3. **Drift check** (from `api/`): `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api`
   reports no changes.
4. **Specification**: `python3 tools/pr_gates.py coverage <ID>` exits 0. If `api/db/`, `docs/DATABASE.md` or
   `docs/blueprint/` changed, a commit must name the accepted Decision that allows it; otherwise it is a Needs
   decision item and the change is reverted.
5. **Code review**, two reviewers on the same diff (`git diff origin/main...HEAD` plus the working tree):
   - the built-in `/code-review` on the diff;
   - the `code-reviewer` agent, given the feature ID, the branch and `out/feature-<ID>.md` (write it first with
     `python3 tools/feature_context.py <ID> --out out/feature-<ID>.md` when it is missing).
   Fix every Blocking and Should fix item of the agent and every finding of `/code-review` you confirm, then run
   steps 2–3 and both reviews again, at most three rounds; an item you do not fix gets its reason in the report, and
   a Blocking item left after three rounds is a Needs decision item. Keep the final verdict, the number of Blocking
   items found and fixed, for each reviewer the number of findings that only it made ("Phát hiện riêng" in the
   report): after three features, the reviewer with none is dropped (DEC-100), and the number of the agent's
   findings labelled `[over-engineering]` and `[duplication]` over all rounds (DEC-106). When each review returns its report,
   keep its numbers and go on at once (the other review, the fixes, the next round or step 6); it does not end the
   run.
6. **Security review**: run `/security-review` when the branch touches authentication or authorization
   (`ZenGear.Api/Authorization`, `Attributes`, `Middlewares`, `Extensions/AuthenticationSetup.cs`, the `Identity`
   area), payments, invoicing, a column listed under Personal data in `docs/DATABASE.md`, raw SQL, file uploads or
   an `[AllowAnonymous]` endpoint; otherwise skip it and say so. Fix every finding, or give the reason it is not a
   vulnerability; after a fix, run steps 2–3 again. Keep the number of findings and of fixed findings. When
   `/security-review` returns its report, keep its numbers and go on at once (the fixes or step 7); its report is not
   the last reply of this run.
7. **Postman run**: skill `api-run` with the Feature ID (the folder of the feature), then without one (the whole
   collection, for regressions). A failed test is a failed step: fix the code (or the request, when the request is
   wrong against the specification) and run steps 2–3 and this step again. Keep the requests run, the tests passed and
   failed of both runs.
8. **Analyzer rules**: when a build of this run was red for an analyzer rule (`S…`, `CA…`) that the section
   "Analyzer rules that often fail the build" of `api/CLAUDE.md` does not list yet, add one line for it there, in the
   style of the existing lines (the rule, why it fails, how to write the code instead), committed with the docs.
9. **Checkpoint 3** (`--checkpoints` only): show the owner `git diff --stat origin/main`, the results of steps 2–8
   and the verdicts of the reviewers, and commit only when they approve, after the changes they ask for and steps
   2–3 again.
   **Commits**, per the `Commits` section of `CLAUDE.md`, without asking: first `git config user.name taitt0600`,
   `git config user.email taitt.dev@gmail.com` and `git config core.hooksPath .githooks`. Then `feat(db)` for
   entities, configurations and migrations; `feat(api)` for behavior, endpoints, the OpenAPI document and their
   tests; `test` for the Postman folder, its environments and fixtures; `test(api)` or `docs` for the rest — each with
   a body and a `Refs:` footer, plus `Closes #<issue>` for issues it resolves, and no `Co-Authored-By` or
   `Claude-Session` trailer. Never `--no-verify`, never amend a pushed commit.
10. **Pull request**:
    - `python3 tools/pr_gates.py check origin/main HEAD "<title>"` runs the PR gates as CI will; fix what it reports.
      When it reports files the owner merges, push the branch but do not open the pull request: the report gives the
      owner the link `https://github.com/taitt0600/zengear/pull/new/<branch>` to open it.
    - `git push -u origin HEAD`.
    - Write the body to `out/pr-body.md` (ignored by git), in English: summary, rules covered, decisions made (yours,
      and the owner's answers), a section "Needs decision" with each item still open, its options and the recommended
      one first, how it was verified (steps 2–8, the plan and code-reviewer verdicts with the Blocking items found and
      fixed, the security review findings or why it was skipped, the Postman runs). The body ends with the section "Tóm tắt cho chủ dự án" in Vietnamese
      (`CLAUDE.md`, Workflow); its part "Bạn cần quyết định" points to the section "Needs decision" instead of
      repeating it, since the routine "ZenGear PR needs decision" reads that section.
    - `python3 tools/github_pr.py create --title "<title>" --body-file out/pr-body.md`: ready, or with `--draft` only
      while a Needs decision item is open; once the owner answers it, apply the answer, push, and run the command
      again without `--draft` to make it ready. Title: the format of a commit subject with the feature ID at the end,
      e.g. `feat(api): add role-based access control (AUT-04)` — the PR gates check the rules of that feature.
    - Do not enable auto-merge: it waits for the owner's test (step 13).
11. **CI**: `python3 tools/github_pr.py checks <number> --wait 540` with a Bash timeout of 10 minutes; exit code 3
    means still running, so run it again, at most six times. Exit code 0: every check passed. Exit code 4: GitHub was
    not reachable; run it again. Exit code 1:
    - A failed check: read its log (`gh run view <run id> --log-failed`) and its annotations; reproduce it with the
      commands of its job in `.github/workflows/`, fix it, run steps 2–3, commit the fix as its own commit typed by
      what it changes (`fix` only for a bug already on `main`; never amend), `git push origin HEAD`, and wait again.
    - A conflict with `main`, or the branch behind it: `git fetch origin main`, `git rebase origin/main`, resolve,
      run steps 2–3, `git push --force-with-lease origin HEAD`, and wait again.
    - At most three rounds of fixes; then the failure is a Needs decision item, asked at once.
12. **Review of the pull request**: run the review of the `code-review` plugin on the pull request
    (`/code-review:code-review <number>`). Fix each finding you confirm (steps 2–3, commit, push, step 11 again) or
    answer it on the pull request with the reason it stays; keep the number of findings, fixed and answered. When
    the review returns its findings, keep its numbers and go on at once (the fixes or step 13); it does not end the
    run.
13. **Test before merge** — the owner tests the feature as an end user, from the guide alone:
    1. **Environment ready**: skill `api-run` with the Feature ID, leaving the API of the branch running in the
       background until the owner answers (the reset, then the API from the branch). The branch stays checked out:
       the Postman app follows the branch of the repository folder.
    2. **Guide** `out/test-guide-<ID>.md`, in Vietnamese, one page at most:
       - **Các bước** (the environment is already running): open Postman on the repository folder; switch to the
         branch `<branch>`; choose environment `dev`; run the folder `<ID>` (Run folder); every test is green.
       - **Kiểm tra tay**: at most three checks that the collection cannot make (a command, a screen, a value in the
         Emulator UI or the database), each with what to do, what to look at and the expected result.
       - When the feature has a web: the Next.js screens to open, each with a Playwright screenshot of the expected
         result.
       - **Phụ lục — Nếu môi trường chưa chạy**: `git switch <branch>`; `pwsh -NoProfile -File tools/dev_reset.ps1`;
         `dotnet run --project api/src/ZenGear.Api --launch-profile http`.
       A defect (from `/fix-defect`) gives the steps that showed the defect, with the result now expected.
    3. Give the guide, `out/feature-<ID>.md` and the facts (the pull request number and state, the check runs of its
       head, the Postman runs of step 7 and of 13.1) to the `report-reviewer` agent; fix every Blocking and Should fix
       item, at most two rounds. Post it with `python3 tools/github_pr.py comment <number> --body-file
       out/test-guide-<ID>.md` and show it to the owner.
    4. Ask with AskUserQuestion: the result — **Đạt** / **Có lỗi** / **Để sau** — and the minutes spent on this
       feature so far — ≤ 10, 10–20, 20–40, more than 40.
       - **Đạt**: first write, review and post the report of step 14, with **Kết quả** "Đạt — auto-merge bật" and
         the four lines of **Kết quả** (step 14): the routine "ZenGear PR merged" starts on the merge event and
         reads the report at once, so it must be on the pull request before anything can merge. Then
         `python3 tools/github_pr.py auto-merge <number>` (not while the pull request is a draft). Exit code 5
         means the owner merges it (it changes a file only the owner merges, or `main` does not enforce every
         required check): say so, and add a one-line comment on the pull request that auto-merge stays off and the
         owner merges it.
       - **Có lỗi**: for each defect the owner describes, write first a test that reproduces it (a unit or
         integration test named after the rule, and a request in the folder through skill `api-collection`) and see
         it fail; fix it on the same branch; steps 2–3; commit, typed by what it changes (`fix` is only for a bug
         already on `main`); `git push origin HEAD`; step 11; then 13.1 to 13.4 again, with the guide sent again.
       - **Để sau**: stop here. The pull request stays open without auto-merge; a later `/finish-feature <ID>` on
         the same branch resumes at this step (see **Resume** above).
       Stop the API of 13.1 once the owner has answered, except after **Để sau**, where it may keep running.
14. **Report** in Vietnamese, once, after the owner's answer at step 13 (before auto-merge on **Đạt**), or when the
    run stops earlier: write it to
    `out/report.md`, have it reviewed (below), post it with `python3 tools/github_pr.py comment <number> --body-file
    out/report.md` when there is a pull request, and end your last reply with the same text followed by the section
    "Tóm tắt cho chủ dự án" (`CLAUDE.md`, Workflow), which is not posted in the report comment. Sections:
    - **Kết quả**: "Đạt — auto-merge bật" (**Đạt**), waiting for the owner's test (**Để sau**), draft waiting
      for a decision, or stopped at the ready check — with the pull request link; then the four lines
      `Nơi chạy: local`, `Thời gian bạn tham gia: <the owner's answer at step 13> phút` (`?` when the run
      stopped before step 13), `Phát hiện over-engineering / duplication: <a> / <b>` (the labelled findings of
      plan-reviewer, passed on by `/implement-feature`, plus those of code-reviewer at step 5; a `/fix-defect` run
      has no plan and counts code-reviewer alone) and
      `Folder mỗi commit feat: <n1>, <n2>, …` (the last line of `python3 tools/structure_metrics.py --range
      origin/main..HEAD`), which the routine "ZenGear PR merged" copies to issue #24 (DEC-106).
    - **Chức năng**: what a user can now do, in the words of the user story.
    - **Cách chạy để kiểm thử**: the steps of the guide (step 13.2), appendix included.
    - **Kịch bản kiểm thử**: one line per request of the folder: the criterion or rule it checks, the account, the
      expected result (status, Problem Details `type`) and the result of the last run; then the manual checks of the
      guide. A defect repeats the steps that showed it, with the result now expected.
    - **Rule đã có test**, **Quyết định Claude đã tự chọn**, **Quyết định của bạn** (the owner's answers),
      **Cần bạn quyết định** (each item still open with its options and their consequences), **Đã kiểm tra** (steps
      2–8, CI, the review of step 12 and the owner's test, with the numbers kept above, "Phát hiện riêng" and the
      labelled findings of each reviewer included).
    Before posting, give the `report-reviewer` agent `out/report.md`, `out/feature-<ID>.md` and the facts it reads:
    the pull request number and state, the check runs of its head with their results, the plan-reviewer and
    code-reviewer verdicts, whether the security review ran, the Postman runs with their results and the owner's
    answer. Fix every Blocking and Should fix item: correct the report, or, when an item shows a check that was not
    made (a criterion without a request, a rule without a refusal), add it with skill `api-collection`, run it with
    `api-run` and fix what fails, running steps 2–3 again after a code change; at most two rounds. Add its final
    verdict to **Đã kiểm tra**.
