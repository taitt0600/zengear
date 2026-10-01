---
description: Verify a finished feature, commit it, open the pull request with auto-merge, follow CI and report
argument-hint: <Feature ID, e.g. AUT-04>
---
Finish feature $ARGUMENTS of ZenGear without waiting for anyone (CLAUDE.md, Workflow). When a step fails, fix it
and run the steps it depends on again; what cannot be fixed under the policy of `/implement-feature` (step 4)
becomes a Needs decision item. `python3` is `python` on Windows.

1. **Branch**: `git branch --show-current` is not `main`. `git status` lists what will be committed; nothing under
   `bin/`, `obj/`, `out/`, secrets or `.env` files.
2. **Build and test** (from `api/`): `dotnet build ZenGear.slnx`, then `dotnet test ZenGear.slnx`. The build refreshes
   `api/openapi/ZenGear.Api.json`; it belongs to the commit that changes the API.
3. **Drift check** (from `api/`): `dotnet ef migrations has-pending-model-changes -p src/ZenGear.Data -s src/ZenGear.Api`
   reports no changes.
4. **Schema check** — only when `api/src/ZenGear.Data/Migrations/` changed on this branch, and only in a cloud
   session: locally it would recreate the user's `zengear` database, and the `schema` job of CI runs it anyway.
   Run that job of `.github/workflows/api.yml` here: a `postgres:18` container named `zengear-schema` as in its
   `services`, its `env`, then its last three steps from `api/`; the last one,
   `python3 ../tools/schema_compare.py spec.txt migrated.txt`, prints the lines that differ for the tables the
   migrations build. Remove the container afterwards.
5. **Specification**: `python3 tools/pr_gates.py coverage $ARGUMENTS` exits 0. If `api/db/`, `docs/DATABASE.md` or
   `docs/blueprint/` changed, a commit must name the accepted Decision that allows it; otherwise it is a Needs
   decision item and the change is reverted.
6. **Code review**: give the `code-reviewer` agent the feature ID and the branch. Fix every Blocking and every
   Should fix item, then run steps 2–3 and the review again, at most three rounds; an item you do not fix gets its
   reason in the report, and a Blocking item left after three rounds is a Needs decision item. Keep the final
   verdict and the number of Blocking items found and fixed.
7. **Security review**: run `/security-review` when the branch touches authentication or authorization
   (`ZenGear.Api/Authorization`, `Attributes`, `Middlewares`, `Extensions/AuthenticationSetup.cs`, the `Identity`
   area), payments, invoicing, a column listed under Personal data in `docs/DATABASE.md`, raw SQL, file uploads or
   an `[AllowAnonymous]` endpoint; otherwise skip it and say so. Fix every finding, or give the reason it is not a
   vulnerability; after a fix, run steps 2–3 again. Keep the number of findings and of fixed findings.
8. **Commits**, per the `Commits` section of `CLAUDE.md`, without asking: first `git config user.name taitt0600`,
   `git config user.email taitt.dev@gmail.com` and `git config core.hooksPath .githooks`. Then `feat(db)` for
   entities, configurations and migrations; `feat(api)` for behavior, endpoints, the OpenAPI document and their
   tests; `test(api)` or `docs` for the rest — each with a body and a `Refs:` footer, plus `Closes #<issue>` for
   issues it resolves, and no `Co-Authored-By` or `Claude-Session` trailer. Never `--no-verify`, never amend a
   pushed commit.
9. **Pull request**:
   - `git push -u origin HEAD`.
   - Write the body to `out/pr-body.md` (ignored by git), in English: summary, rules covered, decisions made,
     Needs decision items, how it was verified (steps 2–5, the plan and code-reviewer verdicts with the Blocking
     items found and fixed, the security review findings or why it was skipped).
   - `python3 tools/github_pr.py create --title "<title>" --body-file out/pr-body.md`, with `--draft` when there
     is a Needs decision item or a step still fails; run again without `--draft` once both are gone, which makes
     the pull request ready. Title: the format of a commit subject with the feature ID at the end, e.g.
     `feat(api): add role-based access control (AUT-04)` — the PR gates check the rules of that feature.
   - Not a draft: `python3 tools/github_pr.py auto-merge <number>`. Exit code 5 means the owner merges: the pull
     request changes a file only the owner merges, or `main` does not enforce every required check. Leave
     auto-merge off and say so in the report.
10. **CI**: `python3 tools/github_pr.py checks <number> --wait 540` with a Bash timeout of 10 minutes; exit code 3
    means still running (or auto-merge has not merged yet), so run it again, at most six times. Exit code 0: the
    pull request is merged, or open with every check passed for the owner to merge. Exit code 4: GitHub was not
    reachable; run it again. Exit code 1:
    - A failed check: read its annotations, reproduce it with the commands of its job in `.github/workflows/`
      (job logs are not reachable from a cloud session), fix it, run steps 2–3, commit the fix as its own commit
      typed by what it changes (`fix` only for a bug already on `main`; never amend), `git push origin HEAD`, and
      wait again.
    - A conflict with `main`, or the branch behind it: `git fetch origin main`, `git rebase origin/main`, resolve,
      run steps 2–3, `git push --force-with-lease origin HEAD`, and wait again.
    - At most three rounds of fixes; then `python3 tools/github_pr.py auto-merge <number> --off` and make the
      failure a Needs decision item.
11. **Report** for acceptance, in Vietnamese: write it to `out/report.md`, post it with
    `python3 tools/github_pr.py comment <number> --body-file out/report.md` when there is a pull request, and end
    your last reply with the same text. Sections:
    - **Kết quả**: merged into `main`, open with auto-merge, draft waiting for a decision, or stopped at the ready
      check — with the pull request link.
    - **Chức năng**: what a user can now do, in the words of the user story.
    - **Cách chạy để kiểm thử**: the steps of `docs/ACCEPTANCE.md` when it exists; otherwise the commands of
      `api/CLAUDE.md` (Local database) to start PostgreSQL and Redis, apply the migrations and seeds and run the API,
      and the Scalar page of the API.
    - **Kịch bản kiểm thử**: for every acceptance criterion of the feature, the account or role to use, the actions
      (screen, or API call with method, path and a sample body) and the expected result, including at least one
      refusal per rule.
    - **Rule đã có test**, **Quyết định Claude đã tự chọn**, **Cần bạn quyết định** (each Needs decision item with
      the options and their consequences), **Đã kiểm tra** (steps 2–7 and CI, with the numbers kept above).
