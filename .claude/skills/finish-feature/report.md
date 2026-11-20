# Report (step 14 of `/finish-feature`)

The steps named below are those of `.claude/skills/finish-feature/SKILL.md` and of
[test-before-merge.md](test-before-merge.md) (step 13); **Handoff** is a paragraph of `SKILL.md`.

14. **Report** in Vietnamese, once, after the owner's answer at step 13 (before auto-merge on **Đạt**), or when the
    run stops earlier: write it to
    `out/report.md` (first line `<!-- finish-feature -->`), have it reviewed (below), post it with
    `python3 tools/github_pr.py comment <number> --body-file out/report.md` when there is a pull request, and end
    your last reply with the same text (without that first line) followed by the section
    "Tóm tắt cho chủ dự án" (`CLAUDE.md`, Workflow), which is not posted in the report comment. Sections:
    - **Kết quả**: "Đạt — auto-merge bật" (**Đạt**), waiting for the owner's test (**Để sau**), draft waiting
      for a decision, or stopped at the ready check — with the pull request link; then the six lines
      `Nơi chạy: local`, `Thời gian bạn tham gia: <the owner's answer at step 13> phút` (`?` when the run
      stopped before step 13), `Phát hiện over-engineering / duplication: <a> / <b>` (<a>: the labelled findings of
      plan-reviewer, from the **Handoff**, plus those of code-reviewer at step 5; a `/fix-defect` run, or a run
      without the handoff file, counts code-reviewer alone; <b>: the percentage of duplicated lines in the line
      `Total:` of `web/node_modules/.bin/jscpd --config .jscpd.json` at the head, as the job quality-web counts it,
      DEC-152 (8), with "(series break)" after it on the first such line) and
      `Folder mỗi commit feat: <n1>, <n2>, …` (the last line of `python3 tools/structure_metrics.py --range
      origin/main..HEAD`); when the run stopped before the code review (`/finish-feature` step 5), these two lines
      are `Phát hiện over-engineering / duplication: ? / ?` and `Folder mỗi commit feat: ?` (no review or commit
      yet); and the two lines printed by `python3 tools/token_usage.py --branch <branch> --line`, `Token: <total>
      (chính <main> / agent <sub>; output <out>, ghi mới <fresh>), <calls> lần gọi, context lớn nhất <peak>` and
      `Sessions: <id>|<id>…`, the session ids of the run, the join key with the OpenTelemetry events of DEC-152 (8)
      (`Token: ?` and `Sessions: ?` when the command fails). The routine "ZenGear PR merged" copies the first five
      lines to issue #24 (DEC-106; the Token line for the evaluation of DEC-112 (8), `docs/AUTOMATION.md`,
      *Metrics*); the line `Sessions:` stays in the report comment, which keeps the ids after Claude Code deletes
      old transcripts.
    - **Chức năng**: what a user can now do, in the words of the user story.
    - **Cách chạy để kiểm thử**: the steps of the guide (step 13.2), appendix included.
    - **Kịch bản kiểm thử**: when the API changed, one line per request of the folder: the criterion or rule it
      checks, the account, the expected result (status, Problem Details `type`) and the result of the last run; when
      the branch changes `web/`, then a sub-list **Luồng màn hình (Playwright)**, one line per flow of step 7 with its
      result (kept apart, since the routine "ZenGear PR merged" counts the Postman lines); a web-only run says that
      the rules of the feature are covered by its earlier pull request. Then the manual checks of the guide. A defect
      repeats the steps that showed it, with the result now expected.
    - **Rule đã có test**, **Quyết định Claude đã tự chọn** (each stopgap named temporary, with its long-term fix
      and its Task issue), **Quyết định của bạn** (the owner's answers), **Cần bạn quyết định** (each item still
      open with its options and their consequences, each option with the four qualities and its cost), **Đã kiểm tra** (steps
      2–8, the web checks and the flows of step 7 included when the branch changes `web/`, CI and the owner's
      test, with the numbers kept above, "Phát hiện riêng" and the labelled findings of each reviewer included;
      while the trial of the plugin `security-guidance` runs (Task issue
      #262 on DEC-112), also the line
      `security-guidance: <a> cảnh báo / <b> phát hiện riêng`: its warnings on edits and its review findings at the
      end of a turn and on commit or push, those of the **Handoff** added to this session's, and how many no other
      reviewer found); when the branch changes `web/`, also the line
      `Bố cục (DEC-119): <a> vòng Có lỗi / <b> phát hiện review do bố cục`: `<a>` the **Có lỗi** rounds caused by
      the layout of a screen (`?` before the owner's answer at step 13), `<b>` the findings labelled `[layout]` of
      plan-reviewer (from the **Handoff**) and code-reviewer, and those of `/code-review` about the layout of a
      screen (`docs/AUTOMATION.md`, *Metrics*), and
      for each screen whether its design was read or only its Layout tree, or "không có Layout" (a screen built
      before DEC-119); a run without a plan (`/fix-defect`, or no handoff file) counts no plan-reviewer findings and
      says so, as the over-engineering line does.
    Before posting, give the `report-reviewer` agent `out/report.md`, `out/feature-<ID>.md` and the facts it reads:
    the pull request number and state, the check runs of its head with their results, the areas the branch changes
    (as in 13.3), the plan-reviewer and code-reviewer verdicts, whether the security review ran, the Postman runs, the
    web checks and the flows with their results and the owner's answer. Fix every Blocking and Should fix item:
    correct the report, or, when an item shows a check that was not made (a criterion without a request, a rule
    without a refusal), add it with skill `api-collection`, run it with
    `api-run` and fix what fails, running steps 2–3 again after a code change; at most two rounds. Add its final
    verdict to **Đã kiểm tra**.
