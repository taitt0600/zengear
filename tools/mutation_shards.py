"""Split the changed source files of web/src into the shards of StrykerJS (DEC-152 (7), Task #296).

  git -c core.quotePath=false diff --name-only --diff-filter=AMR origin/<base>...HEAD -- web/src \
    | python3 tools/mutation_shards.py --root web

StrykerJS on one runner tested about 0.65 mutant per second (pull request #292: 930 mutants, 22 minutes), and a
feature-sized pull request changes 12 to 25 files of web/src, so the job pipeline.yml runs it in a matrix: this script
picks the files StrykerJS mutates (the `mutate` list of <root>/stryker.config.json, its only source: a file must match
a positive pattern and no negated one) among the paths read on stdin, weighs each by its non-blank lines, and spreads
them over ceil(lines / LINES_PER_SHARD) shards, at most MAX_SHARDS, the heaviest file first onto the lightest shard.
Stryker's incremental mode was measured and rejected: it never reuses the mutants of a new file (Task #296).

Output, one JSON object on stdout: {"count": n, "shards": [0, ..., n-1], "files": ["a.ts,b.tsx", ...]}, the files
relative to <root>, one comma-joined string per shard as `stryker run --mutate` takes it. No file: count 0.
Exit 2 for a mutate list this script cannot read as Stryker does (a glob beyond **, *, ? and {a,b}, or a positive
glob after a negated one), or a mutated path with a comma (it would split the list) or a [ or ] (StrykerJS reads
--mutate as globs). A path git quoted (a ", \ or control character in the name) is skipped with a ::warning:: on
stderr, which keeps stdout the JSON the workflow parses.
"""
import argparse
import json
import math
import os
import re
import sys

LINES_PER_SHARD = 250   # about 120 to 180 mutants (0.48 to 0.71 per line on #117 and #292), 3 to 4.5 minutes
MAX_SHARDS = 6          # with the other jobs of a pull request, under the 20 concurrent jobs of the Free plan
UNSUPPORTED = re.compile(r'[\[\]()!+@]')


class GlobError(ValueError):
    pass


def glob_regex(pattern):
    """The regex of a glob of the forms the config uses: **, *, ?, {a,b}. Anything else is refused.
    As in Stryker (minimatch with dot: false), a wildcard never matches a name that starts with a dot."""
    if UNSUPPORTED.search(pattern):
        raise GlobError(f'unsupported glob {pattern!r}')
    out, i = [], 0
    while i < len(pattern):
        segment_start = i == 0 or pattern[i - 1] == '/'
        if pattern.startswith('**/', i):
            out.append(r'(?:(?!\.)[^/]*/)*')
            i += 3
        elif pattern.startswith('**', i):
            out.append(r'(?:(?!\.)[^/]*(?:/|\Z))*')
            i += 2
        elif pattern[i] == '*':
            out.append(r'(?!\.)[^/]*' if segment_start else '[^/]*')
            i += 1
        elif pattern[i] == '?':
            out.append('[^/]')
            i += 1
        elif pattern[i] == '{':
            end = pattern.find('}', i)
            if end < 0 or '{' in pattern[i + 1:end]:
                raise GlobError(f'unsupported glob {pattern!r}')
            out.append('(?:' + '|'.join(re.escape(part) for part in pattern[i + 1:end].split(',')) + ')')
            i = end + 1
        elif pattern[i] in '},':
            raise GlobError(f'unsupported glob {pattern!r}')
        else:
            out.append(re.escape(pattern[i]))
            i += 1
    return re.compile(''.join(out) + r'\Z')


def selector(mutate):
    """A function that tells whether a path relative to the root is mutated by the `mutate` list. Stryker applies the
    patterns in order, so a positive pattern after a negated one could add a file back: such a list is refused."""
    positive, negative = [], []
    for pattern in mutate:
        if pattern.startswith('!'):
            negative.append(glob_regex(pattern[1:]))
        elif negative:
            raise GlobError(f'positive glob {pattern!r} after a negated one')
        else:
            positive.append(glob_regex(pattern))
    return lambda path: any(p.match(path) for p in positive) and not any(n.match(path) for n in negative)


def weight(path):
    with open(path, encoding='utf-8') as file:
        return max(1, sum(1 for line in file if line.strip()))


def partition(weights):
    """[[path, ...], ...]: ceil(total / LINES_PER_SHARD) shards, 1 to MAX_SHARDS, heaviest file first."""
    if not weights:
        return []
    count = max(1, min(MAX_SHARDS, len(weights), math.ceil(sum(weights.values()) / LINES_PER_SHARD)))
    shards = [[0, []] for _ in range(count)]
    for path in sorted(weights, key=lambda p: (-weights[p], p)):
        lightest = min(shards, key=lambda s: s[0])
        lightest[0] += weights[path]
        lightest[1].append(path)
    return [sorted(files) for _, files in shards]


def main(argv=None, stdin=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--root', required=True, help='the folder of stryker.config.json, relative to the repository')
    args = parser.parse_args(argv)
    root = args.root.rstrip('/') + '/'
    try:
        with open(os.path.join(args.root, 'stryker.config.json'), encoding='utf-8') as file:
            mutated = selector(json.load(file)['mutate'])
    except GlobError as error:
        print(f'::error::web/stryker.config.json: {error}; tools/mutation_shards.py reads only **, *, ? and {{a,b}},'
              ' the negated globs after the positive ones')
        return 2
    weights = {}
    for line in (stdin or sys.stdin).read().splitlines():
        path = line.strip()
        if path.startswith('"' + root):
            print(f'::warning::{path}: git quoted this path (a ", \\ or control character in the name);'
                  ' StrykerJS does not mutate it', file=sys.stderr)
            continue
        if not path.startswith(root):
            continue
        relative = path[len(root):]
        if not mutated(relative):
            continue
        if ',' in relative:
            print(f'::error::{path}: a comma in a path would split the --mutate list of StrykerJS')
            return 2
        if '[' in relative or ']' in relative:
            print(f'::error::{path}: StrykerJS reads --mutate as globs, a [ or ] in a path would be a character class')
            return 2
        weights[relative] = weight(path)
    shards = partition(weights)
    print(json.dumps({'count': len(shards), 'shards': list(range(len(shards))),
                      'files': [','.join(files) for files in shards]}))
    return 0


if __name__ == '__main__':
    sys.exit(main())
