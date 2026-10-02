---
name: feature-context
description: Write the feature dossier of one ZenGear feature to out/feature-<ID>.md (its row, rules with implementation rules, use cases, the decisions they cite, glossary rows and DATABASE.md tables) and read it. Use before planning, reviewing or fixing a feature, instead of searching docs/blueprint and docs/DATABASE.md by hand.
argument-hint: <Feature ID, e.g. POS-01>
allowed-tools: Bash(python3 tools/feature_context.py *), Bash(python tools/feature_context.py *), Read
---
1. From the repository root: `python3 tools/feature_context.py $ARGUMENTS --out out/feature-$ARGUMENTS.md`
   (`python` on Windows).
2. Exit code 1: report its message and stop; never guess a rule or a table.
3. Read `out/feature-$ARGUMENTS.md`. It is cut from the documents, which stay the source: read the owning area of
   each table in `docs/ARCHITECTURE.md`, and open a source document only for what the dossier does not hold.
4. A line "Rules not found in business-rules.md nor technical-design.md" is a gap in the specification: a
   Needs decision item (policy of `/implement-feature`, step 4), never a rule to invent.
