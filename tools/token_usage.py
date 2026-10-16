"""Tokens of a run (DEC-100, DEC-110, docs/AUTOMATION.md, Metrics): the model calls of a branch or a session, read
from this project's Claude Code transcripts, for the main session and for subagents apart.
Usage, from any folder of the repository:
  python tools/token_usage.py --branch feat/prd-03          (a table, with a part per agent type)
  python tools/token_usage.py --branch feat/prd-03 --line   (the line "Token:" of the acceptance report)
  python tools/token_usage.py --session <id or prefix> [--dir <transcript folder>]

Transcripts, as observed on Claude Code 2.1 (not documented): ~/.claude/projects/<repository path with every
character that is not a letter or a digit turned into '-'>/ holds <session>.jsonl, and
<session>/subagents/agent-<id>.jsonl with agent-<id>.meta.json (its agentType). A model call is an 'assistant' line;
one call is written on as many lines as it has content blocks, all with the same message.id, so a call counts once
per message.id, with the largest value of each usage field (a subagent's lines carry its output tokens as they grow).
Each line carries gitBranch: lines on main before 'git switch -c' are not counted. A session started in another
folder (a worktree) has its own project folder: pass it with --dir.
Only numbers, ids, branch, agent types and model names are printed, never the text of a transcript."""
import argparse, json, re, sys
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[1]
FIELDS = ('input_tokens', 'cache_creation_input_tokens', 'cache_read_input_tokens', 'output_tokens')
NOT_A_CALL = '<synthetic>'
NAME = re.compile(r'[\w.:/-]{1,80}')

def default_dir():
    return Path.home() / '.claude' / 'projects' / re.sub(r'[^A-Za-z0-9]', '-', str(REPO))

def name(value):
    """A model or agent type as printed: a plain name, or '?' for anything else."""
    return value if isinstance(value, str) and NAME.fullmatch(value) else '?'

def number(value):
    return value if isinstance(value, int) and not isinstance(value, bool) and value >= 0 else 0

class Group:
    def __init__(self):
        self.calls = self.runs = self.no_usage = self.peak = 0
        self.tokens = dict.fromkeys(FIELDS, 0)
        self.models = set()

    def add(self, model, counts):
        self.calls += 1
        self.models.add(model)
        if counts is None:
            self.no_usage += 1
            return
        for field, count in counts.items():
            self.tokens[field] += count
        self.peak = max(self.peak, counts['input_tokens'] + counts['cache_creation_input_tokens']
                        + counts['cache_read_input_tokens'])

    def merged(self, other):
        group = Group()
        for part in (self, other):
            group.calls += part.calls
            group.no_usage += part.no_usage
            group.peak = max(group.peak, part.peak)
            group.models |= part.models
            for field in FIELDS:
                group.tokens[field] += part.tokens[field]
        return group

    @property
    def total(self):
        return sum(self.tokens.values())

    @property
    def output(self):
        return self.tokens['output_tokens']

    @property
    def fresh(self):
        return self.tokens['input_tokens'] + self.tokens['cache_creation_input_tokens']

def agent_type(path):
    try:
        meta = json.loads(path.with_suffix('.meta.json').read_text(encoding='utf-8'))
    except (OSError, ValueError):
        return '?'
    return name(meta.get('agentType')) if isinstance(meta, dict) else '?'

def transcripts(folder, session=None):
    """(session id, path, agent type or None for a main session) of the selected files."""
    mains = sorted(folder.glob('*.jsonl'))
    if session is not None:
        mains = [path for path in mains if path.stem.startswith(session)]
        if len(mains) != 1:
            found = ', '.join(path.stem for path in mains) or 'none'
            raise LookupError(f'session {session} must match one session; matching: {found}')
    for path in mains:
        yield path.stem, path, None
    for path in sorted(folder.glob('*/subagents/**/*.jsonl')):
        owner = path.relative_to(folder).parts[0]
        if session is None or owner == mains[0].stem:
            yield owner, path, agent_type(path)

def calls(path, branch, stats):
    """(message id, model, token counts or None) of each line of a model call in the file, on the branch when one is
    given."""
    with open(path, encoding='utf-8', errors='replace') as lines:
        for line in lines:
            if not line.strip():
                continue
            try:
                entry = json.loads(line)
            except ValueError:
                stats['skipped'] += 1
                continue
            if not isinstance(entry, dict):
                stats['skipped'] += 1
                continue
            if entry.get('type') != 'assistant' or (branch and entry.get('gitBranch') != branch):
                continue
            message = entry.get('message')
            if not isinstance(message, dict) or not isinstance(message.get('id'), str):
                stats['skipped'] += 1
                continue
            if message.get('model') == NOT_A_CALL:
                continue
            usage = message.get('usage')
            counts = {field: number(usage.get(field)) for field in FIELDS} if isinstance(usage, dict) else None
            yield message['id'], name(message.get('model')), counts

def measure(folder, branch=None, session=None):
    """Groups 'main' and 'agent', a group per agent type, and the counts of sessions and skipped lines. A call belongs
    to the first file that holds it; its counts are the largest of its lines."""
    groups, types, stats, found, sessions = {'main': Group(), 'agent': Group()}, {}, Counter(), {}, set()
    for owner, path, kind in transcripts(folder, session):
        parts = [groups['main']] if kind is None else [groups['agent'], types.setdefault(kind, Group())]
        counted = False
        for message_id, model, counts in calls(path, branch, stats):
            if message_id not in found:
                found[message_id] = [parts, model, None]
                counted = True
            known = found[message_id]
            if counts is not None:
                known[2] = counts if known[2] is None else {f: max(known[2][f], counts[f]) for f in FIELDS}
        if counted:
            sessions.add(owner)
            if kind is not None:
                types[kind].runs += 1
    for parts, model, counts in found.values():
        for group in parts:
            group.add(model, counts)
    stats['sessions'] = len(sessions)
    return groups, {kind: types[kind] for kind in sorted(types) if types[kind].calls}, stats

def line(groups):
    main, agent = groups['main'], groups['agent']
    total = main.merged(agent)
    return (f'Token: {total.total} (chính {main.total} / agent {agent.total}; output {total.output}, '
            f'ghi mới {total.fresh}), {total.calls} lần gọi, context lớn nhất {total.peak}')

def table(selection, groups, types, stats):
    rows = [*groups.items(), ('total', groups['main'].merged(groups['agent']))]
    print(f'Tokens of {selection}: {stats["sessions"]} sessions, {stats["skipped"]} lines skipped, '
          f'{rows[-1][1].no_usage} calls without usage')
    print(f'  {"":6} {"calls":>6} {"input":>10} {"cache new":>11} {"cache read":>12} {"output":>9} {"fresh":>10} '
          f'{"total":>12} {"peak":>8}  models')
    for label, group in rows:
        tokens = [group.tokens[field] for field in FIELDS]
        print(f'  {label:6} {group.calls:6} {tokens[0]:10} {tokens[1]:11} {tokens[2]:12} {tokens[3]:9} '
              f'{group.fresh:10} {group.total:12} {group.peak:8}  {", ".join(sorted(group.models)) or "-"}')
    print('By agent type:')
    print(f'  {"type":20} {"runs":>5} {"calls":>6} {"total":>12} {"output":>9}  models')
    for kind, group in types.items():
        print(f'  {kind:20} {group.runs:5} {group.calls:6} {group.total:12} {group.output:9}  '
              f'{", ".join(sorted(group.models))}')

def main(argv):
    for stream in (sys.stdout, sys.stderr):
        # Vietnamese text: never depend on the console code page (Windows).
        if hasattr(stream, 'reconfigure'):
            stream.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description=__doc__.split('\n')[0])
    which = parser.add_mutually_exclusive_group(required=True)
    which.add_argument('--branch')
    which.add_argument('--session')
    parser.add_argument('--line', action='store_true', help='print only the line "Token:" of the report')
    parser.add_argument('--dir', type=Path, default=None, help='transcript folder of the project')
    args = parser.parse_args(argv)
    folder = args.dir or default_dir()
    selection = f'branch {args.branch}' if args.branch else f'session {args.session}'
    if not folder.is_dir():
        print(f'token_usage: no transcript folder {folder}', file=sys.stderr)
        return 2
    try:
        groups, types, stats = measure(folder, branch=args.branch, session=args.session)
    except LookupError as error:
        print(f'token_usage: {error}', file=sys.stderr)
        return 1
    except OSError as error:
        print(f'token_usage: cannot read the transcripts ({error.strerror})', file=sys.stderr)
        return 2
    if not groups['main'].calls and not groups['agent'].calls:
        print(f'token_usage: no model call of {selection} in {folder}', file=sys.stderr)
        return 1
    if args.line:
        print(line(groups))
    else:
        table(selection, groups, types, stats)
    return 0

if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
