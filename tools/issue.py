"""Issues of ZenGear (DEC-148, DEC-151): forms, relations, the reader of the gates and the order of next work.

Local sessions (the GitHub token of gh):
  python tools/issue.py render <feature|task|design|defect> --field <id>=<value>...
  python tools/issue.py create <form> --title "<title>" --field <id>=<value>... [--feature-id <ID>] [--label <label>]...
  python tools/issue.py link <blocked issue> <blocking issue>   |   unlink <blocked issue> <blocking issue>
  python tools/issue.py start <issue>
  python tools/issue.py feature <Feature ID>
  python tools/issue.py next
  python tools/issue.py roadmap      the Roadmap sub-issues with their blockers, and Priority High against them
Workflows (the event file of the workflow, GITHUB_TOKEN):
  python tools/issue.py lint <event file>          issue-lint.yml (reads the issue of the event from the API)
  python tools/issue.py deps <event file>          issue-deps.yml
  python tools/issue.py review-check <event file>  code-review.yml

render prints the body of a form as GitHub writes it ("### <label>", a blank line, the value, "_No response_" for an
empty optional field), so that an issue made through the API or the GitHub MCP passes issue-lint; a cloud session
renders it here (after `pip install --require-hashes -r tools/requirements.txt`) and creates the issue through the
MCP. create looks for an open issue with the same title first and prints it instead of making a second one, only when
the owner wrote it with the form's Issue type (then it sets a missing Feature ID); any other issue with that title
makes it refuse, without writing, so that the owner's token never changes an issue someone else wrote. link and
unlink add or remove a blocked-by relation through the REST API, which takes the id of the blocking issue, not its
number; a cloud session asks issue-deps for it with the comment of `tools/comment_templates.py blocked-by`. start sets
the field Start date to today in Asia/Ho_Chi_Minh, unless it is set already. feature prints the open Feature issues of an ID with their open
blockers. next prints the work in the order of DEC-148 (7), from the issues and handoff comments of the owner's
account and the pull requests of the owner or dependabot[bot] only (DEC-152 (6)); when GitHub cannot be read it says
so and lists the local branches and out/handoff-*.md instead of guessing.

Only render, create and lint read the forms, with PyYAML (tools/requirements.txt), imported there: the other
commands, the PR gates and the workflows issue-deps and code-review need no package.
"""
import argparse
import datetime
import glob
import json
import os
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import comment_templates as ct  # noqa: E402
from github_api import GitHub, GitHubError  # noqa: E402

ROOT = Path(__file__).resolve().parents[1]
FORMS = ROOT / '.github' / 'ISSUE_TEMPLATE'
OWNER_LOGIN = 'taitt0600'
DEPENDABOT = 'dependabot[bot]'
NO_RESPONSE = '_No response_'
FORM_OF_TYPE = {'Feature': 'feature', 'Task': 'task', 'Bug': 'defect', 'Design': 'design'}
TASK_TYPES = ('docs', 'ci', 'chore', 'build', 'test', 'refactor', 'perf')
TITLES = {'feature': (re.compile(r'[A-Z]{3}-[0-9]{2} \S.*'), '<Feature ID> <feature name>, e.g. "STF-09 Approve staff '
                                                               'accounts" or "STF-02 web"'),
          'design': (re.compile(r'SCR-[A-Z]{3}-[0-9]{2} \S.*'), '<SCR-ID> <screen name>'),
          'defect': (re.compile(r'[A-Z]{3}-[0-9]{2}: \S.*'), '<Feature ID>: <what is wrong>')}
EXAMPLE_TITLES = {'feature': 'STF-09 Approve staff accounts', 'task': 'Describe the roadmap',
                  'design': 'SCR-STF-04 Phân quyền', 'defect': 'AUT-04: a locked account signs in'}
HEADING = re.compile(r'^### (.+?)\s*$', re.MULTILINE)
SCREEN_ID = re.compile(r'\bSCR-[A-Z]{3}-[0-9]{2}\b')
LAYOUT_TITLE = re.compile(r'docs: (?:add|change) the layout of (SCR-[A-Z]{3}-[0-9]{2})')  # /design-screen step 5
ZONE = datetime.timezone(datetime.timedelta(hours=7))  # Asia/Ho_Chi_Minh, no daylight saving
FIELD_NODES = ('nodes{__typename ... on IssueFieldTextValue{value field{... on IssueFieldText{name}}} '
               '... on IssueFieldDateValue{value field{... on IssueFieldDate{name}}} '
               '... on IssueFieldSingleSelectValue{name field{... on IssueFieldSingleSelect{name}}}}')


# Forms ---------------------------------------------------------------------------------------------------------------

def load_form(name):
    import yaml  # tools/requirements.txt; only the commands that read a form need it
    with open(FORMS / f'{name}.yml', encoding='utf-8') as f:
        return yaml.safe_load(f)


def inputs(form):
    return [item for item in form['body'] if item.get('type') != 'markdown']


def render(name, fields):
    form = load_form(name)
    items = inputs(form)
    known = {item['id'] for item in items}
    unknown = sorted(set(fields) - known)
    if unknown:
        raise ValueError(f'unknown field of {name}.yml: {", ".join(unknown)} (fields: {", ".join(sorted(known))})')
    parts = []
    for item in items:
        value = (fields.get(item['id']) or '').strip()
        if not value and (item.get('validations') or {}).get('required'):
            raise ValueError(f'the field {item["id"]} ({item["attributes"]["label"]}) of {name}.yml is required')
        parts.append(f'### {item["attributes"]["label"]}\n\n{value or NO_RESPONSE}')
    return '\n\n'.join(parts) + '\n'


def body_values(body):
    """{label: value} of a body written from a form."""
    text = (body or '').replace('\r\n', '\n')
    found = list(HEADING.finditer(text))
    return {m.group(1): text[m.end():found[i + 1].start() if i + 1 < len(found) else len(text)].strip()
            for i, m in enumerate(found)}


def labels_of(item):
    return [label['name'] if isinstance(label, dict) else label for label in item.get('labels') or []]


def form_of(item):
    type_name = (item.get('type') or {}).get('name')
    if type_name in FORM_OF_TYPE:
        return FORM_OF_TYPE[type_name]
    return 'defect' if not type_name and 'defect' in labels_of(item) else None


def lint_problems(item):
    """What an issue lacks against its form (DEC-148 (9)); [] for a valid issue or the log issue #24."""
    labels = labels_of(item)
    if 'log' in labels:
        return []
    name = form_of(item)
    if name is None:
        return ['no Issue type: choose Feature, Task, Bug or Design (DEC-148 (2))']
    problems = []
    values = body_values(item.get('body'))
    for field in inputs(load_form(name)):
        label = field['attributes']['label']
        if (field.get('validations') or {}).get('required') and values.get(label, NO_RESPONSE) in ('', NO_RESPONSE):
            problems.append(f'the field "{label}" is missing or empty')
    if name in TITLES and not TITLES[name][0].fullmatch(item.get('title') or ''):
        problems.append(f'the title is not {TITLES[name][1]}')
    if name == 'task' and len([label for label in labels if label in TASK_TYPES]) != 1:
        problems.append(f'a Task has exactly one type label: {", ".join(TASK_TYPES)} (DEC-148 (2))')
    return problems


def lint(event, gh):
    # The issue as it is now, not the event payload: an issue made through the GitHub MCP gets its type after it opens,
    # so the opened payload has none (#274); a later edit may also have changed labels, title or body.
    number = event['issue']['number']
    item = gh.issue(number)
    problems = lint_problems(item)
    if form_of(item) == 'feature':
        # The field that gate 9 reads, read here with the same token, so that a refused read shows on an issue first.
        prefix = (item.get('title') or '').split(' ')[0]
        try:
            fid = Reader(gh).feature_id(number)
        except GitHubError as error:
            problems.append(f'the field Feature ID could not be read: {error}')
        else:
            if fid != prefix:
                problems.append(f'the field Feature ID is {fid!r}, not the ID that starts the title ({prefix})')
    path = f'/repos/{gh.repo}/issues/{number}'
    # The workflow posts as github-actions[bot]; a local `issue.py create` lints with the owner's token.
    mine = [c for c in gh.pages(f'{path}/comments')
            if (c.get('user') or {}).get('login') in ('github-actions[bot]', OWNER_LOGIN)
            and (ct.parse(c.get('body')) or ('',))[0] == 'issue-lint']
    if problems:
        if 'invalid' not in labels_of(item):
            gh.call('POST', f'{path}/labels', {'labels': ['invalid']})
        body = ct.issue_lint(problems)
        if mine:
            gh.call('PATCH', f'/repos/{gh.repo}/issues/comments/{mine[0]["id"]}', {'body': body})
        else:
            gh.call('POST', f'{path}/comments', {'body': body})
        print(f'#{number}: ' + '; '.join(problems))
        return 0
    if 'invalid' in labels_of(item):
        gh.call('DELETE', f'{path}/labels/invalid')
    for c in mine:
        gh.call('DELETE', f'/repos/{gh.repo}/issues/comments/{c["id"]}')
    print(f'#{number} follows its form')
    return 0


# Relations (DEC-151) -------------------------------------------------------------------------------------------------

def blocked_by_ids(gh, number):
    return {i['id'] for i in gh.pages(f'/repos/{gh.repo}/issues/{number}/dependencies/blocked_by')}


def blocking_issue(gh, number, blocker):
    """The blocking issue, or a ValueError with the reason it cannot block."""
    if blocker == number:
        raise ValueError(f'#{number} cannot block itself')
    try:
        found = gh.issue(blocker)
    except GitHubError as error:
        if error.status in (404, 410):
            raise ValueError(f'#{blocker} is not an issue of {gh.repo}') from None
        raise
    if found.get('pull_request'):
        raise ValueError(f'#{blocker} is a pull request, not an issue')
    return found


def deps(event, gh):
    """issue-deps.yml: one blocked-by or unblock request of the owner on an issue, answered by a result comment."""
    item, comment = event['issue'], event['comment']
    if item.get('pull_request'):
        print('ignored: a comment on a pull request')
        return 0
    if (comment.get('user') or {}).get('login') != OWNER_LOGIN:
        print('ignored: not a comment of the owner')
        return 0
    request = ct.parse(comment.get('body'))
    if not request or request[0] not in ('blocked-by', 'unblock'):
        print('ignored: no blocked-by or unblock marker')
        return 0
    kind, blocker, number = request[0], int(request[1]['issue']), item['number']

    def reply(result, detail):
        gh.call('POST', f'/repos/{gh.repo}/issues/{number}/comments',
                {'body': ct.blocked_by_result(comment['id'], result, detail)})
        print(f'{result}: {detail}')
        return 1 if result == 'error' else 0

    path = f'/repos/{gh.repo}/issues/{number}/dependencies/blocked_by'
    try:
        found = blocking_issue(gh, number, blocker)
        present = found['id'] in blocked_by_ids(gh, number)
        if kind == 'blocked-by':
            if present:
                return reply('ok', f'#{number} was already blocked by #{blocker}.')
            gh.call('POST', path, {'issue_id': found['id']})
            return reply('ok', f'#{number} is blocked by #{blocker}.')
        if found.get('state') != 'closed' and comment.get('performed_via_github_app'):
            return reply('refused', f'a cloud session removes the relation only once #{blocker} is closed '
                                    '(DEC-151 (1)); the owner or a local session can remove it now.')
        if not present:
            return reply('ok', f'#{number} was not blocked by #{blocker}.')
        gh.call('DELETE', f'{path}/{found["id"]}')
        return reply('ok', f'#{number} is no longer blocked by #{blocker}.')
    except ValueError as error:
        return reply('error', str(error))
    except GitHubError as error:
        return reply('error', str(error)[:300])


def link(gh, number, blocker, add):
    found = blocking_issue(gh, number, blocker)
    present = found['id'] in blocked_by_ids(gh, number)
    path = f'/repos/{gh.repo}/issues/{number}/dependencies/blocked_by'
    if add and not present:
        gh.call('POST', path, {'issue_id': found['id']})
    elif not add and present:
        gh.call('DELETE', f'{path}/{found["id"]}')
    print(f'#{number} ' + ('is blocked by' if add else 'is not blocked by') + f' #{blocker}')
    return 0


# code-review (DEC-150 (3)) -------------------------------------------------------------------------------------------

def review_conclusion(pr, comments):
    if (pr.get('user') or {}).get('login') == DEPENDABOT:
        return 'success', 'dependabot[bot] is exempt (DEC-150 (3))'
    sha = pr['head']['sha']
    verdicts = []
    for c in comments:
        parsed = ct.parse(c.get('body'))
        if (parsed and parsed[0] == 'code-review' and parsed[1]['sha'] == sha
                and (c.get('user') or {}).get('login') == OWNER_LOGIN and c.get('created_at') == c.get('updated_at')):
            verdicts.append((c['created_at'], c['id'], parsed[1]['verdict']))
    if not verdicts:
        return 'failure', f'no code-review verdict of {OWNER_LOGIN} for {sha[:12]} yet: review the head again'
    verdict = max(verdicts)[2]
    if verdict == 'pass':
        return 'success', f'code-reviewer passed {sha[:12]}'
    return 'failure', f'the latest code-review verdict for {sha[:12]} has Blocking items'


def review_check(event, gh):
    pr = event['pull_request'] if 'pull_request' in event else gh.pull(event['issue']['number'])
    comments = [] if (pr.get('user') or {}).get('login') == DEPENDABOT else \
        gh.pages(f'/repos/{gh.repo}/issues/{pr["number"]}/comments')
    conclusion, summary = review_conclusion(pr, comments)
    gh.call('POST', f'/repos/{gh.repo}/check-runs',
            {'name': 'code-review', 'head_sha': pr['head']['sha'], 'status': 'completed', 'conclusion': conclusion,
             'output': {'title': summary, 'summary': summary}})
    print(f'code-review: {conclusion}: {summary}')
    return 0


# Reader of the gates, the dossier and next ---------------------------------------------------------------------------

def field_map(nodes):
    found = {}
    for node in nodes or []:
        name = (node.get('field') or {}).get('name')
        value = node.get('value') if node.get('value') is not None else node.get('name')
        if name and value is not None:
            found[name] = value
    return found


class Reader:
    """What the PR gates, the dossier and next read from GitHub; every error is raised to the caller."""

    def __init__(self, gh=None):
        self.gh = gh or GitHub()
        self.name = self.gh.repo.split('/')[1]

    def issue(self, number):
        return self.gh.issue(number)

    def fields(self, number):
        data = self.gh.graphql(
            'query($owner:String!,$name:String!,$number:Int!){repository(owner:$owner,name:$name){issue(number:$number)'
            f'{{issueFieldValues(first:20){{{FIELD_NODES}}}}}}}}}',
            {'owner': self.gh.owner, 'name': self.name, 'number': number})
        return field_map(data['repository']['issue']['issueFieldValues']['nodes'])

    def feature_id(self, number):
        return self.fields(number).get('Feature ID')

    def open_feature_issues(self, fid):
        data = self.gh.graphql(
            'query($q:String!){search(query:$q,type:ISSUE,first:50){nodes{... on Issue{number title issueType{name} '
            f'issueFieldValues(first:20){{{FIELD_NODES}}}}}}}}}}}',
            {'q': f'repo:{self.gh.repo} is:issue is:open in:title "{fid}"'})
        return sorted(node['number'] for node in data['search']['nodes']
                      if node and (node.get('issueType') or {}).get('name') == 'Feature'
                      and field_map(node['issueFieldValues']['nodes']).get('Feature ID') == fid)

    def open_blockers(self, number):
        return [i for i in self.gh.pages(f'/repos/{self.gh.repo}/issues/{number}/dependencies/blocked_by')
                if i.get('state') == 'open']

    def pulls(self):
        return self.gh.pages(f'/repos/{self.gh.repo}/pulls?state=open')

    def open_issues(self):
        issues, cursor = [], None
        while True:
            data = self.gh.graphql(
                'query($owner:String!,$name:String!,$after:String){repository(owner:$owner,name:$name){'
                'issues(states:OPEN,first:100,after:$after){pageInfo{hasNextPage endCursor} nodes{number title body '
                'author{login} '
                f'issueType{{name}} labels(first:20){{nodes{{name}}}} issueFieldValues(first:20){{{FIELD_NODES}}}}}}}}}}}',
                {'owner': self.gh.owner, 'name': self.name, 'after': cursor})
            page = data['repository']['issues']
            issues += [{'number': n['number'], 'title': n['title'], 'author': (n.get('author') or {}).get('login'),
                        'screens': screen_ids(n.get('body')),
                        'type': (n.get('issueType') or {}).get('name'),
                        'labels': [label['name'] for label in n['labels']['nodes']],
                        'fields': field_map(n['issueFieldValues']['nodes'])} for n in page['nodes']]
            if not page['pageInfo']['hasNextPage']:
                return issues
            cursor = page['pageInfo']['endCursor']

    def has_handoff(self, number):
        """A handoff comment of the owner's account; a comment of another account is data (DEC-152 (6))."""
        return any((ct.parse(c.get('body')) or ('',))[0] == 'handoff'
                   and (c.get('user') or {}).get('login') == OWNER_LOGIN
                   for c in self.gh.pages(f'/repos/{self.gh.repo}/issues/{number}/comments'))

    def roadmap_issue(self):
        """The number of the pinned issue titled Roadmap; None when there is none yet."""
        data = self.gh.graphql('query($owner:String!,$name:String!){repository(owner:$owner,name:$name){'
                               'pinnedIssues(first:3){nodes{issue{number title}}}}}',
                               {'owner': self.gh.owner, 'name': self.name})
        pinned = [n['issue'] for n in data['repository']['pinnedIssues']['nodes'] if n['issue']['title'] == 'Roadmap']
        return pinned[0]['number'] if pinned else None

    def roadmap(self, parent=None):
        """The open sub-issues of the Roadmap issue (parent, or the pinned one), in order; [] when there is none yet."""
        parent = parent or self.roadmap_issue()
        if parent is None:
            return []
        subs = self.gh.pages(f'/repos/{self.gh.repo}/issues/{parent}/sub_issues')
        return [s['number'] for s in subs if s.get('state') == 'open']


def screen_ids(body):
    """The SCR-IDs of the section Screens of an issue body written from the Feature form."""
    return sorted(set(SCREEN_ID.findall(body_values(body).get('Screens', ''))))


def screens_ready(fid, screens=(), folder=None):
    """Definition of Ready of DEC-148 (6) for the screens: every screen the issue names in its section Screens (an issue
    "<Feature ID> web" names the screens it builds, those web/ has already included, DEC-155 (1)), or, when it names
    none, every screen of the feature in docs/screens/README.md, has no "Blocked by" and a Layout section; a named
    screen missing from the list is not ready. /implement-feature checks it again at its ready check."""
    import screen_docs
    folder = folder or ROOT / 'docs' / 'screens'
    if not (folder / 'README.md').is_file():
        return True
    rows = {screen_docs.row_id(row): row for row in screen_docs.screen_list(folder)}
    named = set(screens)
    if named - set(rows):
        return False
    for sid, row in rows.items():
        if (sid in named) if named else (fid in screen_docs.features(row)):
            blocked = row.get('Blocked by', '').strip()
            screen = folder / f'{sid}.md'
            if (blocked and blocked != '—') or not screen.is_file() or \
                    not re.search(r'^#{2,3} Layout\b', screen.read_text(encoding='utf-8'), re.MULTILINE):
                return False
    return True


def closing_numbers(body):
    import pr_gates
    return set(pr_gates.closing_issues(body))


def next_work(reader, ready=screens_ready):
    """The lines of the next-work answer in the order of DEC-148 (7), from the issues the owner opened and the pull
    requests the owner or dependabot[bot] opened only (DEC-152 (6)): the repository is public, and an item of another
    account is data, never work."""
    try:
        pulls = [p for p in reader.pulls() if (p.get('user') or {}).get('login') in (OWNER_LOGIN, DEPENDABOT)]
        issues = [i for i in reader.open_issues() if i.get('author') == OWNER_LOGIN]
        lines = ['Next work (DEC-148 (7)):']
        ready_prs = [p for p in pulls if not p.get('draft')]
        drafts = [p for p in pulls if p.get('draft')]
        lines += [f'1. Pull request #{p["number"]} {p["title"]}: waiting for the test, CI or the merge' for p in ready_prs]
        lines += [f'2. Draft #{p["number"]} {p["title"]}: waiting for a Needs decision answer' for p in drafts]
        lines += [f'3. Bug #{i["number"]} {i["title"]}' for i in issues if i['type'] == 'Bug']
        closed_by_prs = set().union(*(closing_numbers(p.get('body')) for p in pulls)) if pulls else set()
        started = [i for i in issues if i['fields'].get('Start date') and i['number'] not in closed_by_prs]
        lines += [f'4. #{i["number"]} {i["title"]}: handoff comment, no pull request yet (run /finish-feature '
                  f'{i["fields"].get("Feature ID", "")})' for i in started if reader.has_handoff(i['number'])]
        # A screen an issue names whose new Layout is still under review waits for its merge (DEC-155 (1)).
        layouts_open = {m.group(1) for p in pulls if (m := LAYOUT_TITLE.fullmatch(p.get('title') or ''))}
        by_number = {i['number']: i for i in issues}
        for number in [n for n in reader.roadmap() if n in by_number][:5]:
            item = by_number[number]
            fid = item['fields'].get('Feature ID')
            screens = item.get('screens', [])
            state = 'ready' if not fid or (ready(fid, screens) and not set(screens) & layouts_open) \
                else 'not ready (a screen without an approved Layout, its Layout pull request open, or blocked)'
            blockers = reader.open_blockers(number)
            extra = f'; blocked by {", ".join("#" + str(b["number"]) for b in blockers)} (handled in its plan)' \
                if blockers else ''
            lines.append(f'5. Roadmap #{number} {item["title"]}: {state}{extra}')
        lines += [f'6. Task #{i["number"]} {i["title"]}: trigger "{i["fields"]["Trigger"]}" (has it occurred?)'
                  for i in issues if i['type'] == 'Task' and i['fields'].get('Trigger')]
        return lines
    except (GitHubError, KeyError, TypeError) as error:
        return [f'GitHub could not be read ({error}); the plan is not known from the issues. Local state:',
                *offline_state()]


def roadmap_report(reader):
    """Every open sub-issue of the Roadmap (any Issue type) with its open blockers, then where Priority High differs
    from the relations: High exactly on an open issue that blocks a sub-issue (DEC-148 (3), DEC-151 (3)). The Roadmap
    issue itself is the order, not work, and is left out, as next leaves out the log #24."""
    parent = reader.roadmap_issue()
    issues = {i['number']: i for i in reader.open_issues() if i['number'] != parent}
    lines, blocking = ['Roadmap sub-issues and their open blocked-by issues:'], set()
    for number in (reader.roadmap(parent) if parent else []):
        blockers = reader.open_blockers(number)
        blocking |= {b['number'] for b in blockers}
        named = ', '.join(f'#{b["number"]}' for b in blockers) or 'nothing'
        lines.append(f'- #{number} {issues.get(number, {}).get("title", "")}: blocked by {named}')
    mismatches = [f'- #{n} blocks a Roadmap sub-issue but its Priority is {issues.get(n, {}).get("fields", {}).get("Priority", "not set")}'
                  for n in sorted(blocking) if issues.get(n, {}).get('fields', {}).get('Priority') != 'High']
    mismatches += [f'- #{n} has Priority High but blocks no Roadmap sub-issue' for n, item in sorted(issues.items())
                   if item['fields'].get('Priority') == 'High' and n not in blocking]
    return lines + ['Priority High against the relations:'] + (mismatches or ['- none'])


def offline_state():
    try:
        branches = subprocess.run(['git', 'branch', '--format=%(refname:short)'], capture_output=True, text=True,
                                  cwd=ROOT).stdout.split()
    except OSError:
        branches = []
    handoffs = sorted(Path(p).name for p in glob.glob(str(ROOT / 'out' / 'handoff-*.md')))
    return [f'- local branch {b}' for b in branches if b != 'main'] + [f'- {h}' for h in handoffs]


# Commands ------------------------------------------------------------------------------------------------------------

def pairs(values):
    fields = {}
    for value in values:
        key, sep, text = value.partition('=')
        if not sep:
            raise ValueError(f'--field takes <id>=<value>, not {value!r}')
        fields[key] = text
    return fields


def set_field(gh, number, name, value):
    node = gh.issue(number)['node_id']
    gh.graphql('mutation($id:ID!,$name:String!,$value:String!){updateIssue(input:{id:$id,issueFieldUpdates:'
               '[{fieldName:$name,operation:SET,value:$value}]}){issue{number}}}',
               {'id': node, 'name': name, 'value': value})


def create(gh, name, title, fields, feature_id=None, extra_labels=()):
    form_type = None
    for item in gh.pages(f'/repos/{gh.repo}/issues?state=open'):
        if not item.get('pull_request') and item['title'] == title:
            # Reuse only the owner's issue of the form's type: the owner's token never changes an issue someone
            # else wrote, or one of another type that happens to share the title.
            form_type = form_type or load_form(name)['type']
            author = (item.get('user') or {}).get('login')
            item_type = (item.get('type') or {}).get('name')
            if author != OWNER_LOGIN or item_type != form_type:
                raise ValueError(f'#{item["number"]} is open with the same title, but by {author} with the Issue type '
                                 f'{item_type}, not {OWNER_LOGIN} with {form_type}: settle it by hand first')
            # An issue made by hand may lack the field that the gates and the dossier read: set it, then lint again.
            if feature_id and Reader(gh).feature_id(item['number']) != feature_id:
                set_field(gh, item['number'], 'Feature ID', feature_id)
                lint({'issue': {'number': item['number']}}, gh)
            print(f'#{item["number"]} {item["html_url"]} (already open)')
            return 0
    form = load_form(name)
    body = render(name, fields)
    made = gh.call('POST', f'/repos/{gh.repo}/issues', {'title': title, 'body': body, 'type': form['type'],
                                                       'labels': list(form.get('labels') or []) + list(extra_labels)})
    if feature_id:
        set_field(gh, made['number'], 'Feature ID', feature_id)
        # issue-lint ran on "opened" before the field was set; setting a field starts no listed event, so lint again.
        lint({'issue': {'number': made['number']}}, gh)
    print(f'#{made["number"]} {made["html_url"]}')
    return 0


def start(gh, number):
    """Start date once: a run that resumes keeps the first date, which M3 of DEC-148 (12) measures from."""
    first = Reader(gh).fields(number).get('Start date')
    if first:
        print(f'#{number}: Start date {first} (kept)')
        return 0
    today = datetime.datetime.now(ZONE).date().isoformat()
    set_field(gh, number, 'Start date', today)
    print(f'#{number}: Start date {today}')
    return 0


def feature(gh, fid):
    reader = Reader(gh)
    numbers = reader.open_feature_issues(fid)
    if not numbers:
        print(f'{fid} has no open Feature issue')
    for number in numbers:
        blockers = reader.open_blockers(number)
        print(f'#{number}: open blockers ' + (', '.join(f'#{b["number"]} {b["title"]}' for b in blockers) or 'none'))
    return 0


def read_event(path):
    with open(path, encoding='utf-8') as f:
        return json.load(f)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('render')
    p.add_argument('form', choices=sorted(FORM_OF_TYPE.values()))
    p.add_argument('--field', action='append', default=[])
    p = commands.add_parser('create')
    p.add_argument('form', choices=sorted(FORM_OF_TYPE.values()))
    p.add_argument('--title', required=True)
    p.add_argument('--field', action='append', default=[])
    p.add_argument('--feature-id')
    p.add_argument('--label', action='append', default=[])
    for name in ('link', 'unlink'):
        p = commands.add_parser(name)
        p.add_argument('blocked', type=int)
        p.add_argument('blocking', type=int)
    commands.add_parser('start').add_argument('number', type=int)
    commands.add_parser('feature').add_argument('feature_id')
    commands.add_parser('next')
    commands.add_parser('roadmap')
    for name in ('lint', 'deps', 'review-check'):
        commands.add_parser(name).add_argument('event')
    args = parser.parse_args(argv)
    sys.stdout.reconfigure(encoding='utf-8')
    try:
        if args.command == 'render':
            sys.stdout.write(render(args.form, pairs(args.field)))
            return 0
        if args.command == 'next':
            try:
                reader = Reader()
            except GitHubError as error:
                print(f'GitHub could not be read ({error}); the plan is not known from the issues. Local state:')
                print('\n'.join(offline_state()))
                return 3
            lines = next_work(reader)
            print('\n'.join(lines))
            return 3 if lines and lines[0].startswith('GitHub could not be read') else 0
        gh = GitHub()
        if args.command == 'roadmap':
            print('\n'.join(roadmap_report(Reader(gh))))
            return 0
        if args.command == 'create':
            return create(gh, args.form, args.title, pairs(args.field), args.feature_id, args.label)
        if args.command in ('link', 'unlink'):
            return link(gh, args.blocked, args.blocking, args.command == 'link')
        if args.command == 'start':
            return start(gh, args.number)
        if args.command == 'feature':
            return feature(gh, args.feature_id)
        handler = {'lint': lint, 'deps': deps, 'review-check': review_check}[args.command]
        return handler(read_event(args.event), gh)
    except (ValueError, GitHubError, OSError) as error:
        print(f'issue: {error}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
