"""Export product-domain-blueprint-MVP.xlsx to Markdown (one file per sheet) for code assistants and diffs.
Usage: python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx docs/blueprint

Before writing anything it checks the ID column of the sheets other tools read (pr_gates.py, feature_context.py):
every ID is text in its format, without a hidden character, and unique; the Status of a decision has no space or
hidden character around it. Excel can turn DEC-098 into a date, or keep an invisible character from the clipboard;
such a row looks right in Excel but silently drops out of what the PR gates accept. On a problem it writes nothing
and exits with 1."""
import sys, os, re, unicodedata, openpyxl

# Sheet: (format of the ID in its first column, an example to type).
ID_FORMATS = {
    'Features': (r'[A-Z]{3}-\d{2}', 'AUT-04'),
    'Use Cases': (r'UC-\d{3}', 'UC-001'),
    'Business Rules': (r'R-[A-Z]{2,4}-\d{2}', 'R-SEC-03'),
    'Decisions': (r'DEC-\d{3}', 'DEC-099'),
}

def cell(v):
    if v is None: return ''
    s = str(v).replace('|', '\\|').replace('\r', '')
    return s.replace('\n', '<br>')

def slug(name):
    return re.sub(r'[^a-z0-9]+', '-', name.lower()).strip('-')

def hidden(s):
    return [f'U+{ord(c):04X}' for c in s if unicodedata.category(c) in ('Cf', 'Cc') and c not in '\n\t']

def problems(title, head, rows):
    """What is wrong with the IDs (and the decision statuses) of one sheet; rows are (Excel row number, values)."""
    if title not in ID_FORMATS: return []
    fmt, example = ID_FORMATS[title]
    found, seen = [], {}
    status = head.index('Status') if title == 'Decisions' and 'Status' in head else None
    for number, r in rows:
        v, where = r[0], f'{title} row {number}'
        if not isinstance(v, str) or not re.fullmatch(fmt, v):
            shown = f'{v!r} ({type(v).__name__}' + (f', hidden {" ".join(hidden(v))})' if isinstance(v, str) and hidden(v) else ')')
            found.append(f"{where}: the ID {shown} is not text like {example}; type it again as '{example} (leading apostrophe)")
            continue
        if v in seen: found.append(f'{where}: the ID {v} repeats row {seen[v]}')
        seen.setdefault(v, number)
        if status is not None:
            s = r[status]
            if not isinstance(s, str) or s != s.strip() or hidden(s):
                found.append(f"{where}: the Status {s!r} has a space or a hidden character around it; the PR gates accept only 'Accepted' exactly")
    if title == 'Decisions' and status is None: found.append('Decisions: no Status column')
    return found

def main(src, out):
    wb = openpyxl.load_workbook(src, read_only=True, data_only=True)
    sheets, errors = [], []
    for ws in wb.worksheets:
        rows = [(n, r) for n, r in enumerate(ws.iter_rows(values_only=True), start=1) if any(v not in (None, '') for v in r)]
        if not rows: continue
        head, body = rows[0][1], rows[1:]
        errors += problems(ws.title, list(head), body)
        sheets.append((ws.title, head, [r for _, r in body]))
    if errors:
        for e in errors: print(f'export_blueprint: {e}', file=sys.stderr)
        print(f'export_blueprint: {len(errors)} problem(s), nothing written', file=sys.stderr)
        return 1
    os.makedirs(out, exist_ok=True)
    index = ['# Blueprint', '', f'Generated from `{os.path.basename(src)}` — do not edit by hand; edit the workbook and run `tools/export_blueprint.py`.', '']
    for title, head, body in sheets:
        lines = [f'# {title}', '', '| ' + ' | '.join(cell(h) for h in head) + ' |', '|' + '---|' * len(head)]
        lines += ['| ' + ' | '.join(cell(v) for v in r) + ' |' for r in body]
        fn = slug(title) + '.md'
        # newline='\n': the same bytes on Windows and Linux (.gitattributes keeps LF).
        with open(os.path.join(out, fn), 'w', encoding='utf-8', newline='\n') as f: f.write('\n'.join(lines) + '\n')
        index.append(f'- [{title}]({fn}) — {len(body)} rows')
    with open(os.path.join(out, 'README.md'), 'w', encoding='utf-8', newline='\n') as f: f.write('\n'.join(index) + '\n')
    return 0

if __name__ == '__main__':
    if len(sys.argv) != 3: sys.exit(__doc__)
    sys.exit(main(sys.argv[1], sys.argv[2]))
