"""Export product-domain-blueprint-MVP.xlsx to Markdown (one file per sheet) for code assistants and diffs.
Usage: python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx docs/blueprint"""
import sys, os, re, openpyxl

def cell(v):
    if v is None: return ''
    s = str(v).replace('|', '\\|').replace('\r', '')
    return s.replace('\n', '<br>')

def slug(name):
    return re.sub(r'[^a-z0-9]+', '-', name.lower()).strip('-')

def main(src, out):
    wb = openpyxl.load_workbook(src, read_only=True, data_only=True)
    os.makedirs(out, exist_ok=True)
    index = ['# Blueprint', '', f'Generated from `{os.path.basename(src)}` — do not edit by hand; edit the workbook and run `tools/export_blueprint.py`.', '']
    for ws in wb.worksheets:
        rows = [r for r in ws.iter_rows(values_only=True) if any(v not in (None, '') for v in r)]
        if not rows: continue
        head, body = rows[0], rows[1:]
        lines = [f'# {ws.title}', '', '| ' + ' | '.join(cell(h) for h in head) + ' |', '|' + '---|' * len(head)]
        lines += ['| ' + ' | '.join(cell(v) for v in r) + ' |' for r in body]
        fn = slug(ws.title) + '.md'
        with open(os.path.join(out, fn), 'w', encoding='utf-8') as f: f.write('\n'.join(lines) + '\n')
        index.append(f'- [{ws.title}]({fn}) — {len(body)} rows')
    with open(os.path.join(out, 'README.md'), 'w', encoding='utf-8') as f: f.write('\n'.join(index) + '\n')

if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
