"""Compare the schema the migrations build with the specification, for what the migrations have built so far.

  python tools/schema_compare.py <spec snapshot> <migrated snapshot>

Both files are outputs of tools/schema_snapshot.sql. The migrations implement the specification table by
table, so only the objects of the tables, views and functions they create are compared: for each of them the
relation, column, constraint, index and trigger lines (or the view and function line) are identical in both
files. An object the migrations create that the specification does not have is a difference too. Exit code 0
when they agree, 1 otherwise; used by the schema job of .github/workflows/pipeline.yml and by /finish-feature."""
import re, sys

OWNERS = (('relation', r'^relation (\S+) '), ('relation', r'^(?:column|constraint) ([^.\s]+)\.'),
          ('relation', r'^(?:index|trigger) .* ON public\.(\S+) '),
          ('view', r'^view (\S+) '), ('function', r'^function ([^(\s]+)\('))


def read(path):
    with open(path, encoding='utf-8') as f:
        return [line.rstrip('\n') for line in f if line.strip()]


def owner(line):
    """The relation, view or function a snapshot line belongs to, or None (extensions)."""
    for kind, pattern in OWNERS:
        match = re.match(pattern, line)
        if match:
            return f'{kind} {match[1]}'
    return None


def compare(spec_lines, migrated_lines):
    built = {owner(line) for line in migrated_lines if line.startswith(('relation ', 'view ', 'function '))}
    spec = {line for line in spec_lines if owner(line) in built}
    migrated = {line for line in migrated_lines if owner(line) in built}
    specified = {owner(line) for line in spec_lines}
    unknown = sorted(name for name in built if name not in specified)
    return sorted(spec - migrated), sorted(migrated - spec), unknown


def main(spec_path, migrated_path):
    missing, extra, unknown = compare(read(spec_path), read(migrated_path))
    for name in unknown:
        print(f'::error::the migrations create {name}, which the specification does not have')
    for line in missing:
        print(f'- {line}')
    for line in extra:
        print(f'+ {line}')
    if missing or extra or unknown:
        print(f'{len(missing)} line(s) of the specification missing, {len(extra)} line(s) not in it '
              '(- specification, + migrations)')
        return 1
    built = sum(1 for line in read(migrated_path) if line.startswith('relation '))
    print(f'the {built} relation(s) built by the migrations match the specification')
    return 0


if __name__ == '__main__':
    if len(sys.argv) != 3:
        print(__doc__)
        sys.exit(2)
    sys.exit(main(sys.argv[1], sys.argv[2]))
