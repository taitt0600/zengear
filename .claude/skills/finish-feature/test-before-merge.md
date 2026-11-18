# Test before merge (step 13 of `/finish-feature`)

The steps named below are those of `.claude/skills/finish-feature/SKILL.md`; **Owner's comments**, **Resume** and
**Changed areas** are its paragraphs, and the report is step 14 ([report.md](report.md)).

13. **Test before merge** — the owner tests the feature as an end user, from the guide alone. First read the
    **Owner's comments**, at every pass of this step.
    1. **Environment ready**: skill `api-run` with the Feature ID, leaving the API of the branch running in the
       background until the owner answers (the reset, then the API from the branch). The branch stays checked out:
       the Postman app follows the branch of the repository folder. When the branch changes `web/`, then start
       `npm run dev` in `web/` in the background, only when `http://localhost:3000` does not answer (a second one
       would take port 3001, which the CORS of the API refuses), and leave it running as the API is.
    2. **Guide** `out/test-guide-<ID>.md`, in Vietnamese, one page at most; its first line is
       `<!-- finish-feature -->` and its second `## Hướng dẫn kiểm thử <ID>`:
       - **Các bước** (the environment is already running), when the API changed (**Changed areas**, step 2):
         open Postman on the repository folder; check that it shows the branch `<branch>` (13.1 left it checked
         out); choose environment `dev`; run the folder `<ID>` (Run folder); every test is
         green. A branch that changes neither has no Postman step.
       - When the branch changes `web/`: the screens to open at `http://localhost:3000`, the account to sign in with
         (its password only by reference to `docs/ACCEPTANCE.md`, *Accounts*, never written in the guide), and for
         each screen the Playwright screenshot of the expected result from step 7: its path under
         `web/test-results/` on this machine, and the link of the `pipeline.yml` run of the head commit (from step
         11, or `gh run list --commit <head sha> --workflow pipeline.yml`), whose artifact `playwright-*` of the
         job `acceptance` holds the same screenshots. Beside each screenshot, the link of the screen's approved
         design (its **Layout** in `docs/screens/<SCR-ID>.md`) for the owner to compare (DEC-119 (5)); a screen
         without one says "chưa có bản vẽ (màn hình làm trước DEC-119)".
       - **Kiểm tra tay**: at most three checks in total, screens included, that the collection and the flows cannot
         make (a command, a screen, a value in the Emulator UI or the database), each with what to do, what to look
         at and the expected result.
       - **Phụ lục — Nếu môi trường chưa chạy**: `git switch <branch>`; `pwsh -NoProfile -File tools/dev_reset.ps1`;
         `dotnet run --project api/src/ZenGear.Api --launch-profile http`; when the branch changes `web/`, in `web/`
         `npm ci`, then `npm run dev`.
       A defect (from `/fix-defect`) gives the steps that showed the defect, with the result now expected.
    3. Give the guide, `out/feature-<ID>.md` and the facts (the pull request number and state, the check runs of its
       head, the changed areas of step 2 among `api/`, `postman/`, `web/` and `api/openapi/`, the Postman runs of
       step 7 and of 13.1, the web checks and the flows passed and failed) to the `report-reviewer` agent; fix
       every Blocking and Should fix item, at most two rounds. Read the **Owner's comments** once more, then post
       it with
       `python3 tools/github_pr.py comment <number> --body-file out/test-guide-<ID>.md` and show it to the owner.
    4. Ask with AskUserQuestion: the result — **Đạt** / **Có lỗi** / **Để sau** — and the minutes spent on this
       feature so far — ≤ 10, 10–20, 20–40, more than 40.
       - **Đạt**: first write, review and post the report of step 14, with **Kết quả** "Đạt — auto-merge bật" and
         the five lines of **Kết quả** (step 14): the routine "ZenGear PR merged" starts on the merge event and
         reads the report at once, so it must be on the pull request before anything can merge. Then
         `python3 tools/github_pr.py auto-merge <number>` (not while the pull request is a draft). Exit code 5
         means the owner merges it (it changes a file only the owner merges, or `main` does not enforce every
         required check): say so, and add a one-line comment on the pull request that auto-merge stays off and the
         owner merges it (`python3 tools/github_pr.py comment <number> --body-file out/automerge-note.md`, after
         the line `<!-- finish-feature -->`).
       - **Có lỗi**: for each defect the owner describes, write first a test that reproduces it (a unit or
         integration test named after the rule, and a request in the folder through skill `api-collection`; for a
         defect of a screen, a Vitest test or a Playwright flow in `web/e2e/`, run with `npm run e2e -- <spec>`
         while the API of 13.1 runs, and the request only when the API is involved) and see it fail; then stop the
         API (a running API locks the build output of steps 2–3; step 7 starts it again); fix it on the same
         branch; steps 2–3, and the screen flows of step 7 when the branch changes `web/` (they also renew the
         screenshots of the guide); commit, typed by what it changes (`fix` is only for a bug already on `main`);
         `git push origin HEAD`; step 11; then 13.1 to 13.4 again, with the guide sent again.
       - **Để sau**: stop here. The pull request stays open without auto-merge; a later `/finish-feature <ID>` on
         the same branch resumes at this step (see **Resume**).
       Stop the API of 13.1, and the web if 13.1 started it (not one the owner was running already), once the owner
       has answered (on **Có lỗi**, after the reproducing flow has failed), except after **Để sau**, where they may
       keep running.
