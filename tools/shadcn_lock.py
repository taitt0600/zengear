"""Hashes of the shadcn/ui components, so that a component changed by hand is seen (DEC-104 (5), DEC-111 (4)).

  python tools/shadcn_lock.py write [<folder>]
  python tools/shadcn_lock.py check [<folder>]

<folder> defaults to web/src/components/ui of this repository. Its lock, <folder>/.shadcn-lock.json, is a JSON object
that maps the path of every other file under the folder (relative to it, with "/") to the sha256 of its content after
CRLF is turned into LF, so that a checkout with Windows line endings hashes the same.
write records the lock after `shadcn add`. check fails when there are files but no lock, a lock but no file, a file
without an entry, an entry without a file, or a hash that differs; a folder without files and without a lock passes
(before the web shell). tools/pr_gates.py reads the lock with lock_entries to judge which changes the owner merges.

Exit codes: 0 passed or written, 1 a check failed or the lock cannot be read, 2 usage."""
import hashlib, json, sys
from pathlib import Path

FOLDER = Path(__file__).resolve().parents[1] / 'web' / 'src' / 'components' / 'ui'
LOCK = '.shadcn-lock.json'


def lock_entries(value):
    """The entries of a parsed lock; ValueError when it is not an object of path -> string."""
    if not isinstance(value, dict) or not all(isinstance(v, str) for v in value.values()):
        raise ValueError('is not a JSON object of file path -> sha256')
    return value


def digest(path):
    return hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest()


def hashes(folder):
    files = sorted(p for p in folder.rglob('*') if p.is_file() and p.relative_to(folder).as_posix() != LOCK)
    return {p.relative_to(folder).as_posix(): digest(p) for p in files}


def read_lock(path):
    try:
        return lock_entries(json.loads(path.read_bytes()))
    except (ValueError, RecursionError) as error:
        raise ValueError(f'{path} {error}') from None


def check(folder):
    found = hashes(folder) if folder.is_dir() else {}
    lock = folder / LOCK
    if not lock.is_file():
        if found:
            return [f'{folder} has files but no {LOCK}: run python tools/shadcn_lock.py write after shadcn add']
        return []
    try:
        entries = read_lock(lock)
    except ValueError as error:
        return [str(error)]
    if not found:
        return [f'{lock} exists but {folder} has no component file']
    problems = [f'{name}: no entry in {LOCK}' for name in sorted(found.keys() - entries.keys())]
    problems += [f'{name}: in {LOCK} but no such file' for name in sorted(entries.keys() - found.keys())]
    problems += [f'{name}: differs from its hash in {LOCK} (components/ui is never edited by hand)'
                 for name in sorted(found.keys() & entries.keys()) if found[name] != entries[name]]
    return problems


def write(folder):
    found = hashes(folder) if folder.is_dir() else {}
    if not found:
        return [f'{folder} has no component file to lock']
    with open(folder / LOCK, 'w', encoding='utf-8', newline='\n') as f:
        f.write(json.dumps(found, indent=2, sort_keys=True) + '\n')
    return []


if __name__ == '__main__':
    if len(sys.argv) not in (2, 3) or sys.argv[1] not in ('check', 'write'):
        print(__doc__)
        sys.exit(2)
    target = Path(sys.argv[2]) if len(sys.argv) == 3 else FOLDER
    errors = (check if sys.argv[1] == 'check' else write)(target)
    for message in errors:
        print(message)
    if not errors:
        print(f'{target / LOCK}: ' + ('every file matches' if sys.argv[1] == 'check' else 'written'))
    sys.exit(1 if errors else 0)
