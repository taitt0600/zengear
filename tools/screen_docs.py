"""The screen list of docs/screens/README.md, the screen files and the known gaps agree.

  python tools/screen_docs.py [<folder>]

<folder> defaults to docs/screens of this repository. The screen list is every table under the heading
"## Screen list" of README.md, one table per part of the web app under a heading "### <part>"; its columns are
ID, Screen, Route, Feature, Permission and Blocked by. The check fails when:

- a row's ID is not a link [SCR-<AAA>-<nn>](SCR-<AAA>-<nn>.md), or names an ID twice;
- a row's file does not exist, or its first line is not "# <ID> <Screen>" with the row's ID and Screen;
- a file SCR-*.md of the folder has no row;
- a Feature cell is empty or holds something other than Feature IDs separated by commas;
- the items of gaps.md ("<n>. **<title>** ...") are not numbered 1, 2, 3, ... without a hole: a closed gap keeps
  its number;
- a file of the folder names a gap ("gap 3", "gaps 7 and 8", "gaps 7 to 10") that gaps.md does not have;
- a Blocked by cell names a closed gap (an item whose title, or the bracket after it, is followed by "— closed.").

tools/feature_context.py reads the screens of a feature through screen_list() and the gaps they name through
gap_refs() and gap_items(). The CI job tools-test runs the check
on the real folder (tools/tests/test_screen_docs.py).

Exit codes: 0 passed, 1 a check failed or a file cannot be read, 2 usage."""
import re
import sys
from pathlib import Path

FOLDER = Path(__file__).resolve().parents[1] / 'docs' / 'screens'
LINKED_ID = re.compile(r'^\[(SCR-[A-Z]{3}-\d{2})\]\((SCR-[A-Z]{3}-\d{2})\.md\)$')
FEATURE_ID = re.compile(r'^[A-Z]{3}-\d{2}$')
HEADING = re.compile(r'^(#{1,6})\s+(.*\S)')
GAP_ITEM = re.compile(r'^(\d+)\.\s+\*\*(.+?)\*\*(.*)')
# After the title, an optional bracket such as "(DEC-104 (9))", then "— closed." ("— closed for SCR-STF-02:" is open).
CLOSED = re.compile(r'\s*(?:\(.*?\)\s*)?— closed\.')
# "gap 3", "Gap 3", "gaps 1 and 4", "gaps 7 to 10", "gaps 2, 5, and 7"; a newline may fall inside it. "gap 12px" is
# no gap.
GAP_REF = re.compile(r'\bgaps?\s+(\d+\b(?:\s*(?:,\s*and|,|and|to)\s*\d+\b)*)', re.IGNORECASE)
COLUMNS = ['ID', 'Screen', 'Route', 'Feature', 'Permission', 'Blocked by']


def split_row(line):
    """Cells of a Markdown table row; an escaped pipe (\\|) stays inside its cell."""
    cells = re.split(r'(?<!\\)\|', line.strip())
    return [cell.strip() for cell in cells[1:-1]]


def screen_list(folder):
    """The rows of the screen list of <folder>/README.md, as dicts by column with 'Part' (the heading of its table)
    added; ValueError when the README has no screen list or a table sits outside a part heading."""
    rows, part, header, inside, found = [], None, None, False, False
    for line in (folder / 'README.md').read_text(encoding='utf-8').splitlines():
        heading = HEADING.match(line)
        if heading:
            level, text = len(heading.group(1)), heading.group(2)
            if level <= 2:
                inside = level == 2 and text == 'Screen list'
                found = found or inside
                part = None
            elif inside and level == 3:
                part = text
            header = None
            continue
        if not inside:
            continue
        if not line.startswith('|'):
            header = None
            continue
        cells = split_row(line)
        if header is None:
            if part is None:
                raise ValueError('README.md: a table of the screen list sits outside a "### <part>" heading')
            if cells != COLUMNS:
                raise ValueError(f'README.md, part {part}: the columns are {cells}, expected {COLUMNS}')
            header = cells
        elif not set(''.join(cells)) <= set('-: '):
            if len(cells) != len(header):
                raise ValueError(f'README.md, part {part}: the row {line.strip()!r} has {len(cells)} cells, '
                                 f'expected {len(header)}')
            rows.append({**dict(zip(header, cells)), 'Part': part})
    if not found:
        raise ValueError('README.md has no heading "## Screen list"')
    return rows


def row_id(row):
    """The screen ID of a row whose ID cell is a link to its own file, else None."""
    match = LINKED_ID.match(row.get('ID', ''))
    return match.group(1) if match and match.group(1) == match.group(2) else None


def features(row):
    return [name.strip() for name in row.get('Feature', '').split(',') if name.strip()]


def gap_items(folder):
    """{number: text} of the items of gaps.md, each from its "<n>. **" line up to the next item or heading."""
    items, number = {}, None
    for line in (folder / 'gaps.md').read_text(encoding='utf-8').splitlines():
        match = GAP_ITEM.match(line)
        if match:
            number = int(match.group(1))
            items[number] = [line]
        elif HEADING.match(line):
            number = None
        elif number is not None:
            items[number].append(line)
    return {key: '\n'.join(lines).rstrip() for key, lines in items.items()}


def gaps(folder):
    """{number: closed} of the items of gaps.md, and the errors of their numbering."""
    numbers, errors = {}, []
    for line in (folder / 'gaps.md').read_text(encoding='utf-8').splitlines():
        match = GAP_ITEM.match(line)
        if match:
            number = int(match.group(1))
            if number in numbers:
                errors.append(f'gaps.md: gap {number} is numbered twice')
            numbers[number] = CLOSED.match(match.group(3)) is not None
    expected = list(range(1, len(numbers) + 1))
    if sorted(numbers) != expected:
        errors.append(f'gaps.md: the gaps are numbered {sorted(numbers)}, expected {expected} (a closed gap keeps '
                      f'its number)')
    return numbers, errors


def gap_refs(text):
    """The gap numbers a text names; "gaps 7 to 10" names 7, 8, 9 and 10."""
    out = set()
    for match in GAP_REF.finditer(text):
        tokens = re.findall(r'\d+|to', match.group(1).lower())
        for i, token in enumerate(tokens):
            if token == 'to':
                out.update(range(int(tokens[i - 1]), int(tokens[i + 1]) + 1))
            else:
                out.add(int(token))
    return out


def check(folder):
    """The errors of the folder, empty when it is consistent."""
    errors = []
    try:
        rows = screen_list(folder)
        numbers, gap_errors = gaps(folder)
    except (OSError, ValueError) as error:
        return [str(error)]
    errors += gap_errors
    if not rows:
        errors.append('README.md: the screen list has no row')

    listed = set()
    for row in rows:
        screen = row_id(row)
        if screen is None:
            errors.append(f'README.md: the ID cell {row["ID"]!r} is not a link [SCR-AAA-nn](SCR-AAA-nn.md) to the '
                          f'file of the same ID')
            continue
        if screen in listed:
            errors.append(f'README.md: {screen} has two rows')
        listed.add(screen)
        if not features(row) or not all(FEATURE_ID.match(name) for name in features(row)):
            errors.append(f'README.md: {screen} has the Feature cell {row["Feature"]!r}, expected Feature IDs '
                          f'separated by commas')
        path = folder / f'{screen}.md'
        if not path.is_file():
            errors.append(f'README.md: {screen} has no file {path.name}')
        else:
            first = path.read_text(encoding='utf-8').splitlines()[:1]
            expected = f'# {screen} {row["Screen"]}'
            if first != [expected]:
                errors.append(f'{path.name}: the first line is {(first or [""])[0]!r}, expected {expected!r} '
                              f'(the row of README.md)')
        for number in sorted(gap_refs(row.get('Blocked by', ''))):
            if numbers.get(number):
                errors.append(f'README.md: {screen} is blocked by gap {number}, which gaps.md says is closed')

    for path in sorted(folder.glob('SCR-*.md')):
        if path.stem not in listed:
            errors.append(f'{path.name} has no row in the screen list of README.md')

    for path in sorted(folder.glob('*.md')):
        unknown = sorted(gap_refs(path.read_text(encoding='utf-8')) - set(numbers))
        if unknown:
            errors.append(f'{path.name} names gap {", ".join(map(str, unknown))}, which gaps.md does not have')
    return errors


def main(argv=None):
    args = sys.argv[1:] if argv is None else argv
    if len(args) > 1 or (args and args[0].startswith('-')):
        print(__doc__.splitlines()[2].strip(), file=sys.stderr)
        return 2
    folder = Path(args[0]) if args else FOLDER
    errors = check(folder)
    for error in errors:
        print(f'screen_docs: {error}', file=sys.stderr)
    if not errors:
        print(f'screen_docs: {folder.as_posix()} agrees ({len(screen_list(folder))} screens)')
    return 1 if errors else 0


if __name__ == '__main__':
    sys.exit(main())
