---
name: check-docs
description: Check that the documentation agrees with itself, the code and CI (report only)
disable-model-invocation: true
---
Check that ZenGear's documentation is consistent. Report only; change nothing.

1. **Commands**: every command in `AGENTS.md`, `CLAUDE.md`, `api/AGENTS.md`, `web/AGENTS.md` and `.claude/skills/*/SKILL.md` (with the reference files beside them) matches how `.github/workflows/*.yml` runs it (solution name, `-p`/`-s` for `dotnet ef`, working directory).
2. **API**: every Problem Details `type` and status code the code returns (`/problems/…` in `api/src`) is listed in `docs/API_CONVENTIONS.md`, and every endpoint path follows its URL rules.
3. **Names**: every entity class under `api/src/ZenGear.Data/Entities` has a row in `docs/GLOSSARY.md` with the same class, table and area; every area folder in the three projects is an area of `docs/ARCHITECTURE.md`.
4. **Schema**: every table created by the migrations exists in `docs/DATABASE.md` (`#### <table>`) and in `api/db/`, with the same constraint and index names.
5. **Blueprint**: `python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx out/blueprint-export` produces the same files as `docs/blueprint/` (the Markdown was not edited by hand).
6. **Rules in code**: every `R_…` test name refers to a rule that exists in `docs/blueprint/business-rules.md` or `docs/blueprint/technical-design.md`.
7. **Feature issues** (DEC-148 (2), (6)): with `python tools/issue.py next` and `gh issue list --state all --limit 500 --json number,title,state`, every Phase 1 feature of `docs/blueprint/features.md` that has no merged `feat` pull request titled `… (<Feature ID>)` has exactly one open issue titled `<Feature ID> …` (`python tools/issue.py feature <ID>`, which also checks the field Feature ID); no open Feature issue names a feature that is done (except an issue `<Feature ID> web`) or an ID missing from the blueprint; no Feature ID has two open issues.
8. **Priority High** (DEC-148 (3), DEC-151 (3)): an open issue has Priority High exactly when it blocks a sub-issue of the Roadmap, of any Issue type: `python tools/issue.py roadmap` reads the blocked-by relations of every open sub-issue and lists each issue that differs; report them. When GitHub cannot be read, report checks 7 and 8 as not run, never as OK.

Report a table: check, result (OK / MISMATCH), file and line, what differs. End with the list of files that need a change and which of them are specifications (these need a Decision before they change).
