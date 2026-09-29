---
description: Check that the documentation agrees with itself, the code and CI (report only)
---
Check that ZenGear's documentation is consistent. Report only; change nothing.

1. **Commands**: every command in `CLAUDE.md`, `api/CLAUDE.md`, `web/CLAUDE.md` and `.claude/commands/*.md` matches how `.github/workflows/*.yml` runs it (solution name, `-p`/`-s` for `dotnet ef`, working directory).
2. **API**: every Problem Details `type` and status code the code returns (`/problems/…` in `api/src`) is listed in `docs/API_CONVENTIONS.md`, and every endpoint path follows its URL rules.
3. **Names**: every entity class under `api/src/ZenGear.Data/Entities` has a row in `docs/GLOSSARY.md` with the same class, table and area; every area folder in the three projects is an area of `docs/ARCHITECTURE.md`.
4. **Schema**: every table created by the migrations exists in `docs/DATABASE.md` (`#### <table>`) and in `api/db/`, with the same constraint and index names.
5. **Blueprint**: `python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx <temp dir>` produces the same files as `docs/blueprint/` (the Markdown was not edited by hand).
6. **Rules in code**: every `R_…` test name refers to a rule that exists in `docs/blueprint/business-rules.md` or `docs/blueprint/technical-design.md`.

Report a table: check, result (OK / MISMATCH), file and line, what differs. End with the list of files that need a change and which of them are specifications (these need a Decision before they change).
