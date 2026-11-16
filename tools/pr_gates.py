"""Pull request gates that stand in for a person's approval (CLAUDE.md, Workflow).

  python tools/pr_gates.py check <base sha> <head sha> <event file>
  python tools/pr_gates.py check-local <base> "<pull request title>" --body-file <file> [--draft]
  python tools/pr_gates.py coverage <Feature ID>

check-local runs check on HEAD of the current branch with the event GitHub would send (that title and body, auto-merge
off), so that /finish-feature and /fix-defect see the gates before they push.

check, run by .github/workflows/gates.yml on every pull request; the title, body, branch, author, draft and auto-merge
state are read from the event file of the workflow ($GITHUB_EVENT_PATH), never from the command line:
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
  5. Owner merges: a pull request that changes a file of owner_paths has auto-merge off (DEC-149 (1)); the event
     auto_merge_enabled runs the gates again, so turning it on after they passed fails them.
  6. Retired with docs/STATUS.md by DEC-148 (11) (D): the plan is the GitHub Issues, and gate 10 checks the blockers
     of a feature; the number stays so that gates 7 to 12 keep theirs.
  7. Web packages (DEC-104 (2), DEC-111 (3)), read at the head as parsed JSON: every entry of
     web/approved-packages.json cites a decision Accepted at the head; every name in the dependencies,
     devDependencies, optionalDependencies and peerDependencies of web/package.json is in that file, with an exact
     semver (no range, tag, npm: alias, git, file: or URL); web/package-lock.json exists with lockfileVersion 3, and
     each of its packages entries but the root has integrity and resolves to
     https://registry.npmjs.org/<name>/-/<unscoped name>-<version>.tgz, <name> being the key after its last
     node_modules/. An alias (a "name" that differs), a link (unless web/package.json has workspaces) and a top-level
     inBundle entry are refused; a nested inBundle entry is skipped, since the tarball of its parent holds it.
     web/npm-shrinkwrap.json, which npm would install from instead of the lockfile, is refused.
  8. Branch name (DEC-148 (10)): tools/branch_name.py problem(), its type equal to the type of the title.
  9. Closes (DEC-148 (6), (10), DEC-150 (2)): every issue the body closes is an open issue; a feat title that ends with
     a Feature ID closes an open issue of type Feature whose field Feature ID is that ID, and at most one such issue is
     open; a Bug, Task or Design branch closes the issue of its name.
 10. Blockers (DEC-148 (6)): the Feature issue of a feat pull request has no open blocked-by issue that the body does
     not close (a blocker may be done in the feature's own pull request).
 11. Body (DEC-150 (2)): the sections of .github/pull_request_template.md, the four parts of "Tóm tắt cho chủ dự án";
     a Needs decision section other than "Không có" needs a draft.
 12. Refs (DEC-150 (1)): every feat and fix commit has a Refs: footer that names #<n> of an issue the body closes.
Gates 8 to 12 and the commit identity skip a pull request opened by dependabot[bot] (DEC-148 (10)). Gates 9 and 10 read
GitHub (tools/issue.py); a read that fails fails the gate with its reason.
check reads the pull request from git objects only: nothing of the pull request is checked out or run.
coverage reads the working tree, new files included (used by /implement-feature and /finish-feature).
owner_paths lists the changed files that only the owner merges (tools/github_pr.py refuses auto-merge for them): by
name, and by content between the merge-base and the head for web/package.json (owner merged unless it only adds
approved packages or changes the exact version of one, DEC-111 (4))."""
import json, os, re, subprocess, sys
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import branch_name  # noqa: E402

SPEC_PATHS = ('api/db/', 'docs/blueprint/', 'docs/DATABASE.md', 'docs/product-domain-blueprint-MVP.xlsx')
BUILD_SUFFIXES = ('.csproj', '.props', '.targets')
# What decides how the code is built, checked or merged: changed only in pull requests the owner merges.
OWNER_PATHS = SPEC_PATHS + ('.claude/', '.githooks/', '.github/', '.gitleaks.toml', '.mcp.json', '.config/', 'tools/',
                            '.cz.toml',
                            'docs/ARCHITECTURE.md', 'api/tests/ZenGear.ArchitectureTests/', 'api/global.json',
                            'api/.config/', 'api/.editorconfig', 'nuget.config', 'NuGet.Config',
                            # The web build files of DEC-104 (3); web/package.json goes by content.
                            'web/approved-packages.json', 'web/npm-shrinkwrap.json', 'web/tsconfig.json',
                            'web/components.json', 'web/.npmrc',
                            'web/.nvmrc', 'web/next.config.', 'web/eslint.config.', 'web/postcss.config.',
                            'web/vitest.config.', 'web/playwright.config.', 'web/orval.config.')
OWNER_SUFFIXES = BUILD_SUFFIXES + ('.rsp', '.dcproj', '.sln', '.slnx')
CODE_PATHS = ('api/src/', 'web/')
FEATURES = 'docs/blueprint/features.md'
DECISIONS = 'docs/blueprint/decisions.md'
PACKAGES = 'api/Directory.Packages.props'
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
RUNS_CODE = re.compile(r'<\s*(Exec|Target|UsingTask|Import|PreBuildEvent|PostBuildEvent|Analyzer|Sdk)\b|\bSdk\s*=')
FEATURE_IN_TITLE = re.compile(r'\(([A-Z]{3}-\d{2})\)\s*$')
CODE_TITLE = re.compile(r'^(feat|fix)(\([^)]*\))?!?:')
FEAT_TITLE = re.compile(r'^feat(\([^)]*\))?!?:')
DECISION = re.compile(r'\bDEC-\d{3}\b')
TEST_METHOD = re.compile(r'\b(?:Task|ValueTask|void)\s+(R_[A-Z]+_\d+)_\w*\s*\(')
AGENT_EMAIL = 'noreply@anthropic.com'
AGENT_TRAILER = re.compile(r'^\s*(Co-Authored-By|Claude-Session)\s*:', re.IGNORECASE | re.MULTILINE)
DEPENDABOT = 'dependabot[bot]'
# The one identity of every commit (DEC-150 (1), CLAUDE.md, Commits), as git log prints "%an <%ae>".
OWNER = 'taitt0600 <taitt.dev@gmail.com>'
# The closing keywords of GitHub: close, closes, closed, fix, fixes, fixed, resolve, resolves, resolved.
CLOSING = re.compile(r'\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?):?\s+#([1-9][0-9]*)\b', re.IGNORECASE)
REFS = re.compile(r'^Refs:(.*)$', re.MULTILINE)
ISSUE_REF = re.compile(r'#([1-9][0-9]*)\b')
CODE_COMMIT = re.compile(r'^(feat|fix)(\([^)]*\))?!?:')
SECTION = re.compile(r'^##[ \t]+(.+?)[ \t]*$', re.MULTILINE)
COMMENT = re.compile(r'<!--.*?-->', re.DOTALL)
SUMMARY = 'Tóm tắt cho chủ dự án'
BODY_SECTIONS = (SUMMARY, 'Needs decision', 'Deviations', 'Test')
SUMMARY_PARTS = ('Làm gì', 'Lệch so với yêu cầu', 'Rủi ro và kết quả rà', 'Bạn cần quyết định')
NOTHING = ('không có', 'none')


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
    """(hash, "author <email>", "committer <email>", message, changed files) of every commit of the pull request."""
    result = []
    for record in git('log', '--format=%h%x1f%an <%ae>%x1f%cn <%ce>%x1f%B%x1e', f'{base}..{head}').split('\x1e'):
        if not record.strip():
            continue
        fields = record.lstrip('\n').split('\x1f', 3)
        if len(fields) != 4:
            fail(f'unreadable commit record: {record[:80]!r}')
        changed = [path for path in git('diff-tree', '-z', '--no-commit-id', '--name-only', '-r', '--no-renames',
                                        '--root', fields[0]).split('\0') if path]
        result.append((*fields, changed))
    return result


def sections(body):
    """{heading of level 2: its text without HTML comments} of a pull request body."""
    text = COMMENT.sub('', (body or '').replace('\r\n', '\n'))
    found = list(SECTION.finditer(text))
    return {m.group(1): text[m.end():found[i + 1].start() if i + 1 < len(found) else len(text)].strip()
            for i, m in enumerate(found)}


def body_errors(body, draft):
    """Gate 11: the sections of .github/pull_request_template.md (DEC-150 (2)); also run by tools/github_pr.py create."""
    parts = sections(body)
    errors = [f'the body has no section "## {name}" of .github/pull_request_template.md'
              for name in BODY_SECTIONS if name not in parts]
    if SUMMARY in parts:
        errors += [f'the section "{SUMMARY}" has no part "{part}"' for part in SUMMARY_PARTS if part not in parts[SUMMARY]]
    needs = parts.get('Needs decision')
    if needs is not None:
        if not needs:
            errors.append('the section "Needs decision" is empty: write "Không có" when there is none')
        elif needs.rstrip('.').lower() not in NOTHING and not draft:
            errors.append('the body has a Needs decision item: the pull request stays a draft until it is answered '
                          '(CLAUDE.md, Workflow)')
    return errors


def closing_issues(body):
    return sorted({int(n) for n in CLOSING.findall(COMMENT.sub('', body or ''))})


def issue_errors(pr, closes, api):
    """Gates 9 and 10 through the reader of tools/issue.py; any GitHub error fails the gate."""
    title, branch = pr.get('title') or '', pr['head']['ref']
    errors = []
    try:
        issues = {number: api.issue(number) for number in closes}
        for number, issue in issues.items():
            if 'pull_request' in issue:
                errors.append(f'Closes #{number}: #{number} is a pull request, not an issue')
            elif issue.get('state') != 'open':
                errors.append(f'Closes #{number}: the issue is not open')
        own = branch_name.issue_number(branch)
        if own is not None and own not in closes:
            errors.append(f'the branch {branch} was made from #{own}, but the body has no "Closes #{own}" '
                          '(DEC-148 (10))')
        feature = FEATURE_IN_TITLE.search(title)
        if feature and FEAT_TITLE.match(title):
            fid = feature.group(1)
            mine = [n for n, issue in issues.items() if issue.get('state') == 'open' and 'pull_request' not in issue
                    and (issue.get('type') or {}).get('name') == 'Feature' and api.feature_id(n) == fid]
            if not mine:
                errors.append(f'a feat pull request of {fid} closes its open Feature issue, whose field Feature ID is '
                              f'{fid} (DEC-148 (6)): add "Closes #<issue>"')
            open_features = api.open_feature_issues(fid)
            if len(open_features) > 1:
                errors.append(f'{fid} has more than one open Feature issue: '
                              + ', '.join(f'#{n}' for n in open_features) + ' (DEC-148 (6))')
            for number in mine:
                # A blocker done in this pull request is closed by it (DEC-148 (6)): it does not fail the gate.
                for blocker in (b for b in api.open_blockers(number) if b['number'] not in closes):
                    errors.append(f'#{number} is still blocked by the open issue #{blocker["number"]} '
                                  f'"{blocker.get("title", "")}": close it in this pull request or settle it first '
                                  '(DEC-148 (6))')
    except Exception as error:  # noqa: BLE001  any failure to read GitHub fails the gate (DEC-148 (7))
        errors.append(f'cannot read the issues of the pull request from GitHub: {error}')
    return errors


def refs_errors(commit_list, closes):
    """Gate 12: a feat or fix commit names #<n> of a closed issue in its Refs: footer (DEC-150 (1))."""
    errors = []
    for commit, _, _, message, _ in commit_list:
        if CODE_COMMIT.match(message):
            named = {int(n) for line in REFS.findall(message) for n in ISSUE_REF.findall(line)}
            if not named & set(closes):
                errors.append(f'commit {commit} is feat or fix, but its Refs: footer names no issue this pull request '
                              f'closes (#{", #".join(map(str, closes)) or "<issue>"})')
    return errors


def pull_request_errors(pr, commit_list, api):
    """Gates 8, 9, 10, 11 and 12 on the pull request of the event; dependabot[bot] is exempt (DEC-148 (10))."""
    if (pr.get('user') or {}).get('login') == DEPENDABOT:
        return []
    title, body = pr.get('title') or '', pr.get('body') or ''
    errors = []
    reason = branch_name.problem(pr['head']['ref'], title)
    if reason:
        errors.append(reason)
    errors += body_errors(body, bool(pr.get('draft')))
    closes = closing_issues(body)
    errors += issue_errors(pr, closes, api)
    errors += refs_errors(commit_list, closes)
    return errors


def identity_errors(commit, author, committer):
    errors = []
    if AGENT_EMAIL in (author.lower(), committer.lower()):
        errors.append(f'commit {commit} is authored or committed as Claude ({AGENT_EMAIL}): '
                      'set git user.name and user.email to the owner (CLAUDE.md, Commits)')
    elif author != OWNER or committer != OWNER:
        errors.append(f'commit {commit} is authored by {author} and committed by {committer}, not by the owner '
                      f'{OWNER} (DEC-150 (1), CLAUDE.md, Commits)')
    return errors


def read_event(path):
    with open(path, encoding='utf-8') as f:
        return json.load(f)


def local_event(title, body_file, draft):
    """The event GitHub would send for the current branch, so that a session runs the gates before it pushes."""
    with open(body_file, encoding='utf-8') as f:
        body = f.read()
    branch = git('branch', '--show-current').strip()
    return {'pull_request': {'title': title, 'body': body, 'draft': draft, 'auto_merge': None, 'head': {'ref': branch},
                             'user': {'login': 'taitt0600'}}}


def check(base, head, event, api=None):
    """event: the parsed event of the workflow; api: the issue reader (tools/issue.py) or a fake in tests."""
    pr = event['pull_request']
    title = pr.get('title') or ''
    dependabot = (pr.get('user') or {}).get('login') == DEPENDABOT
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

    accepted = accepted_decisions(head)
    commit_list = commits(base, head)
    for commit, author, committer, message, files in commit_list:
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
        if not dependabot:
            errors += identity_errors(commit, author, committer)
        if AGENT_TRAILER.search(message):
            errors.append(f'commit {commit} has a Co-Authored-By or Claude-Session trailer (CLAUDE.md, Commits)')

    errors += web_package_errors(head, accepted)
    if api is None and not dependabot:
        import issue  # GitHub is read only for the gates that need it
        api = issue.Reader()
    errors += pull_request_errors(pr, commit_list, api)

    owned = owner_paths(changed, merge_base, head)
    if owned and pr.get('auto_merge'):
        errors.append(f'auto-merge is on, but the pull request changes files the owner merges: {", ".join(owned)}; '
                      'turn auto-merge off, the owner merges it (DEC-149 (1))')
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
    if len(sys.argv) == 5 and sys.argv[1] == 'check':
        sys.exit(check(sys.argv[2], sys.argv[3], read_event(sys.argv[4])))
    if len(sys.argv) in (6, 7) and sys.argv[1] == 'check-local' and sys.argv[4] == '--body-file' \
            and sys.argv[6:] in ([], ['--draft']):
        sys.exit(check(sys.argv[2], 'HEAD', local_event(sys.argv[3], sys.argv[5], sys.argv[6:] == ['--draft'])))
    if len(sys.argv) == 3 and sys.argv[1] == 'coverage':
        sys.exit(coverage(sys.argv[2]))
    print(__doc__)
    sys.exit(2)
