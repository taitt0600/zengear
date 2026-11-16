"""Print the slice of the specification that one feature needs, as Markdown: the feature dossier.

Usage: python3 tools/feature_context.py <Feature ID> [--out out/feature-<ID>.md] [--root <repository>] [--no-issues]

The dossier holds the feature row of docs/blueprint/features.md, its open Feature issues on GitHub with their open
blocked-by issues, Start date and handoff comment (DEC-148 (6), (8);
read with the token of gh through tools/issue.py; when GitHub cannot be read the section says so in bold and the
command prints a warning, so that nobody plans without the blockers), its rules (docs/blueprint/business-rules.md,
or docs/blueprint/technical-design.md for implementation rules), its use cases, the decisions they cite,
the glossary rows of its entities and tables, the docs/DATABASE.md section of every table its rules touch, and, when
it has screens, the file of each (docs/screens/, found by its ID through the screen list of docs/screens/README.md,
with its headings two levels down), docs/screens/conventions.md and the items of docs/screens/gaps.md they name.
/implement-feature, /fix-defect and the reviewer agents read it instead of searching the documents one by one.

Read-only on docs/ and GitHub; the same commit and the same issues give the same dossier (--no-issues leaves the
issues out, for tests and offline reading, and says so). --out writes only under out/ (ignored by git), so that a
dossier never replaces a tracked file.
"""
import argparse
import re
import sys
from pathlib import Path

import screen_docs

RULE_ID = re.compile(r'\bR-[A-Z]+-\d+\b')
DEC_ID = re.compile(r'\bDEC-\d{3}\b')
FEATURE_ID = re.compile(r'^[A-Z]{3}-\d{2}$')
HEADING = screen_docs.HEADING
split_row = screen_docs.split_row


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


def demote(markdown, levels=2):
    """The text with every heading outside a code fence moved down by <levels>, so that a screen file sits under the
    dossier's own headings."""
    out, fence = [], None
    for line in markdown.splitlines():
        marker = re.match(r'^\s*(`{3,}|~{3,})', line)
        if marker and (fence is None or marker.group(1).startswith(fence)):
            fence = marker.group(1) if fence is None else None
        elif fence is None and HEADING.match(line):
            line = '#' * levels + line
        out.append(line)
    return '\n'.join(out)


def screens(folder, feature_id):
    """The rows of the feature in the screen list, the text of their files, then conventions.md and the items of
    gaps.md that those files or rows name, which the screen files rely on; nothing when the folder has no README.md
    (a repository before the screens) or the feature has no screen."""
    if not (folder / 'README.md').is_file():
        return []
    rows = [row for row in screen_docs.screen_list(folder) if feature_id in screen_docs.features(row)]
    if not rows:
        return []
    texts = [(folder / f'{screen_docs.row_id(row)}.md').read_text(encoding='utf-8').strip()
             for row in rows if screen_docs.row_id(row)]
    parts = [markdown_table(rows), *(demote(text) for text in texts)]
    conventions = folder / 'conventions.md'
    if conventions.is_file():
        parts.append(demote(conventions.read_text(encoding='utf-8').strip()))
    named = screen_docs.gap_refs('\n'.join(texts + [row.get('Blocked by', '') for row in rows]))
    items = screen_docs.gap_items(folder) if (folder / 'gaps.md').is_file() else {}
    if named & set(items):
        parts += ['### Known gaps that these screens name (docs/screens/gaps.md)',
                  *(items[number] for number in sorted(named & set(items)))]
    return parts


def names(cell):
    """Comma-separated names of a cell, without backticks."""
    return [name.strip().strip('`') for name in cell.split(',') if name.strip()]


UNREAD = '**GitHub could not be read: the issues and blockers of this feature are not known ({}). Do not plan as if it ' \
         'had none: read them with `python tools/issue.py feature {}` once GitHub answers (DEC-148 (7)).**'


def issue_section(feature_id, reader):
    """The open Feature issues of the feature and their open blockers, or the reason they could not be read."""
    lines = ['## Issues (GitHub)']
    if reader is None:
        return lines + ['_Not read (--no-issues)._']
    try:
        numbers = reader.open_feature_issues(feature_id)
        if not numbers:
            return lines + [f'No open Feature issue of {feature_id}: the feat pull request needs one to close '
                            '(DEC-148 (6)).']
        for number in numbers:
            fields = reader.fields(number)
            handoff = 'yes' if reader.has_handoff(number) else 'no'
            lines.append(f'### #{number}\n- Start date: {fields.get("Start date", "not set")}; handoff comment: {handoff}')
            blockers = reader.open_blockers(number)
            if blockers:
                lines.append('- Open blocked-by issues; the plan handles each, in this pull request with Closes or as a '
                             'Needs decision item (DEC-148 (6)):')
                lines += [f'  - #{b["number"]} {b.get("title", "")}' for b in blockers]
            else:
                lines.append('- No open blocked-by issue.')
    except Exception as error:  # noqa: BLE001  every failure is written, never hidden (DEC-148 (7))
        print(f'feature_context: warning: GitHub could not be read: {error}', file=sys.stderr)
        return lines + [UNREAD.format(error, feature_id)]
    return lines


def build(feature_id, root, issues=None):
    """issues: the reader of tools/issue.py (None leaves the section empty, as --no-issues)."""
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
    parts += issue_section(feature_id, issues)
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
    my_screens = screens(root / 'docs' / 'screens', feature_id)
    if my_screens:
        parts += ['## Screens (docs/screens/)', *my_screens]
    return '\n\n'.join(parts) + '\n'


class Unreadable:
    """A reader whose every call raises the error met while making the real one (no token, no gh)."""

    def __init__(self, error):
        self.error = error

    def __getattr__(self, name):
        def fail(*args):
            raise self.error
        return fail


def issue_reader():
    try:
        import issue
        return issue.Reader()
    except Exception as error:  # noqa: BLE001  written in the dossier by issue_section
        return Unreadable(error)


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
    parser.add_argument('--no-issues', action='store_true', help='leave the GitHub issues out (tests, offline)')
    args = parser.parse_args(argv)
    root = args.root.resolve()
    try:
        dossier = build(args.feature_id, root, None if args.no_issues else issue_reader())
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
