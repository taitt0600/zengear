"""The branch name of an issue, and its check against the title of a pull request (DEC-148 (10)).

  python tools/branch_name.py make --number <n> --type <Feature|Bug|Task|Design> [--feature <ID>] [--web]
                                   [--label <label>]... [--title "<issue title>"] [--taken <merged branch>]...
  python tools/branch_name.py check <branch> "<pull request title>"

The inputs come from the issue: a cloud session reads them with the GitHub MCP, a local one with gh; --taken names a
branch of the same feature whose pull request has merged already, so that the next one takes -2, -3, ...

  Feature  feat/<feature id>[-web][-N]   title type feat
  Bug      fix/<feature id>-<issue>      title type fix
  Task     <type>/<issue>-<slug>         type: the one Conventional Commits label of the issue
  Design   docs/<issue>-<slug>

Only a-z, 0-9 and '-', at most 50 characters; the slug comes from the issue title without its diacritics and is cut
at a word. tools/pr_gates.py runs problem() on every pull request but those of dependabot[bot].
"""
import argparse
import re
import sys
import unicodedata

MAX_LENGTH = 50
TASK_TYPES = ('docs', 'ci', 'chore', 'build', 'test', 'refactor', 'perf')
FEATURE_ID = re.compile(r'[A-Z]{3}-[0-9]{2}')
SLUG = r'[a-z0-9]+(-[a-z0-9]+)*'
PATTERNS = (
    ('feat', re.compile(r'feat/(?P<fid>[a-z]{3}-[0-9]{2})(-web)?(-[2-9]|-[1-9][0-9]+)?')),
    ('fix', re.compile(r'fix/(?P<fid>[a-z]{3}-[0-9]{2})-(?P<issue>[1-9][0-9]*)')),
    (None, re.compile(rf'(?P<type>{"|".join(TASK_TYPES)})/(?P<issue>[1-9][0-9]*)(-{SLUG})?')),
)
TITLE_TYPE = re.compile(r'^([a-z]+)(\([^)]*\))?!?:')


def slug(title):
    # NFKD splits a letter from its accents; đ and Đ have no decomposition.
    text = unicodedata.normalize('NFKD', title.replace('đ', 'd').replace('Đ', 'D'))
    text = ''.join(c for c in text if not unicodedata.combining(c)).lower()
    return '-'.join(re.findall(r'[a-z0-9]+', text))


def cut(prefix, words):
    """prefix + as many whole words of the slug as fit in MAX_LENGTH."""
    name = prefix
    for word in words.split('-') if words else []:
        if len(f'{name}-{word}') > MAX_LENGTH:
            break
        name = f'{name}-{word}'
    return name


def make(number, issue_type, feature=None, web=False, labels=(), title='', taken=()):
    if issue_type in ('Feature', 'Bug'):
        if not (feature and FEATURE_ID.fullmatch(feature)):
            raise ValueError(f'a {issue_type} issue needs its Feature ID (e.g. STF-09), not {feature!r}')
        if issue_type == 'Bug':
            return f'fix/{feature.lower()}-{number}'
        base = f'feat/{feature.lower()}' + ('-web' if web else '')
        name, n = base, 1
        while name in set(taken):
            n += 1
            name = f'{base}-{n}'
        return name
    if issue_type == 'Task':
        types = [label for label in labels if label in TASK_TYPES]
        if len(types) != 1:
            raise ValueError(f'a Task issue has exactly one type label of {", ".join(TASK_TYPES)}; it has {types}')
        prefix = types[0]
    elif issue_type == 'Design':
        prefix = 'docs'
    else:
        raise ValueError(f'unknown Issue type {issue_type!r}: Feature, Bug, Task or Design')
    return cut(f'{prefix}/{number}', slug(title))


def match(branch):
    """(type of the branch, match) or (None, None)."""
    for kind, pattern in PATTERNS:
        found = pattern.fullmatch(branch)
        if found:
            return kind or found.group('type'), found
    return None, None


def problem(branch, title):
    """Why the branch does not fit DEC-148 (10) or the type of the title, or None."""
    if len(branch) > MAX_LENGTH:
        return f'the branch {branch} is longer than {MAX_LENGTH} characters'
    kind, _ = match(branch)
    if kind is None:
        return (f'the branch {branch} does not follow the pattern of DEC-148 (10): feat/<feature id>[-web][-N], '
                'fix/<feature id>-<issue> or <type>/<issue>-<slug>; make it with tools/branch_name.py')
    title_type = TITLE_TYPE.match(title)
    if not title_type or title_type.group(1) != kind:
        return f'the branch {branch} is of type {kind}, but the title of the pull request is not "{kind}…"'
    return None


def issue_number(branch):
    """The issue a Bug, Task or Design branch was made from, or None."""
    _, found = match(branch)
    return int(found.group('issue')) if found and 'issue' in found.groupdict() else None


def feature_id(branch):
    _, found = match(branch)
    return found.group('fid').upper() if found and 'fid' in found.groupdict() else None


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('make')
    p.add_argument('--number', type=int, required=True)
    p.add_argument('--type', required=True, dest='issue_type')
    p.add_argument('--feature')
    p.add_argument('--web', action='store_true')
    p.add_argument('--label', action='append', default=[])
    p.add_argument('--title', default='')
    p.add_argument('--taken', action='append', default=[])
    p = commands.add_parser('check')
    p.add_argument('branch')
    p.add_argument('title')
    args = parser.parse_args(argv)
    if args.command == 'make':
        try:
            print(make(args.number, args.issue_type, args.feature, args.web, args.label, args.title, args.taken))
        except ValueError as error:
            print(f'branch_name: {error}', file=sys.stderr)
            return 1
        return 0
    reason = problem(args.branch, args.title)
    if reason:
        print(f'branch_name: {reason}', file=sys.stderr)
        return 1
    print('branch name ok')
    return 0


if __name__ == '__main__':
    sys.exit(main())
