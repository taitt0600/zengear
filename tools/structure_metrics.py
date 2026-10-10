"""Metrics of the code structure (DEC-106, docs/ARCHITECTURE.md, Folder structure): the size of each area, which
grows into its own project above its thresholds, and the folders touched per feat commit (target: at most 5).
Usage, from the repository root:
  python tools/structure_metrics.py [number of feat commits, default 20]
  python tools/structure_metrics.py --range origin/main..HEAD   (the feat commits of a branch, for the report)"""
import os, posixpath, subprocess, sys

SRC = 'api/src'
SERVICES = f'{SRC}/ZenGear.Services'
MODULE_LIMIT, FILE_LIMIT, FOLDER_TARGET = 8, 60, 5
NOT_AREAS = {'Common', 'bin', 'obj'}

def skipped(path):
    return any(part in ('bin', 'obj', 'Migrations') for part in path.split('/'))

def cs_files(folder):
    return sum(f.endswith('.cs') for root, _, files in os.walk(folder)
               if not skipped(root.replace('\\', '/')) for f in files)

def area_sizes(root='.'):
    """Area of ZenGear.Services → (modules, .cs files). Only ZenGear.Services counts: it is what would become the
    area's project, while entities stay in the one DbContext of ZenGear.Data."""
    services = os.path.join(root, SERVICES)
    sizes = {}
    for area in sorted(os.listdir(services)):
        path = os.path.join(services, area)
        if area in NOT_AREAS or not os.path.isdir(path):
            continue
        modules = [m for m in os.listdir(path) if os.path.isdir(os.path.join(path, m)) and cs_files(os.path.join(path, m))]
        sizes[area] = (len(modules), cs_files(path))
    return sizes

def over_limit(sizes):
    return [(area, modules, files) for area, (modules, files) in sizes.items()
            if modules > MODULE_LIMIT or files > FILE_LIMIT]

def module_folder(path):
    """A file of ZenGear.Services/<Area>/<Module>/ counts as its module, also inside a mechanism folder; any other file
    counts as its own folder."""
    parts = path.split('/')
    if path.startswith(SERVICES + '/') and len(parts) > 6:
        return '/'.join(parts[:5])
    return posixpath.dirname(path)

def folders_touched(files):
    return len({module_folder(f) for f in files if not skipped(f)})

def git(root, *args):
    return subprocess.run(['git', '-C', root, *args], capture_output=True, text=True, encoding='utf-8', check=True).stdout

def commits(root, grep, revisions=()):
    out = git(root, 'log', '--no-merges', '-E', f'--grep={grep}', '--format=%x1e%h %s', '--name-only', *revisions, '--', SRC)
    for block in out.split('\x1e')[1:]:
        head, *files = [line for line in block.splitlines() if line.strip()]
        yield head, [f for f in files if not skipped(f)]

def feat_lines(root, revisions, limit=None):
    for head, files in list(commits(root, '^feat', revisions))[:limit]:
        areas = sorted({f.split('/')[3] for f in files if f.startswith(SERVICES + '/') and f.count('/') > 3} - NOT_AREAS)
        yield folders_touched(files), f'{",".join(areas) or "-":20}  {head}'

def report_range(root, revision_range):
    lines = list(feat_lines(root, [revision_range]))
    for count, line in lines:
        print(f'  {count:3}  {line}')
    print(f'Folders per feat commit: {", ".join(str(count) for count, _ in lines) or "none"}')

def main(root, limit):
    sizes = area_sizes(root)
    over = over_limit(sizes)
    print(f'Areas above a threshold ({MODULE_LIMIT} modules or {FILE_LIMIT} files of ZenGear.Services): {len(over)}')
    for area, modules, files in over:
        print(f'  {area}: {modules} modules, {files} files')
    print('Areas:')
    for area, (modules, files) in sizes.items():
        print(f'  {modules:3} modules {files:4} files  {area}')

    print(f'Folders touched (module level) by the last {limit} feat commits, newest first (target: at most {FOLDER_TARGET}):')
    for count, line in feat_lines(root, [], limit):
        print(f'  {count:3}  {line}')

    renamed = [head for head, _ in commits(root, '.') if not head.split(' ', 1)[1].startswith('refactor')
               and git(root, 'show', '--format=', '--diff-filter=R', '--name-only', head.split()[0], '--', SRC).strip()]
    print(f'Commits outside refactor that rename files: {len(renamed)}')
    for head in renamed[:10]:
        print(f'  {head}')

if __name__ == '__main__':
    args = sys.argv[1:]
    if args[:1] == ['--range'] and len(args) == 2:
        report_range('.', args[1])
    elif not args or (len(args) == 1 and args[0].isdigit()):
        main('.', int(args[0]) if args else 20)
    else:
        sys.exit(__doc__)
