"""Structured comments of ZenGear and their hidden markers (DEC-148 (8), (9), DEC-150 (3), DEC-151 (1)).

  python tools/comment_templates.py handoff --feature <ID> --step <n> --branch <branch> --next "<command>" [--notes <text>]
  python tools/comment_templates.py code-review --sha <head sha> --verdict pass|blocking [--summary <text>]
  python tools/comment_templates.py blocked-by <issue> | unblock <issue>
  python tools/comment_templates.py log <merged|needs-decision|token|re-entry|reminder|handover> --text "<line>"

Each command prints the comment body; post it with `tools/github_pr.py comment` or the GitHub MCP. A structured comment
starts with one marker line `<!-- zengear:<kind> key=value ... -->`; parse() reads it only when it is the only marker,
at the start of a line outside a code fence or quote, with exactly the keys of its kind and valid values. Free comments
are not checked, and the footer that a cloud session adds after the text does not change the marker (DEC-149 (5)).
The workflows code-review, issue-deps and issue-lint and tools/issue.py read the markers with parse().
"""
import argparse
import re
import sys

SHA = re.compile(r'[0-9a-f]{40}')
NUMBER = re.compile(r'[1-9][0-9]{0,9}')
FEATURE = re.compile(r'[A-Z]{3}-[0-9]{2}')
LOG_LINES = ('merged', 'needs-decision', 'token', 're-entry', 'reminder', 'handover')
# The keys of each kind and the pattern of each value.
KINDS = {
    'handoff': {'feature': FEATURE, 'step': NUMBER},
    'code-review': {'sha': SHA, 'verdict': re.compile(r'pass|blocking')},
    'blocked-by': {'issue': NUMBER},
    'unblock': {'issue': NUMBER},
    'blocked-by-result': {'request': NUMBER, 'result': re.compile(r'ok|error|refused')},
    'issue-lint': {},
    'log': {'line': re.compile('|'.join(LOG_LINES))},
}
MARKER = re.compile(r'<!-- zengear:(?P<kind>[a-z-]+)(?P<pairs>(?: [a-z]+=[^\s=]+)*) -->')
ANY_MARKER = re.compile(r'<!--\s*zengear:')


def marker(kind, **fields):
    keys = KINDS[kind]
    values = {key: str(value) for key, value in fields.items()}
    if set(values) != set(keys) or not all(keys[key].fullmatch(value) for key, value in values.items()):
        raise ValueError(f'invalid {kind} marker: {values}')
    return f'<!-- zengear:{kind}' + ''.join(f' {key}={values[key]}' for key in keys) + ' -->'


def parse(body):
    """(kind, {key: value}) of the one valid marker of a comment, or None."""
    if not body:
        return None
    found, fence = [], False
    for line in body.replace('\r\n', '\n').split('\n'):
        if line.lstrip().startswith(('```', '~~~')):
            fence = not fence
            continue
        if fence:
            continue
        if ANY_MARKER.search(line):
            found.append(line.rstrip())
    if len(found) != 1:
        return None
    match = MARKER.fullmatch(found[0])
    if not match or match.group('kind') not in KINDS:
        return None
    pairs = [pair.split('=', 1) for pair in match.group('pairs').split()]
    fields = dict(pairs)
    keys = KINDS[match.group('kind')]
    if len(fields) != len(pairs) or set(fields) != set(keys):
        return None
    if not all(keys[key].fullmatch(value) for key, value in fields.items()):
        return None
    return match.group('kind'), fields


def handoff(feature, step, branch='', next_command='', notes=''):
    lines = [marker('handoff', feature=feature, step=step),
             f'**Handoff of {feature}**: `/implement-feature` stopped after step {step}; the code is on the owner\'s '
             'machine until `/finish-feature` commits it (DEC-100, DEC-112 (4)).']
    if branch:
        lines.append(f'- Branch: `{branch}` (not pushed yet)')
    if next_command:
        lines.append(f'- Next: `/clear`, then `{next_command}` in a local session')
    if notes:
        lines.append(f'- {notes}')
    return '\n'.join(lines) + '\n'


def code_review(sha, verdict, summary=''):
    head = marker('code-review', sha=sha, verdict=verdict)
    word = 'pass' if verdict == 'pass' else 'Blocking items left'
    text = f'**code-reviewer on `{sha[:12]}`**: {word}.'
    return '\n'.join([head, text] + ([summary] if summary else [])) + '\n'


def blocked_by(issue):
    return (f'{marker("blocked-by", issue=issue)}\nThis issue is blocked by #{issue}; the workflow issue-deps records '
            'the relation (DEC-151).\n')


def unblock(issue):
    return f'{marker("unblock", issue=issue)}\nThis issue is no longer blocked by #{issue} (DEC-151).\n'


def blocked_by_result(request, result, detail=''):
    return f'{marker("blocked-by-result", request=request, result=result)}\nissue-deps: {result}. {detail}'.rstrip() + '\n'


def issue_lint(missing):
    items = '\n'.join(f'- {item}' for item in missing)
    return (f'{marker("issue-lint")}\nThis issue does not follow its form in `.github/ISSUE_TEMPLATE/` (DEC-148 (9)); '
            f'fix these, and the label `invalid` goes away:\n{items}\n')


def log_line(line, text):
    return f'{marker("log", line=line)}\n{text}\n'


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('handoff')
    p.add_argument('--feature', required=True)
    p.add_argument('--step', type=int, required=True)
    p.add_argument('--branch', default='')
    p.add_argument('--next', default='', dest='next_command')
    p.add_argument('--notes', default='')
    p = commands.add_parser('code-review')
    p.add_argument('--sha', required=True)
    p.add_argument('--verdict', required=True)
    p.add_argument('--summary', default='')
    for name in ('blocked-by', 'unblock'):
        commands.add_parser(name).add_argument('issue', type=int)
    p = commands.add_parser('log')
    p.add_argument('line', choices=LOG_LINES)
    p.add_argument('--text', required=True)
    args = parser.parse_args(argv)
    try:
        if args.command == 'handoff':
            body = handoff(args.feature, args.step, args.branch, args.next_command, args.notes)
        elif args.command == 'code-review':
            body = code_review(args.sha, args.verdict, args.summary)
        elif args.command == 'blocked-by':
            body = blocked_by(args.issue)
        elif args.command == 'unblock':
            body = unblock(args.issue)
        else:
            body = log_line(args.line, args.text)
    except ValueError as error:
        print(f'comment_templates: {error}', file=sys.stderr)
        return 1
    sys.stdout.reconfigure(encoding='utf-8')
    sys.stdout.write(body)
    return 0


if __name__ == '__main__':
    sys.exit(main())
