---
description: Fix a defect found in acceptance testing, unattended (failing test → fix → pull request)
argument-hint: <Feature ID> <what is wrong, or #issue>
---
Fix this defect of ZenGear without waiting for anyone (CLAUDE.md, Workflow): $ARGUMENTS

1. **Ready check** as in `/implement-feature` (step 1), on a branch `fix/<id in lowercase>-<short name>` locally.
   With `#<issue>`, read it with `gh issue view <number>` (locally) or the GitHub tools of the session.
2. **Understand**: the row of the feature in `docs/blueprint/features.md`, the rules it lists and the code and tests
   the defect goes through. Decide whether the code breaks the specification, or the specification itself is
   wrong or silent. The second is a Needs decision item (policy of `/implement-feature`, step 4): stop and report.
3. **Reproduce** with a test that fails for the reason described: named after the rule it breaks
   (`R_USR_01_...`), otherwise after the behavior, in the project `/implement-feature` (step 5) gives for that kind
   of test. Run it and see it fail.
4. **Fix** the code, the smallest change that makes the test pass without weakening another test, then
   `dotnet build ZenGear.slnx`, `dotnet test ZenGear.slnx` and the drift check from `api/`.
5. **Finish** with the steps 1–11 of `/finish-feature` for this feature, with these differences: one commit
   `fix(<scope>): <subject>` with `Refs:` (and `Closes #<issue>`); pull request title
   `fix(<scope>): <subject> (<Feature ID>)`; in the report, **Kịch bản kiểm thử** repeats the steps that showed the
   defect, with the result now expected.
