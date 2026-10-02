---
description: Fix a defect found in acceptance testing, with checkpoints locally or unattended in the cloud (failing test → fix → pull request)
argument-hint: <Feature ID> <what is wrong, or #issue>
---
Fix this defect of ZenGear (CLAUDE.md, Workflow; DEC-099): $ARGUMENTS

Locally, stop at the checkpoint of step 3 and at the one of `/finish-feature` (step 9); in a cloud session (the
routine "ZenGear fix defects") never stop.

1. **Ready check** as in `/implement-feature` (step 1), on a branch `fix/<id in lowercase>-<short name>` locally.
   With `#<issue>`, read it with `gh issue view <number>` (locally) or the GitHub tools of the session.
2. **Understand**: the feature dossier (`python3 tools/feature_context.py <Feature ID> --out
   out/feature-<Feature ID>.md`, skill `feature-context`) and the code and tests the defect goes through. Decide whether the code breaks the specification, or the specification itself is
   wrong or silent. The second is a Needs decision item (policy of `/implement-feature`, step 4): stop and report;
   locally, ask the owner first and go on with the answer when it does not change a specification.
3. **Reproduce** with a test that fails for the reason described: named after the rule it breaks
   (`R_USR_01_...`), otherwise after the behavior, in the project `/implement-feature` (step 5) gives for that kind
   of test. Run it and see it fail. **Checkpoint (local)**: show the failing test and why it reproduces the
   defect; go on when the owner agrees.
4. **Fix** the code, the smallest change that makes the test pass without weakening another test, then
   `dotnet build ZenGear.slnx`, `dotnet test ZenGear.slnx` and the drift check from `api/`.
5. **Finish** with the steps 1–12 of `/finish-feature` for this feature, with these differences: one commit
   `fix(<scope>): <subject>` with `Refs:` (and `Closes #<issue>`); pull request title
   `fix(<scope>): <subject> (<Feature ID>)`; in the report, **Kịch bản kiểm thử** repeats the steps that showed the
   defect, with the result now expected.
