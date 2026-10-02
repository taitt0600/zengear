---
name: decision-draft
description: Draft a ZenGear Decision (DEC-xxx) for the Decisions sheet of docs/product-domain-blueprint-MVP.xlsx, with the feature and rule rows it changes, the commit message and the owner's numbered steps, once the owner has settled a proposal, an analysis or a "Cần bạn quyết định" item. Drafts only. Use when the user asks to draft or record a decision.
argument-hint: <what was decided, or the question to settle>
allowed-tools: Read, Grep, Glob, Bash(grep *), Bash(git log *), Bash(python3 tools/feature_context.py *), Bash(python tools/feature_context.py *), mcp__github__list_pull_requests
---
Draft a Decision for: $ARGUMENTS

Drafts only: change no tracked file and commit nothing. The specification changes only in the workbook, by the
owner (CLAUDE.md, Rules for every change), and it is read-only in a cloud session. Answer in Vietnamese; every cell
of the row is in Vietnamese, in the style of the rows of `docs/blueprint/decisions.md`.

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
   - Every cell on one line (separate items with "; ") and without the character `|`, which splits a cell in the
     export.
   - Then the same eight cells as a single line separated by tab characters, in a code block, to paste at once.
5. **Rows that change with it**: for each feature or rule, its ID, the column, the current text and the new text; a
   new rule as a full row of `docs/blueprint/business-rules.md`:
   Rule ID | Rule | Type | Static/Dynamic | Enforced At | Source | Features | Tables | Phase.
6. **Commit message**, Conventional Commits as in `CLAUDE.md`, in the style of the commit that recorded DEC-096
   (`git log --grep "DEC-096" -1 --format=%B`): subject `docs: record DEC-0xx for <topic>`, body wrapped at 72
   characters, footer `Refs: DEC-0xx` followed by the features and rules it changes.
7. **The owner's steps**, numbered, each with its check:
   1. `git switch main`, `git pull --ff-only origin main`, `git switch -c docs/dec-0xx`.
   2. Excel, sheet Decisions: paste the tab line into column A of the first empty row. Click the ID cell and type it
      again as `'DEC-0xx`: the leading apostrophe keeps it text, so Excel neither turns it into a date nor keeps a
      hidden character from the clipboard. Copy the format of the row above with Format Painter, make the changes of
      step 5 in the other sheets, save and close Excel.
   3. `python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx docs/blueprint`.
   4. `git status --short` lists the workbook and only the exported files the change explains, and
      `python -c "l=open('docs/blueprint/decisions.md',encoding='utf-8').read().splitlines()[-1]; print(l.startswith('| DEC-0xx |'))"`
      prints `True`.
   5. Commit the workbook and the exported files with the message of step 6.
   6. `python tools/pr_gates.py check origin/main HEAD "<subject>"` prints `PR gates passed`.
   7. `git push -u origin docs/dec-0xx`, open `https://github.com/taitt0600/zengear/pull/new/docs/dec-0xx` with the
      subject as title, and merge with **Rebase and merge** once every check passes.
