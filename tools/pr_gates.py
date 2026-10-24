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
  6. Work state: a feat pull request whose title ends with a feature ID leaves no row of docs/STATUS.md at the head
     that names that feature (a Next row keyed by it, a Waiting row triggered "before <ID>"; DEC-110 (5)); the pull
     request that does the work deletes it (CLAUDE.md, Work state). fix pull requests and other titles are not
     checked.
  7. Web packages (DEC-104 (2), DEC-111 (3)), read at the head as parsed JSON: every entry of
     web/approved-packages.json cites a decision Accepted at the head; every name in the dependencies,
     devDependencies, optionalDependencies and peerDependencies of web/package.json is in that file, with an exact
     semver (no range, tag, npm: alias, git, file: or URL); web/package-lock.json exists with lockfileVersion 3, and
     each of its packages entries but the root has integrity and resolves to
     https://registry.npmjs.org/<name>/-/<unscoped name>-<version>.tgz, <name> being the key after its last
     node_modules/. An alias (a "name" that differs), a link (unless web/package.json has workspaces) and a top-level
     inBundle entry are refused; a nested inBundle entry is skipped, since the tarball of its parent holds it.
     web/npm-shrinkwrap.json, which npm would install from instead of the lockfile, is refused.
check reads the pull request from git objects only: nothing of the pull request is checked out or run.
coverage reads the working tree, new files included (used by /implement-feature and /finish-feature).
owner_paths lists the changed files that only the owner merges (tools/github_pr.py refuses auto-merge for them): by
name, and by content between the merge-base and the head for web/package.json (owner merged unless it only adds
approved packages or changes the exact version of one, DEC-111 (4))."""
import json, re, subprocess, sys
import xml.etree.ElementTree as ET

SPEC_PATHS = ('api/db/', 'docs/blueprint/', 'docs/DATABASE.md', 'docs/product-domain-blueprint-MVP.xlsx')
BUILD_SUFFIXES = ('.csproj', '.props', '.targets')
# What decides how the code is built, checked or merged: changed only in pull requests the owner merges.
OWNER_PATHS = SPEC_PATHS + ('.claude/', '.githooks/', '.github/', '.gitleaks.toml', '.mcp.json', '.config/', 'tools/',
                            'docs/ARCHITECTURE.md', 'api/tests/ZenGear.ArchitectureTests/', 'api/global.json',
                            'api/.config/', 'api/.editorconfig', 'nuget.config', 'NuGet.Config',
                            # The web build files of DEC-104 (3); web/package.json goes by content.
                            'web/approved-packages.json', 'web/npm-shrinkwrap.json', 'web/tsconfig.json',
                            'web/components.json', 'web/.npmrc',
                            'web/.nvmrc', 'web/next.config.', 'web/eslint.config.', 'web/postcss.config.',
                            'web/vitest.config.', 'web/playwright.config.')
OWNER_SUFFIXES = BUILD_SUFFIXES + ('.rsp', '.dcproj', '.sln', '.slnx')
CODE_PATHS = ('api/src/', 'web/')
FEATURES = 'docs/blueprint/features.md'
DECISIONS = 'docs/blueprint/decisions.md'
PACKAGES = 'api/Directory.Packages.props'
STATUS = 'docs/STATUS.md'
WEB_MANIFEST = 'web/package.json'
WEB_LOCK = 'web/package-lock.json'
APPROVED = 'web/approved-packages.json'
SECTIONS = ('dependencies', 'devDependencies', 'optionalDependencies', 'peerDependencies')
REGISTRY = 'https://registry.npmjs.org/'
# ASCII digits only: \d of a str pattern also matches other Unicode digits.
EXACT = re.compile(r'(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)'
                   r'(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?(\+[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?')
# A name of a lockfile key: capital letters stay valid in legacy names (JSONStream, Base64), which npm still serves;
# scopes are kept lowercase (assumed, not checked against npm). resolved still ties the name to its own tarball, and
# lock_errors refuses keys that differ only in case, which would share a folder on Windows and macOS.
NPM_NAME = re.compile(r'(@[a-z0-9][a-z0-9._~-]*/)?[A-Za-z0-9][A-Za-z0-9._~-]*')
DECISION_ID = re.compile(r'DEC-[0-9]{3}')
# npm installs from npm-shrinkwrap.json instead of package-lock.json when both exist.
WEB_SHRINKWRAP = 'web/npm-shrinkwrap.json'
MAX_ERRORS = 20  # per file, so that a broken lockfile still gives a readable log
MISSING = object()
# Tables of docs/STATUS.md: (heading, column, value of that column in a row that names the feature).
WORK_STATE = (('Next', 'Key', '{}'), ('Waiting for a trigger', 'Trigger', 'before {}'))
RUNS_CODE = re.compile(r'<\s*(Exec|Target|UsingTask|Import|PreBuildEvent|PostBuildEvent|Analyzer|Sdk)\b|\bSdk\s*=')
FEATURE_IN_TITLE = re.compile(r'\(([A-Z]{3}-\d{2})\)\s*$')
CODE_TITLE = re.compile(r'^(feat|fix)(\([^)]*\))?!?:')
FEAT_TITLE = re.compile(r'^feat(\([^)]*\))?!?:')
HEADING = re.compile(r'^#{1,6}\s+(.*\S)')
DECISION = re.compile(r'\bDEC-\d{3}\b')
TEST_METHOD = re.compile(r'\b(?:Task|ValueTask|void)\s+(R_[A-Z]+_\d+)_\w*\s*\(')
AGENT_EMAIL = 'noreply@anthropic.com'
AGENT_TRAILER = re.compile(r'^\s*(Co-Authored-By|Claude-Session)\s*:', re.IGNORECASE | re.MULTILINE)


def annotate(level, message):
    # An annotation, since the job log cannot be read from a cloud session. Values read from the pull request may
    # hold %, CR or LF, which GitHub would read as the start of another workflow command unless escaped.
    print(f'::{level}::' + message.replace('%', '%25').replace('\r', '%0D').replace('\n', '%0A'))


def fail(message):
    annotate('error', message)
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


def work_state_rows(text, feature):
    """Rows of docs/STATUS.md that name the feature: in the first table after the heading, the whole cell of the
    column, without backticks, is the ID (Next) or "before <ID>" (Waiting); an ID anywhere else does not count. No
    heading or no table gives no row. tools/feature_context.py keeps the same reader for the dossier."""
    found = []
    for heading, column, value in WORK_STATE:
        header, armed = None, False
        for line in text.splitlines():
            title = HEADING.match(line)
            if title and not armed:
                armed = title.group(1) == heading
            elif armed and line.startswith('|'):
                row = cells(line)
                if header is None:
                    header = row
                elif dict(zip(header, row)).get(column, '').strip('`').strip() == value.format(feature):
                    found.append(line.strip())
            elif armed and header is not None:
                break
    return found


def accepted_decisions(rev):
    rows = (cells(line) for line in show(rev, DECISIONS).splitlines() if line.startswith('| DEC-'))
    return {row[0] for row in rows if len(row) == 8 and row[7] == 'Accepted'}


def missing_decision(named):
    """Why a commit that needs a decision has none, for its error message."""
    if not named:
        return 'without an accepted decision (DEC-xxx) in its message'
    return (f'naming {", ".join(sorted(named))}, but no row of {DECISIONS} at the head has that ID as its first cell '
            'and "Accepted" as its Status (look in the workbook for a hidden character or a date in the ID cell)')


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


class Unreadable(Exception):
    pass


def exists(rev, path):
    return subprocess.run(['git', 'cat-file', '-e', f'{rev}:{path}'], capture_output=True).returncode == 0


def read_json(rev, path):
    """The parsed JSON of a file at a revision, MISSING when it does not exist; Unreadable with the reason otherwise."""
    run = subprocess.run(['git', 'cat-file', 'blob', f'{rev}:{path}'], capture_output=True)
    if run.returncode != 0:
        return MISSING
    try:
        return json.loads(run.stdout)
    except (ValueError, RecursionError) as error:
        raise Unreadable(f'{path} is not readable JSON: {error}') from None


def capped(path, errors):
    if len(errors) > MAX_ERRORS:
        return errors[:MAX_ERRORS] + [f'{path}: and {len(errors) - MAX_ERRORS} more']
    return errors


def string_map(value):
    return isinstance(value, dict) and all(isinstance(v, str) for v in value.values())


def approved_errors(approved, accepted):
    if approved is MISSING:
        return []
    if not string_map(approved):
        return [f'{APPROVED} is not a JSON object of package name -> decision (DEC-xxx)']
    return capped(APPROVED, [
        f'{APPROVED}: {json.dumps(name)} cites {json.dumps(decision)}, which is not a decision Accepted at the head'
        for name, decision in sorted(approved.items()) if not (DECISION_ID.fullmatch(decision) and decision in accepted)])


def manifest_errors(manifest, approved):
    if manifest is MISSING:
        return []
    if not isinstance(manifest, dict):
        return [f'{WEB_MANIFEST} is not a JSON object']
    names = approved if isinstance(approved, dict) else {}
    errors = []
    for section in SECTIONS:
        entries = manifest.get(section, {})
        if not string_map(entries):
            errors.append(f'{WEB_MANIFEST}: {section} is not an object of package name -> version')
            continue
        for name, version in entries.items():
            if name not in names:
                errors.append(f'{WEB_MANIFEST}: {section} {json.dumps(name)} is not in {APPROVED}; a package that an '
                              'official installer adds is approved by its own pull request first (DEC-111 (5)), any '
                              'other needs a Decision')
            if not EXACT.fullmatch(version):
                errors.append(f'{WEB_MANIFEST}: {section} {json.dumps(name)} has {json.dumps(version)}, not an exact '
                              'version (no range, tag, npm: alias, git, file: or URL)')
    return capped(WEB_MANIFEST, errors)


def lock_entry_error(key, entry, packages, workspaces):
    """Why one packages entry of the lockfile is refused, or None."""
    if not isinstance(entry, dict):
        return 'is not an object'
    if entry.get('inBundle') is True:
        # A top-level key has no parent but the root, which has no tarball.
        parent = key.rpartition('/node_modules/')[0]
        return None if parent and parent in packages else 'is inBundle without a parent entry whose tarball holds it'
    if entry.get('link') is True:
        return None if workspaces else f'is a link, and {WEB_MANIFEST} has no workspaces'
    name = key.rpartition('node_modules/')[2] if 'node_modules/' in key else ''
    if not NPM_NAME.fullmatch(name):
        return 'is not a package under node_modules/'
    if 'name' in entry and entry['name'] != name:
        return f'installs {json.dumps(entry["name"])} under another name (an alias)'
    version, integrity = entry.get('version'), entry.get('integrity')
    if not (isinstance(version, str) and EXACT.fullmatch(version)):
        return f'has the version {json.dumps(version)}, not an exact version'
    if not (isinstance(integrity, str) and integrity):
        return 'has no integrity'
    expected = f'{REGISTRY}{name}/-/{name.rpartition("/")[2]}-{version}.tgz'
    if entry.get('resolved') != expected:
        return f'resolves to {json.dumps(entry.get("resolved"))}, not {expected}'
    return None


def lock_errors(lock, manifest):
    if lock is MISSING:
        return [f'{WEB_LOCK} is missing: commit it with {WEB_MANIFEST} (DEC-104 (1))'] if manifest is not MISSING else []
    if not isinstance(lock, dict) or lock.get('lockfileVersion') != 3 or not isinstance(lock.get('packages'), dict):
        return [f'{WEB_LOCK} is not a lockfile with lockfileVersion 3 and a packages object']
    packages = lock['packages']
    workspaces = isinstance(manifest, dict) and 'workspaces' in manifest
    errors, seen = [], {}
    for key, entry in packages.items():
        reason = None if key == '' else lock_entry_error(key, entry, packages, workspaces)
        if not reason and key.lower() in seen:
            reason = f'differs only in case from {json.dumps(seen[key.lower()])}: one folder on Windows and macOS'
        seen.setdefault(key.lower(), key)
        if reason:
            errors.append(f'{WEB_LOCK}: {json.dumps(key)} {reason}')
    return capped(WEB_LOCK, errors)


def package_errors(manifest, lock, approved, accepted):
    """Gate 7 on the parsed files at the head, MISSING for a file that does not exist."""
    return approved_errors(approved, accepted) + manifest_errors(manifest, approved) + lock_errors(lock, manifest)


def web_package_errors(head, accepted):
    files, errors = {}, []
    for path in (WEB_MANIFEST, WEB_LOCK, APPROVED):
        try:
            files[path] = read_json(head, path)
        except Unreadable as error:
            errors.append(str(error))
    if exists(head, WEB_SHRINKWRAP):
        errors.append(f'{WEB_SHRINKWRAP} would replace {WEB_LOCK} when npm installs: remove it')
    return errors or package_errors(files[WEB_MANIFEST], files[WEB_LOCK], files[APPROVED], accepted)


def same(a, b):
    # Deep equality of JSON values: true and 1, or 1 and 1.0, differ; the order of keys does not matter.
    return json.dumps(a, sort_keys=True) == json.dumps(b, sort_keys=True)


def routine_manifest_change(before, after, approved):
    """True when web/package.json only adds approved packages or changes the exact version of one (DEC-111 (4))."""
    if not (isinstance(before, dict) and isinstance(after, dict)):
        return False
    others = [{k: v for k, v in manifest.items() if k not in SECTIONS} for manifest in (before, after)]
    if not same(*others):
        return False
    names = approved if isinstance(approved, dict) else {}
    for section in SECTIONS:
        old, new = before.get(section, {}), after.get(section, {})
        # Not an object, or a package removed or moved to another section.
        if not (string_map(old) and string_map(new)) or not old.keys() <= new.keys():
            return False
        if any(old.get(name) != version and not (name in names and EXACT.fullmatch(version))
               for name, version in new.items()):
            return False
    return True


def routine_manifest(base, head):
    try:
        return routine_manifest_change(read_json(base, WEB_MANIFEST), read_json(head, WEB_MANIFEST),
                                       read_json(head, APPROVED))
    except Unreadable:
        return False


def owner_paths(changed, base, head):
    """The changed files only the owner merges; base is the merge-base of the pull request and head its head."""
    return sorted(p for p in changed if p.startswith(OWNER_PATHS) or p.endswith(OWNER_SUFFIXES)
                  or (p == WEB_MANIFEST and not routine_manifest(base, head)))


def changed_files(base, head):
    # -z: without it git quotes a path with a non-ASCII byte ("tools/\303\251.py"), which no prefix would match.
    return [path for path in git('diff', '-z', '--name-only', '--no-renames', base, head).split('\0') if path]


def commits(base, head):
    """(hash, author email, committer email, message, changed files) of every commit of the pull request."""
    result = []
    for record in git('log', '--format=%h%x1f%ae%x1f%ce%x1f%B%x1e', f'{base}..{head}').split('\x1e'):
        if not record.strip():
            continue
        fields = record.lstrip('\n').split('\x1f', 3)
        if len(fields) != 4:
            fail(f'unreadable commit record: {record[:80]!r}')
        changed = [path for path in git('diff-tree', '-z', '--no-commit-id', '--name-only', '-r', '--no-renames',
                                        '--root', fields[0]).split('\0') if path]
        result.append((*fields, changed))
    return result


def check(base, head, title, app=''):
    for rev in (base, head):
        git('rev-parse', '--verify', '--quiet', f'{rev}^{{commit}}')
    errors = []
    merge_base = git('merge-base', base, head).strip()
    changed = changed_files(merge_base, head)

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
    if feature and FEAT_TITLE.match(title):
        for line in work_state_rows(show(head, STATUS), feature.group(1)):
            errors.append(f'{STATUS} at the head still has a row that names {feature.group(1)}: {line}; '
                          'the pull request that does the work deletes it (CLAUDE.md, Work state)')

    accepted = accepted_decisions(head)
    for commit, author, committer, message, files in commits(base, head):
        named = set(DECISION.findall(message))
        decided = bool(named & accepted)
        needs = missing_decision(named)
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

    errors += web_package_errors(head, accepted)

    owned = owner_paths(changed, merge_base, head)
    if owned and app:
        errors.append(f'opened through the {app} app, but changes files the owner merges: {", ".join(owned)}; '
                      'the owner opens this pull request on GitHub')
    for path in owned:
        annotate('notice', f'merged by the owner, not by auto-merge: {path}')
    for error in errors:
        annotate('error', error)
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
