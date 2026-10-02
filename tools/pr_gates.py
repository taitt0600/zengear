"""Pull request gates that stand in for a person's approval (CLAUDE.md, Workflow).

  python tools/pr_gates.py check <base sha> <head sha> "<pull request title>" [<app that opened it>]
  python tools/pr_gates.py coverage <Feature ID>

check, run by .github/workflows/gates.yml on every pull request:
  1. Rule coverage: a feat or fix pull request that changes api/src/ or web/ ends its title with a feature ID,
     e.g. "(STF-01)"; every rule of that feature (its Rules column on main and in the pull request) has a test
     method whose name starts with the rule ID (R-USR-01 -> R_USR_01_...).
  2. Specification: a commit that changes api/db/, docs/blueprint/, docs/DATABASE.md or the blueprint workbook,
     or adds a package to api/Directory.Packages.props, names a decision (DEC-xxx) with the status Accepted in
     docs/blueprint/decisions.md.
  3. Build files: a commit that adds an element that runs code at build time (Exec, Target, UsingTask, Import,
     PreBuildEvent, PostBuildEvent, Analyzer, Sdk) to a *.csproj, *.props or *.targets file names a decision the
     same way.
  4. Commit identity: no commit is authored or committed as Claude (noreply@anthropic.com), and no commit
     message carries a Co-Authored-By or Claude-Session trailer (CLAUDE.md, Commits).
  5. Owner merges: a pull request opened through a GitHub App (a Claude Code cloud session opens it through the
     Claude app) changes no file of owner_paths; the owner opens those pull requests on GitHub.
check reads the pull request from git objects only: nothing of the pull request is checked out or run.
coverage reads the working tree, new files included (used by /implement-feature and /finish-feature).
owner_paths lists the changed files that only the owner merges (tools/github_pr.py refuses auto-merge for them)."""
import re, subprocess, sys
import xml.etree.ElementTree as ET

SPEC_PATHS = ('api/db/', 'docs/blueprint/', 'docs/DATABASE.md', 'docs/product-domain-blueprint-MVP.xlsx')
BUILD_SUFFIXES = ('.csproj', '.props', '.targets')
# What decides how the code is built, checked or merged: changed only in pull requests the owner merges.
OWNER_PATHS = SPEC_PATHS + ('.claude/', '.githooks/', '.github/', '.gitleaks.toml', '.mcp.json', '.config/', 'tools/',
                            'docs/ARCHITECTURE.md', 'api/tests/ZenGear.ArchitectureTests/', 'api/global.json',
                            'api/.config/', 'api/.editorconfig', 'nuget.config', 'NuGet.Config')
OWNER_SUFFIXES = BUILD_SUFFIXES + ('.rsp', '.dcproj', '.sln', '.slnx')
CODE_PATHS = ('api/src/', 'web/')
FEATURES = 'docs/blueprint/features.md'
DECISIONS = 'docs/blueprint/decisions.md'
PACKAGES = 'api/Directory.Packages.props'
RUNS_CODE = re.compile(r'<\s*(Exec|Target|UsingTask|Import|PreBuildEvent|PostBuildEvent|Analyzer|Sdk)\b|\bSdk\s*=')
FEATURE_IN_TITLE = re.compile(r'\(([A-Z]{3}-\d{2})\)\s*$')
CODE_TITLE = re.compile(r'^(feat|fix)(\([^)]*\))?!?:')
DECISION = re.compile(r'\bDEC-\d{3}\b')
TEST_METHOD = re.compile(r'\b(?:Task|ValueTask|void)\s+(R_[A-Z]+_\d+)_\w*\s*\(')
AGENT_EMAIL = 'noreply@anthropic.com'
AGENT_TRAILER = re.compile(r'^\s*(Co-Authored-By|Claude-Session)\s*:', re.IGNORECASE | re.MULTILINE)


def fail(message):
    # An error annotation, since the job log cannot be read from a cloud session.
    print(f'::error::{message}')
    sys.exit(1)


def git(*args, ok=(0,)):
    run = subprocess.run(['git', *args], capture_output=True, text=True, encoding='utf-8')
    if run.returncode not in ok:
        fail(f'git {" ".join(args)} failed: {run.stderr.strip()}')
    return run.stdout


def show(rev, path):
    """A file at a revision, or in the working tree when rev is None; '' when it does not exist."""
    if rev is None:
        try:
            with open(path, encoding='utf-8') as f:
                return f.read()
        except FileNotFoundError:
            return ''
    return git('show', f'{rev}:{path}', ok=(0, 128))


def cells(line):
    # Table cells of the exported blueprint; "\|" is a pipe inside a cell.
    return [c.strip() for c in re.split(r'(?<!\\)\|', line)[1:-1]]


def feature_rules(feature, rev):
    for line in show(rev, FEATURES).splitlines():
        row = cells(line) if line.startswith('| ') else []
        if row and row[0] == feature:
            return set(re.findall(r'\bR-[A-Z]+-\d+\b', row[5]))
    return None


def accepted_decisions(rev):
    rows = (cells(line) for line in show(rev, DECISIONS).splitlines() if line.startswith('| DEC-'))
    return {row[0] for row in rows if len(row) == 8 and row[7] == 'Accepted'}


def tested_rules(rev):
    # Exit code 1 of git grep means no match.
    where = [rev] if rev else []
    options = [] if rev else ['--untracked']
    text = git('grep', *options, '-h', '-E', r'R_[A-Z]+_[0-9]+_', *where, '--', 'api/tests/*.cs', ok=(0, 1))
    return {name.replace('_', '-') for name in TEST_METHOD.findall(text)}


def uncovered(feature, revs):
    """Rules of the feature in any of the revisions without a test at the last one; None if no revision has it."""
    found = [rules for rules in (feature_rules(feature, rev) for rev in revs) if rules is not None]
    if not found:
        return None
    tested = tested_rules(revs[-1])
    return sorted(rule for rule in set().union(*found) if rule not in tested)


def packages(text):
    try:
        root = ET.fromstring(text) if text.strip() else None
    except ET.ParseError:
        return {'<unreadable Directory.Packages.props>'}
    return set() if root is None else {e.get('Include') for e in root.iter() if e.get('Include')}


def owner_paths(paths):
    return sorted(p for p in paths if p.startswith(OWNER_PATHS) or p.endswith(OWNER_SUFFIXES))


def commits(base, head):
    """(hash, author email, committer email, message, changed files) of every commit of the pull request."""
    result = []
    for record in git('log', '--format=%h%x1f%ae%x1f%ce%x1f%B%x1e', f'{base}..{head}').split('\x1e'):
        if not record.strip():
            continue
        fields = record.lstrip('\n').split('\x1f', 3)
        if len(fields) != 4:
            fail(f'unreadable commit record: {record[:80]!r}')
        changed = git('diff-tree', '--no-commit-id', '--name-only', '-r', '--no-renames', '--root', fields[0]).split()
        result.append((*fields, changed))
    return result


def check(base, head, title, app=''):
    for rev in (base, head):
        git('rev-parse', '--verify', '--quiet', f'{rev}^{{commit}}')
    errors = []
    changed = git('diff', '--name-only', '--no-renames', f'{base}...{head}').split()

    feature = FEATURE_IN_TITLE.search(title)
    if feature:
        missing = uncovered(feature.group(1), [base, head])
        if missing is None:
            errors.append(f'{feature.group(1)} is not in {FEATURES}')
        elif missing:
            errors.append(f'{feature.group(1)}: no test named after {", ".join(missing)}')
    elif CODE_TITLE.match(title) and any(path.startswith(CODE_PATHS) for path in changed):
        errors.append('a feat or fix pull request that changes code ends its title with the feature ID, '
                      'e.g. "(STF-01)", so that its rules are checked')

    accepted = accepted_decisions(head)
    needs = 'without an accepted decision (DEC-xxx) in its message'
    for commit, author, committer, message, files in commits(base, head):
        decided = bool(set(DECISION.findall(message)) & accepted)
        spec = [path for path in files if path.startswith(SPEC_PATHS)]
        build = [path for path in files if path.endswith(BUILD_SUFFIXES) and any(
            line.startswith('+') and RUNS_CODE.search(line)
            for line in git('show', '--format=', '--no-renames', commit, '--', path).splitlines())]
        added = sorted(packages(show(commit, PACKAGES)) - packages(show(f'{commit}^', PACKAGES))) \
            if PACKAGES in files else []
        if not decided:
            if spec:
                errors.append(f'commit {commit} changes the specification {needs}: {", ".join(spec)}')
            if added:
                errors.append(f'commit {commit} adds packages {needs}: {", ".join(added)}')
            if build:
                errors.append(f'commit {commit} adds build code {needs}: {", ".join(build)}')
        if AGENT_EMAIL in (author.lower(), committer.lower()):
            errors.append(f'commit {commit} is authored or committed as Claude ({AGENT_EMAIL}): '
                          'set git user.name and user.email to the owner (CLAUDE.md, Commits)')
        if AGENT_TRAILER.search(message):
            errors.append(f'commit {commit} has a Co-Authored-By or Claude-Session trailer (CLAUDE.md, Commits)')

    owned = owner_paths(changed)
    if owned and app:
        errors.append(f'opened through the {app} app, but changes files the owner merges: {", ".join(owned)}; '
                      'the owner opens this pull request on GitHub')
    for path in owned:
        print(f'::notice::merged by the owner, not by auto-merge: {path}')
    for error in errors:
        print(f'::error::{error}')
    if not errors:
        print('PR gates passed' + (f' (rules of {feature.group(1)} covered)' if feature else ''))
    return 1 if errors else 0


def coverage(feature):
    missing = uncovered(feature, [None])
    if missing is None:
        print(f'{feature} is not in {FEATURES}')
        return 2
    print('\n'.join(missing) if missing else f'every rule of {feature} has a test')
    return 1 if missing else 0


if __name__ == '__main__':
    if len(sys.argv) in (5, 6) and sys.argv[1] == 'check':
        sys.exit(check(*sys.argv[2:]))
    if len(sys.argv) == 3 and sys.argv[1] == 'coverage':
        sys.exit(coverage(sys.argv[2]))
    print(__doc__)
    sys.exit(2)
