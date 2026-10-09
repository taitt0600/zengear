---
name: decision-draft
description: Draft a ZenGear Decision (DEC-xxx) for the Decisions sheet of docs/product-domain-blueprint-MVP.xlsx, with the feature and rule rows it changes, a blueprint patch the owner applies with tools/blueprint_apply.py, the commit message and the owner's numbered steps, once the owner has settled a proposal, an analysis or a "Cần bạn quyết định" item. Drafts only. Use when the user asks to draft or record a decision.
argument-hint: <what was decided, or the question to settle>
allowed-tools: Read, Grep, Glob, Edit(out/*.patch.json), Bash(grep *), Bash(git log *), Bash(python3 tools/feature_context.py *), Bash(python tools/feature_context.py *), mcp__github__list_pull_requests
---
Draft a Decision for: $ARGUMENTS

Drafts only: change no tracked file and commit nothing; the one file written is the untracked patch of step 6
(`out/` is git-ignored). The specification changes only in the workbook, by the owner (CLAUDE.md, Rules for every
change), who applies the patch with `tools/blueprint_apply.py` in their own terminal; never run that tool. Answer
in Vietnamese; every cell of the row is in Vietnamese, in the style of the rows of `docs/blueprint/decisions.md`.

1. **Next ID**: one above the highest of
   `grep -oE '^\| DEC-[0-9]{3}' docs/blueprint/decisions.md | sort | tail -1` and of every DEC-xxx named in the title
   of an open pull request (GitHub tools: `list_pull_requests` with state `open`; locally `gh pr list`), so that two
   drafts never take the same ID.
2. **Context**: read the decisions the subject touches and name every one the new decision changes, extends or
   contradicts; settle a contradiction in the new row ("Thay thế phần … của DEC-0xx"). For each feature concerned,
   `python3 tools/feature_context.py <ID> --out out/feature-<ID>.md` (`python` on Windows) and read it; find the rules
   concerned in `docs/blueprint/business-rules.md` (implementation rules: `docs/blueprint/technical-design.md`,
   section 8).
3. **Open points**: when an option, an amount, a rate, a deadline or a legal duty is not settled, ask the user before
   drafting. Never invent a tax rate, a legal duty or a price.
4. **The row**, as a table with the columns of the sheet:
   Decision ID | Topic | Context | Decision | Consequences | Source | Phase | Status
   - Topic: at most about eight words. Context: the problem, the constraints and the options compared, with the facts
     behind them. Decision: what is chosen, stated so that code and tests can follow it, ending with "Không chọn: …"
     for each rejected option and why. Consequences: for the code, the data, the operations and the users, costs
     included. Source: law, documentation, a product, the owner. Phase: as in the other rows. Status: `Accepted` when
     the owner agreed, otherwise `Proposed`.
   - Every cell without the character `|`, which splits a cell in the export. In the table and the tab line every
     cell is on one line (separate items with "; "); only the patch of step 6 may break a cell into lines.
   - Then the same eight cells as a single line separated by tab characters, in a code block: the fallback to paste
     by hand when the patch cannot be applied.
5. **Rows that change with it**: for each feature or rule, its ID, the column, the current text and the new text; a
   new rule as a full row of `docs/blueprint/business-rules.md`:
   Rule ID | Rule | Type | Static/Dynamic | Enforced At | Source | Features | Tables | Phase.
6. **The patch**, every row and cell change of steps 4 and 5 as JSON for `tools/blueprint_apply.py` (its docstring
   has the format):
   ```json
   {"rows":  [{"sheet": "Decisions", "after": "DEC-0xx", "values": {"Decision ID": "DEC-0xx", "Topic": "…", "Status": "Accepted"}}],
    "cells": [{"sheet": "Features", "id": "AUT-04", "column": "Acceptance Criteria", "old": "…", "new": "…"}]}
   ```
   - `rows`: keys are the column headers of the sheet, every value is text. `after` is the ID of the row above: the
     last DEC ID for the Decision (`"end"` also works), the rule above for a new rule (a row added earlier in the
     patch may be named).
   - `cells`: `old` is the current text exactly, copied from `docs/blueprint/*.md` with `<br>` turned back into a
     newline and `\|` into `|`; the tool refuses the patch when it differs. An ID is never changed this way.
   - In `new` and in new rows, a line break of the cell is `\n`: the lines of Acceptance Criteria are joined by `\n`.
   - Locally, write it to `out/dec-0xx.patch.json`. In a cloud session, send it as a file when the session can attach
     one, otherwise as a fenced `json` block that the owner saves as `out/dec-0xx.patch.json`.
7. **Commit message**, Conventional Commits as in `CLAUDE.md`, in the style of the commit that recorded DEC-096
   (`git log --grep "DEC-096" -1 --format=%B`): subject `docs: record DEC-0xx for <topic>`, body wrapped at 72
   characters, footer `Refs: DEC-0xx` followed by the features and rules it changes.
8. **The owner's steps**, numbered, each with its check:
   1. `git switch main`, `git pull --ff-only origin main`, `git switch -c docs/dec-0xx`.
   2. In your own terminal (a Claude session asks before running it, and cannot in the cloud): save the patch as
      `out/dec-0xx.patch.json` if it came as a block, then `python tools/blueprint_apply.py out/dec-0xx.patch.json`.
      It applies the patch and exports `docs/blueprint/`; when the patch does not fit the workbook it lists every
      problem and writes nothing (ask for a new draft, or fall back to pasting the tab line of step 4 and making the
      changes of step 5 by hand, then `python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx docs/blueprint`).
   3. Open the workbook in Excel once to look at the new rows, then close it without saving.
   4. `git status --short` lists the workbook and only the exported files the change explains, and
      `python -c "l=open('docs/blueprint/decisions.md',encoding='utf-8').read().splitlines()[-1]; print(l.startswith('| DEC-0xx |'))"`
      prints `True`.
   5. Commit the workbook and the exported files with the message of step 7.
   6. `python tools/pr_gates.py check origin/main HEAD "<subject>"` prints `PR gates passed`.
   7. `git push -u origin docs/dec-0xx`, open `https://github.com/taitt0600/zengear/pull/new/docs/dec-0xx` with the
      subject as title, and merge with **Rebase and merge** once every check passes.
