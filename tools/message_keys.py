"""The message files of the web hold the same keys (DEC-104 (6), DEC-111 (7)).

  python tools/message_keys.py [<folder>]

<folder> defaults to web/messages of this repository. Every <folder>/*.json is flattened into dotted keys: a nested
object adds a level, any other value is a leaf ({"cart": {"title": "..."}} gives cart.title), and an empty object
gives no key. The check fails when a file is not a JSON object, a key contains "." (two different shapes would
flatten to the same key), or the files do not hold the same set of keys. No folder, or a single file (Phase 1 ships
vi.json only), passes. The CI job web-test runs it.

Exit codes: 0 passed, 1 a check failed or a file cannot be read, 2 usage."""
import json, sys
from pathlib import Path

FOLDER = Path(__file__).resolve().parents[1] / 'web' / 'messages'
LISTED = 20


def flatten(value, prefix=''):
    """The dotted keys of a parsed message object, and the keys that contain a dot."""
    keys, dotted = set(), set()
    for name, child in value.items():
        key = prefix + name
        if '.' in name:
            dotted.add(key)
        if isinstance(child, dict):
            more, more_dotted = flatten(child, key + '.')
            keys |= more
            dotted |= more_dotted
        else:
            keys.add(key)
    return keys, dotted


def read_keys(path):
    """The keys of one file; ValueError with a message when it cannot be read as a JSON object."""
    try:
        content = path.read_bytes()
    except OSError as error:
        raise ValueError(f'{path.name}: cannot be read ({error.strerror or error})') from None
    try:
        value = json.loads(content)
    except (ValueError, RecursionError) as error:
        raise ValueError(f'{path.name}: not valid JSON ({error})') from None
    if not isinstance(value, dict):
        raise ValueError(f'{path.name}: the top level is not a JSON object')
    try:
        return flatten(value)
    except RecursionError:
        raise ValueError(f'{path.name}: nested too deeply') from None


def listed(keys):
    names = sorted(keys)
    more = f', and {len(names) - LISTED} more' if len(names) > LISTED else ''
    return ', '.join(names[:LISTED]) + more


def check(folder):
    files = sorted(folder.glob('*.json')) if folder.is_dir() else []
    problems, found = [], {}
    for path in files:
        try:
            keys, dotted = read_keys(path)
        except ValueError as error:
            problems.append(str(error))
            continue
        if dotted:
            problems.append(f'{path.name}: a key contains ".": {listed(dotted)}')
        found[path.name] = keys
    every = set().union(*found.values())
    for name, keys in found.items():
        if every - keys:
            problems.append(f'{name}: missing {listed(every - keys)}')
    return files, problems


if __name__ == '__main__':
    sys.stdout.reconfigure(encoding='utf-8')   # redirected output on Windows uses the ANSI code page
    if len(sys.argv) > 2 or (len(sys.argv) == 2 and sys.argv[1].startswith('-')):
        print(__doc__)
        sys.exit(2)
    target = Path(sys.argv[1]) if len(sys.argv) == 2 else FOLDER
    files, errors = check(target)
    for message in errors:
        print(message)
    if not errors:
        print(f'{target}: ' + ('no message file' if not files else '1 message file' if len(files) == 1
                               else f'{len(files)} message files hold the same keys'))
    sys.exit(1 if errors else 0)
