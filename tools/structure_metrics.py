"""Metrics of the code structure that reopen DEC-097 (docs/ARCHITECTURE.md, Folder structure).
Usage, from the repository root: python tools/structure_metrics.py [number of feat commits, default 20]"""
import os, re, subprocess, sys
from collections import Counter

SRC = 'api/src'
SERVICES = f'{SRC}/ZenGear.Services'
AREA_FOLDER_LIMIT, MODELS_LIMIT = 20, 40
KIND_FOLDERS = {'Models', 'Enums', 'Validators'}
AREAS = set(re.findall(r'^\| `([A-Za-z]+)` \|', open('docs/ARCHITECTURE.md', encoding='utf-8').read(), re.M))

def skipped(path):
    return any(part in ('bin', 'obj', 'Migrations') for part in path.split('/'))

def folder_sizes():
    sizes = Counter()
    for root, _, files in os.walk(SRC):
        root = root.replace('\\', '/')
        if not skipped(root):
            sizes[root] = sum(f.endswith('.cs') for f in files)
    return sizes

def over_limit(sizes):
    for folder, count in sorted(sizes.items()):
        rel = folder[len(SERVICES) + 1:].split('/') if folder.startswith(SERVICES + '/') else None
        if folder.endswith('/Models') and count > MODELS_LIMIT:
            yield folder, count, MODELS_LIMIT
        elif rel and rel[0] != 'Common' and (len(rel) == 1 or rel[-1] not in KIND_FOLDERS) and count > AREA_FOLDER_LIMIT:
            yield folder, count, AREA_FOLDER_LIMIT

def git(*args):
    return subprocess.run(['git', *args], capture_output=True, text=True, encoding='utf-8', check=True).stdout

def commits(grep):
    out = git('log', '--no-merges', '-E', f'--grep={grep}', '--format=%x1e%h %s', '--name-only', '--', SRC)
    for block in out.split('\x1e')[1:]:
        head, *files = [line for line in block.splitlines() if line.strip()]
        yield head, [f for f in files if not skipped(f)]

def main(limit):
    sizes = folder_sizes()
    over = list(over_limit(sizes))
    print(f'Folders above their limit (area root or mechanism: {AREA_FOLDER_LIMIT}, Models: {MODELS_LIMIT}): {len(over)}')
    for folder, count, cap in over:
        print(f'  {count:3} > {cap}  {folder}')
    print('Largest folders:')
    for folder, count in sizes.most_common(5):
        print(f'  {count:3}  {folder}')

    print(f'Folders touched by the last {limit} feat commits (newest first):')
    for head, files in list(commits('^feat'))[:limit]:
        folders = {os.path.dirname(f) for f in files}
        areas = sorted({f.split('/')[3] for f in files if f.startswith(SERVICES + '/')} & AREAS)
        print(f'  {len(folders):3}  {",".join(areas) or "-":20}  {head}')

    renamed = [head for head, _ in commits('.') if not re.match(r'^\w+ refactor', head)
               and git('show', '--format=', '--diff-filter=R', '--name-only', head.split()[0], '--', SRC).strip()]
    print(f'Commits outside refactor that rename files: {len(renamed)}')
    for head in renamed[:10]:
        print(f'  {head}')

if __name__ == '__main__':
    main(int(sys.argv[1]) if len(sys.argv) > 1 else 20)
