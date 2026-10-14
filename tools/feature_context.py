"""Print the slice of the specification that one feature needs, as Markdown: the feature dossier.

Usage: python3 tools/feature_context.py <Feature ID> [--out out/feature-<ID>.md] [--root <repository>]

The dossier holds the feature row of docs/blueprint/features.md, its rows of docs/STATUS.md (a Next row keyed by
its ID, a Waiting row triggered "before <ID>"; DEC-110 (5)), its rules (docs/blueprint/business-rules.md,
or docs/blueprint/technical-design.md for implementation rules), its use cases, the decisions they cite,
the glossary rows of its entities and tables, and the docs/DATABASE.md section of every table its rules touch.
/implement-feature, /fix-defect and the reviewer agents read it instead of searching the documents one by one.

Read-only on docs/ and deterministic: the same commit gives the same dossier. --out writes only under out/
(ignored by git), so that a dossier never replaces a tracked file.
"""
import argparse
import re
import sys
from pathlib import Path

RULE_ID = re.compile(r'\bR-[A-Z]+-\d+\b')
DEC_ID = re.compile(r'\bDEC-\d{3}\b')
FEATURE_ID = re.compile(r'^[A-Z]{3}-\d{2}$')
HEADING = re.compile(r'^(#{1,6})\s+(.*\S)')
# Tables of docs/STATUS.md: (heading, column, value of that column in a row that names the feature).
WORK_STATE = (('Next', 'Key', '{}'), ('Waiting for a trigger', 'Trigger', 'before {}'))


def split_row(line):
    """Cells of a Markdown table row; an escaped pipe (\\|) stays inside its cell."""
    cells = re.split(r'(?<!\\)\|', line.strip())
    return [cell.strip() for cell in cells[1:-1]]


def read_table(path, after_heading=None):
    """Rows of the first Markdown table of the file (or of the first one after a heading), as dicts by column."""
    header, rows, armed = None, [], after_heading is None
    for line in path.read_text(encoding='utf-8').splitlines():
        heading = HEADING.match(line)
        if heading and not armed:
            armed = heading.group(2) == after_heading
            continue
        if not armed:
            continue
        if not line.startswith('|'):
            if header is not None:
                break
            continue
        cells = split_row(line)
        if header is None:
            header = cells
        elif not set(''.join(cells)) <= set('-: '):
            rows.append(dict(zip(header, cells)))
    if header is None:
        raise ValueError(f'no table in {path}' + (f' under "{after_heading}"' if after_heading else ''))
    return rows


def table_sections(database_md, tables):
    """The '#### <table>' sections of DATABASE.md, each up to the next heading of level 4 or less."""
    out, keep = [], False
    for line in database_md.read_text(encoding='utf-8').splitlines():
        heading = HEADING.match(line)
        if heading and len(heading.group(1)) <= 4:
            keep = len(heading.group(1)) == 4 and heading.group(2) in tables
        if keep:
            out.append(line)
    return '\n'.join(out)


def row_block(title, row):
    lines = [f'### {title}']
    lines += [f'- **{key}**: {value}' for key, value in row.items() if value and value != '—']
    return '\n'.join(lines)


def markdown_table(rows):
    header = '| ' + ' | '.join(rows[0]) + ' |'
    return '\n'.join([header, '|' + '---|' * len(rows[0]), *('| ' + ' | '.join(row.values()) + ' |' for row in rows)])


def work_state(status_md, feature_id):
    """One table per kind of docs/STATUS.md with rows that name the feature: the whole cell, without backticks, is
    the ID (Next) or "before <ID>" (Waiting); an ID anywhere else does not count. tools/pr_gates.py keeps the same
    reader for its gate 6."""
    tables = []
    for heading, column, value in WORK_STATE:
        try:
            rows = read_table(status_md, after_heading=heading)
        except (FileNotFoundError, ValueError):  # no file, no heading or no table: no row
            continue
        mine = [row for row in rows if row.get(column, '').strip('`').strip() == value.format(feature_id)]
        if mine:
            tables.append(markdown_table(mine))
    return tables


def names(cell):
    """Comma-separated names of a cell, without backticks."""
    return [name.strip().strip('`') for name in cell.split(',') if name.strip()]


def build(feature_id, root):
    if not FEATURE_ID.match(feature_id):
        raise ValueError(f'not a feature ID: {feature_id!r} (expected e.g. POS-01)')
    bp = root / 'docs' / 'blueprint'
    features = read_table(bp / 'features.md')
    feature = next((row for row in features if row.get('Feature ID') == feature_id), None)
    if feature is None:
        raise LookupError(f'{feature_id} is not a row of docs/blueprint/features.md')

    rules = {row['Rule ID']: row for row in read_table(bp / 'business-rules.md')}
    use_cases = read_table(bp / 'use-cases.md')
    decisions = read_table(bp / 'decisions.md')
    glossary = read_table(root / 'docs' / 'GLOSSARY.md', after_heading='Entities')

    rule_ids = list(dict.fromkeys(RULE_ID.findall(feature.get('Rules', ''))))
    my_rules = [rules[rule_id] for rule_id in rule_ids if rule_id in rules]
    implementation_ids = [rule_id for rule_id in rule_ids if rule_id not in rules]
    # Feature ID of a use case may list several features; POS-010 must not match POS-01.
    my_use_cases = [row for row in use_cases if feature_id in re.split(r'[,\s]+', row.get('Feature ID', ''))]

    # Implementation rules live in technical-design.md, section 8: keep the lines that name them.
    technical = (bp / 'technical-design.md').read_text(encoding='utf-8').splitlines()
    implementation_lines = [line for line in technical
                            if any(re.search(rf'\b{re.escape(rule_id)}\b', line) for rule_id in implementation_ids)]
    missing = [rule_id for rule_id in implementation_ids
               if not any(re.search(rf'\b{re.escape(rule_id)}\b', line) for line in implementation_lines)]

    cited = ' '.join([*feature.values()] + [' '.join(row.values()) for row in my_rules + my_use_cases]
                     + implementation_lines)
    decision_ids = set(DEC_ID.findall(cited))
    my_decisions = [row for row in decisions if row.get('Decision ID') in decision_ids]

    tables = {table for row in my_rules for table in names(row.get('Tables', ''))}
    entities = set(names(feature.get('Entities', '')))
    my_glossary = [row for row in glossary
                   if row.get('Entity', '').strip('`') in entities or row.get('Table', '').strip('`') in tables]

    parts = [f'# Feature dossier {feature_id}',
             '_Generated by tools/feature_context.py from docs/. Do not edit; the documents are the source._',
             '## Feature', row_block(feature_id, feature)]
    work = work_state(root / 'docs' / 'STATUS.md', feature_id)
    if work:
        parts += ['## Work state (docs/STATUS.md)', *work]
    parts += ['## Rules', *[row_block(row['Rule ID'], row) for row in my_rules]]
    if implementation_ids:
        parts += ['## Implementation rules (docs/blueprint/technical-design.md, section 8)', *implementation_lines]
    if missing:
        parts.append(f'**Rules not found in business-rules.md nor technical-design.md**: {", ".join(missing)}')
    parts += ['## Use cases', *([row_block(row['UC ID'], row) for row in my_use_cases] or ['(none)'])]
    parts += ['## Decisions', *([row_block(row['Decision ID'], row) for row in my_decisions] or ['(none)'])]
    if my_glossary:
        parts += ['## Glossary (docs/GLOSSARY.md)', markdown_table(my_glossary)]
    parts += ['## Tables (docs/DATABASE.md)', table_sections(root / 'docs' / 'DATABASE.md', tables) or '(none)']
    return '\n\n'.join(parts) + '\n'


def output_path(root, out):
    """--out resolved under <root>/out/, or an error: a dossier never replaces a tracked file."""
    target = (root / out).resolve() if not Path(out).is_absolute() else Path(out).resolve()
    allowed = (root / 'out').resolve()
    if allowed not in target.parents:
        raise ValueError(f'--out must be a file under {allowed}, not {target}')
    return target


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    parser.add_argument('feature_id', help='Feature ID, e.g. POS-01')
    parser.add_argument('--out', help='write the dossier to this file under out/ instead of stdout')
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parent.parent,
                        help='repository root (default: the parent of tools/)')
    args = parser.parse_args(argv)
    root = args.root.resolve()
    try:
        dossier = build(args.feature_id, root)
        if args.out:
            target = output_path(root, args.out)
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(dossier, encoding='utf-8')
            print(f'feature_context: wrote {target.relative_to(root).as_posix()} ({len(dossier.encode())} bytes)')
        else:
            # Vietnamese text: never depend on the console code page (Windows).
            sys.stdout.reconfigure(encoding='utf-8')
            sys.stdout.write(dossier)
    except (ValueError, LookupError, OSError, KeyError) as error:
        print(f'feature_context: {error}', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main())
