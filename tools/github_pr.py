"""GitHub steps of /finish-feature and /fix-defect, over the REST API (CLAUDE.md, Workflow).

  python tools/github_pr.py create --title "<title>" --body-file <file> [--draft]
  python tools/github_pr.py ready <number>
  python tools/github_pr.py auto-merge <number> [--off]
  python tools/github_pr.py checks <number> [--wait <seconds>]
  python tools/github_pr.py comment <number> --body-file <file>

create opens the pull request of the current branch against main, or updates the one already open (title,
body, and draft or ready as asked). auto-merge refuses when the pull request changes a file that only the owner
merges (owner_paths in tools/pr_gates.py, which judges web/package.json and the shadcn lock by content: the branch
of the pull request and main are fetched here and compared from their merge-base, DEC-111 (4)), or when main does
not require every check in REQUIRED_CHECKS or lets someone bypass them, so that nothing merges unchecked; it turns
auto-merge off again when the head moved while it was being enabled. REQUIRED_CHECKS are job names, each equal to its job id:
ci-ok aggregates every job of .github/workflows/pipeline.yml, and pr-gates and pr-title run in
.github/workflows/gates.yml. checks prints the latest run of every check of the head commit, the annotations of the failed ones and the merge state.

Exit codes: 0 done, 1 a check failed or the pull request cannot merge as it is, 3 still running when the wait
ended, 4 GitHub could not be reached, 5 the owner merges (auto-merge refused).

In a cloud session the GitHub proxy adds the credential (GH_TOKEN reads "proxy-injected") and serves no
GraphQL, so draft, ready and auto-merge use its routes under /pulls/<number>/ccr/. Elsewhere the token comes
from GH_TOKEN, GITHUB_TOKEN or `gh auth token`, and those three use GraphQL. Job logs are not reachable from
a cloud session: reproduce a failed job with its commands in .github/workflows/."""
import argparse, json, os, re, subprocess, sys, time, urllib.error, urllib.parse, urllib.request

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pr_gates import changed_files, owner_paths  # noqa: E402

API = 'https://api.github.com'
PROXY_TOKEN = 'proxy-injected'
BASE = 'main'
REQUIRED_CHECKS = ('ci-ok', 'pr-gates', 'pr-title')
PASSED = ('success', 'neutral', 'skipped')
POLL_SECONDS = 30
ATTEMPTS = 3


class GitHubError(Exception):
    exit_code = 1


class Unreachable(GitHubError):
    exit_code = 4


class OwnerMerges(GitHubError):
    exit_code = 5


def run(*args):
    return subprocess.run(args, capture_output=True, text=True, encoding='utf-8', check=True).stdout.strip()


def read(path):
    with open(path, encoding='utf-8') as f:
        return f.read()


class GitHub:
    def __init__(self):
        self.token = os.environ.get('GH_TOKEN') or os.environ.get('GITHUB_TOKEN') or self._gh_token()
        self.proxy = self.token == PROXY_TOKEN
        url = run('git', 'remote', 'get-url', 'origin')
        match = re.search(r'[/:]([^/:]+)/([^/]+?)(?:\.git)?/?$', url)
        if not match:
            raise GitHubError(f'origin is not a GitHub repository: {url}')
        self.owner, self.repo = match[1], f'{match[1]}/{match[2]}'

    @staticmethod
    def _gh_token():
        try:
            return run('gh', 'auth', 'token')
        except (OSError, subprocess.CalledProcessError):
            raise GitHubError('No GitHub token: set GH_TOKEN, or sign in with `gh auth login`.') from None

    def call(self, method, path, body=None):
        data = None if body is None else json.dumps(body).encode()
        headers = {'Authorization': f'Bearer {self.token}', 'Accept': 'application/vnd.github+json',
                   'X-GitHub-Api-Version': '2022-11-28', 'User-Agent': 'zengear-github-pr'}
        if data is not None:
            headers['Content-Type'] = 'application/json'
        # Reads are retried; a write is sent once, since it may have been applied before the error.
        attempts = ATTEMPTS if method == 'GET' else 1
        for attempt in range(1, attempts + 1):
            request = urllib.request.Request(f'{API}{path}', data=data, method=method, headers=headers)
            try:
                with urllib.request.urlopen(request, timeout=60) as response:
                    text = response.read().decode()
                return json.loads(text) if text else None
            except urllib.error.HTTPError as error:
                detail = error.read().decode(errors='replace')[:500]
                if error.code < 500 or attempt == attempts:
                    kind = Unreachable if error.code >= 500 else GitHubError
                    raise kind(f'{method} {path}: HTTP {error.code} {detail}') from None
            except (urllib.error.URLError, OSError) as error:
                if attempt == attempts:
                    raise Unreachable(f'{method} {path}: {error}') from None
            time.sleep(5 * attempt)

    def graphql(self, query, variables):
        result = self.call('POST', '/graphql', {'query': query, 'variables': variables})
        if result.get('errors'):
            raise GitHubError('GraphQL: ' + '; '.join(error['message'] for error in result['errors']))
        return result['data']

    def pull(self, number):
        return self.call('GET', f'/repos/{self.repo}/pulls/{number}')


def owned_files(pr):
    """owner_paths of the pull request, read from its branch and main fetched from origin into this clone."""
    head = pr['head']['sha']
    try:
        run('git', 'fetch', '--quiet', '--no-tags', 'origin', BASE)
        main = run('git', 'rev-parse', 'FETCH_HEAD')
        run('git', 'fetch', '--quiet', '--no-tags', 'origin', f'pull/{pr["number"]}/head')
        fetched = run('git', 'rev-parse', 'FETCH_HEAD')
        if fetched != head:
            raise GitHubError(f'#{pr["number"]} moved to {fetched} while it was read: run auto-merge again')
        merge_base = run('git', 'merge-base', main, head)
    except (OSError, subprocess.CalledProcessError) as error:
        raise GitHubError(f'cannot read #{pr["number"]} with git: {getattr(error, "stderr", "") or error}') from None
    try:
        return owner_paths(changed_files(merge_base, head), merge_base, head)
    except SystemExit:
        # pr_gates exits through fail() when git cannot read what it just fetched; its message is printed already.
        raise GitHubError(f'cannot read #{pr["number"]} with git') from None


def set_draft(gh, pr, draft):
    if gh.proxy:
        route = 'convert_to_draft' if draft else 'ready_for_review'
        gh.call('POST', f'/repos/{gh.repo}/pulls/{pr["number"]}/ccr/{route}')
    else:
        mutation = 'convertPullRequestToDraft' if draft else 'markPullRequestReadyForReview'
        gh.graphql(f'mutation($id:ID!){{{mutation}(input:{{pullRequestId:$id}}){{clientMutationId}}}}',
                   {'id': pr['node_id']})


def create(gh, title, body_file, draft):
    branch = run('git', 'branch', '--show-current')
    if not branch or branch == BASE:
        raise GitHubError(f'Run from the branch of the feature, not {BASE}.')
    remote = run('git', 'ls-remote', '--heads', 'origin', branch).split()
    if not remote or remote[0] != run('git', 'rev-parse', 'HEAD'):
        raise GitHubError('The branch is not pushed as it is: git push -u origin HEAD')
    body = read(body_file)
    query = urllib.parse.urlencode({'head': f'{gh.owner}:{branch}', 'state': 'open'})
    existing = gh.call('GET', f'/repos/{gh.repo}/pulls?{query}')
    if existing:
        pr = gh.call('PATCH', f'/repos/{gh.repo}/pulls/{existing[0]["number"]}', {'title': title, 'body': body})
        if pr['draft'] != draft:
            set_draft(gh, pr, draft)
    else:
        pr = gh.call('POST', f'/repos/{gh.repo}/pulls',
                     {'title': title, 'head': branch, 'base': BASE, 'body': body, 'draft': draft})
    pr = gh.pull(pr['number'])
    print(f'#{pr["number"]} {pr["html_url"]}' + (' (draft)' if pr['draft'] else ''))


def ready(gh, number):
    pr = gh.pull(number)
    if pr['draft']:
        set_draft(gh, pr, False)
    print(f'#{number} is ready for review')


def protection_gaps(gh):
    protection = gh.call('GET', f'/repos/{gh.repo}/branches/{BASE}').get('protection') or {}
    checks = protection.get('required_status_checks') or {}
    gaps = [f'{BASE} does not require "{name}"' for name in REQUIRED_CHECKS if name not in checks.get('contexts', [])]
    if checks.get('enforcement_level') != 'everyone':
        gaps.append(f'{BASE} lets administrators bypass the required checks ("Do not allow bypassing" is off)')
    return gaps


def set_auto_merge(gh, pr, on):
    if gh.proxy:
        path = f'/repos/{gh.repo}/pulls/{pr["number"]}/ccr/auto_merge'
        if on:
            gh.call('PUT', path, {'merge_method': 'rebase'})
        else:
            gh.call('DELETE', path)
    elif on:
        gh.graphql('mutation($id:ID!){enablePullRequestAutoMerge(input:{pullRequestId:$id,mergeMethod:REBASE})'
                   '{clientMutationId}}', {'id': pr['node_id']})
    else:
        gh.graphql('mutation($id:ID!){disablePullRequestAutoMerge(input:{pullRequestId:$id}){clientMutationId}}',
                   {'id': pr['node_id']})


def auto_merge(gh, number, off):
    pr = gh.pull(number)
    if off:
        if pr['auto_merge']:
            set_auto_merge(gh, pr, False)
        print(f'#{number}: auto-merge off')
        return
    if pr['state'] != 'open' or pr['base']['ref'] != BASE:
        raise GitHubError(f'#{number} is not an open pull request into {BASE}.')
    if pr['draft']:
        raise GitHubError(f'#{number} is a draft: make it ready first.')
    owned = owned_files(pr)
    if owned:
        raise OwnerMerges(f'Auto-merge not enabled: #{number} changes files the owner merges: {", ".join(owned)}')
    gaps = protection_gaps(gh)
    if gaps:
        raise OwnerMerges('Auto-merge not enabled, the owner merges: ' + '; '.join(gaps))
    try:
        set_auto_merge(gh, pr, True)
    except GitHubError as error:
        # Auto-merge cannot be enabled once every requirement is met; main enforces them, so merge now.
        if 'clean status' not in str(error).lower():
            raise
        gh.call('PUT', f'/repos/{gh.repo}/pulls/{number}/merge', {'merge_method': 'rebase', 'sha': pr['head']['sha']})
        print(f'#{number}: every requirement was met already, merged with rebase')
        return
    after = gh.pull(number)
    if after['head']['sha'] != pr['head']['sha']:
        # A push between the check of the files and the enabling: that head was not judged.
        set_auto_merge(gh, after, False)
        raise GitHubError(f'#{number} moved while auto-merge was enabled; it is off again: run auto-merge again')
    state = after['auto_merge']
    if not state or state.get('merge_method') != 'rebase':
        raise GitHubError(f'#{number}: auto-merge is not on with rebase: {state}')
    print(f'#{number}: auto-merge on (rebase), it merges when the required checks pass')


def latest_checks(gh, sha):
    runs = gh.call('GET', f'/repos/{gh.repo}/commits/{sha}/check-runs?per_page=100')['check_runs']
    latest = {}
    for check in sorted(runs, key=lambda c: c['id']):
        latest[check['name']] = check
    return latest


def checks(gh, number, wait):
    deadline = time.monotonic() + wait
    while True:
        pr = gh.pull(number)
        sha = pr['head']['sha']
        workflows = gh.call('GET', f'/repos/{gh.repo}/actions/runs?head_sha={sha}&per_page=100')['workflow_runs']
        found = latest_checks(gh, sha)
        running = [item for item in [*workflows, *found.values()] if item['status'] != 'completed']
        # A run cancelled by a newer one of the same workflow counts only once nothing runs any more.
        failed = [c for c in found.values() if c['status'] == 'completed' and c['conclusion'] not in PASSED
                  and not (c['conclusion'] == 'cancelled' and running)]
        settled = pr['merged'] or failed or pr['mergeable_state'] in ('dirty', 'behind') \
            or (workflows and not running and not pr['auto_merge'])
        if settled or time.monotonic() >= deadline:
            break
        time.sleep(POLL_SECONDS)

    for check in sorted(found.values(), key=lambda c: c['name']):
        print(f'{check["name"]}: {check["conclusion"] or check["status"]}')
    for check in failed:
        print(f'\n{check["name"]} failed: {check["html_url"]}')
        for note in gh.call('GET', f'/repos/{gh.repo}/check-runs/{check["id"]}/annotations?per_page=50'):
            if note['annotation_level'] != 'notice':
                where = f'{note["path"]}:{note["start_line"]}'
                print(f'  {note["annotation_level"]} {where} {note["message"].strip()[:400]}')
    missing = [name for name in REQUIRED_CHECKS if name not in found]

    print()
    if pr['merged']:
        print(f'#{number} merged into {BASE} as {pr["merge_commit_sha"]}')
        return 0
    if failed:
        print(f'#{number} not merged: {len(failed)} check(s) failed')
        return 1
    if pr['mergeable_state'] in ('dirty', 'behind'):
        print(f'#{number} is {pr["mergeable_state"]} against {BASE}: rebase the branch on origin/{BASE}')
        return 1
    if running or not workflows:
        names = sorted({item.get('name') for item in running}) or ['nothing started yet']
        print(f'#{number} still running: {", ".join(names)}')
        return 3
    if missing:
        print(f'#{number}: these required checks did not run: {", ".join(missing)}')
        return 1
    if pr['auto_merge']:
        print(f'#{number}: every check passed, auto-merge has not merged yet (state: {pr["mergeable_state"]})')
        return 3
    print(f'#{number} open, every check passed, auto-merge off: the owner merges')
    return 0


def comment(gh, number, body_file):
    result = gh.call('POST', f'/repos/{gh.repo}/issues/{number}/comments', {'body': read(body_file)})
    print(result['html_url'])


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest='command', required=True)
    p = commands.add_parser('create')
    p.add_argument('--title', required=True)
    p.add_argument('--body-file', required=True)
    p.add_argument('--draft', action='store_true')
    commands.add_parser('ready').add_argument('number', type=int)
    p = commands.add_parser('auto-merge')
    p.add_argument('number', type=int)
    p.add_argument('--off', action='store_true')
    p = commands.add_parser('checks')
    p.add_argument('number', type=int)
    p.add_argument('--wait', type=int, default=0, help='seconds to wait for running checks (at most 540)')
    p = commands.add_parser('comment')
    p.add_argument('number', type=int)
    p.add_argument('--body-file', required=True)
    args = parser.parse_args()

    try:
        gh = GitHub()
        if args.command == 'create':
            return create(gh, args.title, args.body_file, args.draft)
        if args.command == 'ready':
            return ready(gh, args.number)
        if args.command == 'auto-merge':
            return auto_merge(gh, args.number, args.off)
        if args.command == 'checks':
            return checks(gh, args.number, min(max(args.wait, 0), 540))
        return comment(gh, args.number, args.body_file)
    except GitHubError as error:
        print(error, file=sys.stderr)
        return error.exit_code


if __name__ == '__main__':
    sys.exit(main())
