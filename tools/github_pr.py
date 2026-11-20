"""GitHub steps of /finish-feature and /fix-defect, over the REST API (CLAUDE.md, Workflow).

  python tools/github_pr.py create --title "<title>" --body-file <file> [--draft]
  python tools/github_pr.py ready <number>
  python tools/github_pr.py auto-merge <number> [--off]
  python tools/github_pr.py checks <number> [--wait <seconds>]
  python tools/github_pr.py comment <number> --body-file <file>
  python tools/github_pr.py template [--closes <issue>]...

template prints .github/pull_request_template.md (DEC-150 (2)) to start the body from. create opens the pull request
of the current branch against main, or updates the one already open (title, body, and draft or ready as asked); it
refuses a body without the sections of the template, or with a Needs decision item but not --draft, as gate 11 of
tools/pr_gates.py would. auto-merge refuses when the pull request changes a file that only the owner
merges (owner_paths in tools/pr_gates.py, which judges web/package.json by content: the branch
of the pull request and main are fetched here and compared from their merge-base, DEC-111 (4)), or when main does
not require every check in REQUIRED_CHECKS by its rulesets, or lets someone bypass them (bypass_actors of a ruleset,
DEC-153 (3)), so that nothing merges unchecked; it turns
auto-merge off again when the head moved while it was being enabled. REQUIRED_CHECKS are job names, each equal to its job id:
ci-ok aggregates every job of .github/workflows/pipeline.yml, and pr-gates and pr-title run in
.github/workflows/gates.yml; code-review is the check run that the job review-check of .github/workflows/code-review.yml
posts, success only with a verdict comment pass for the head (DEC-150 (3)). checks prints the latest run of every check of the head commit, the annotations of the failed ones and the merge state.

Exit codes: 0 done, 1 a check failed or the pull request cannot merge as it is, 3 still running when the wait
ended, 4 GitHub could not be reached, 5 the owner merges (auto-merge refused).

In a cloud session the GitHub proxy adds the credential (GH_TOKEN reads "proxy-injected") and serves no
GraphQL, so draft, ready and auto-merge use its routes under /pulls/<number>/ccr/. Elsewhere the token comes
from GH_TOKEN, GITHUB_TOKEN or `gh auth token` (tools/github_api.py), and those three use GraphQL. Job logs are not reachable from
a cloud session: reproduce a failed job with its commands in .github/workflows/."""
import argparse, os, subprocess, sys, time, urllib.parse

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from github_api import GitHub, GitHubError, Unreachable, run  # noqa: E402,F401  (Unreachable: exit code 4)
import pr_gates  # noqa: E402
from pr_gates import body_errors, changed_files, owner_paths  # noqa: E402

BASE = 'main'
# code-review (DEC-150 (3)) is not a job: the job review-check posts it from the verdict comments; the owner made it a
# required check of main after its first run (#251).
REQUIRED_CHECKS = ('ci-ok', 'pr-gates', 'pr-title', 'code-review')
PASSED = ('success', 'neutral', 'skipped')
POLL_SECONDS = 30
TEMPLATE = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), '.github',
                        'pull_request_template.md')


class OwnerMerges(GitHubError):
    exit_code = 5


def read(path):
    with open(path, encoding='utf-8') as f:
        return f.read()


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
    # The sections of .github/pull_request_template.md, as gate 11 of tools/pr_gates.py checks them (DEC-150 (2)).
    errors = body_errors(body, draft)
    if errors:
        raise GitHubError(f'{body_file}: ' + '; '.join(errors) + f' (start from: python tools/github_pr.py template)')
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


def has_status_checks(rules):
    return any(rule.get('type') == 'required_status_checks' for rule in rules or [])


def ruleset_checks(gh):
    """The checks the active rulesets require of main (GET /rules/branches/<branch>, REST 2022-11-28), and the gaps
    of every active branch ruleset that requires checks: someone in its bypass_actors (any bypass_mode, exempt
    included), or bypass_actors not returned (only to a token with write access to the ruleset: unverifiable).
    The items of /rules/branches carry no documented ruleset id, so every such ruleset is judged, not only main's."""
    rules = gh.pages(f'/repos/{gh.repo}/rules/branches/{BASE}')
    required = {check['context'] for rule in rules if rule.get('type') == 'required_status_checks'
                for check in (rule.get('parameters') or {}).get('required_status_checks', [])}
    if not required:
        return required, []
    gaps, found = [], False
    for summary in gh.pages(f'/repos/{gh.repo}/rulesets'):
        ruleset = gh.call('GET', f'/repos/{gh.repo}/rulesets/{summary["id"]}')
        if ruleset.get('target') != 'branch' or ruleset.get('enforcement') != 'active' \
                or not has_status_checks(ruleset.get('rules')):
            continue
        found = True
        name = f'ruleset "{ruleset.get("name")}" (#{ruleset["id"]})'
        if 'bypass_actors' not in ruleset:
            gaps.append(f'cannot read who bypasses {name} (the token has no write access to it)')
        elif ruleset['bypass_actors']:
            actors = ', '.join(f'{a.get("actor_type")} {a.get("actor_id")} ({a.get("bypass_mode", "always")})'
                               for a in ruleset['bypass_actors'])
            gaps.append(f'{name} lets someone bypass the required checks: {actors}')
    if not found:
        gaps.append(f'cannot find the ruleset that requires checks of {BASE}, so cannot verify who bypasses it')
    return required, gaps


def protection_gaps(gh):
    """Every check of REQUIRED_CHECKS is required of main by its rulesets, and no ruleset that requires checks can be
    bypassed (DEC-153 (3); the classic branch protection of main was deleted and is not read)."""
    required, gaps = ruleset_checks(gh)
    return [f'{BASE} does not require "{name}"' for name in REQUIRED_CHECKS if name not in required] + gaps


def issue_gaps(gh, pr):
    """Gates 9 and 10 again for a feat pull request of a feature: a blocker added or reopened after pr-gates passed
    does not run the gates again (DEC-148 (6), (12))."""
    title = pr.get('title') or ''
    if not (pr_gates.FEAT_TITLE.match(title) and pr_gates.FEATURE_IN_TITLE.search(title)):
        return []
    import issue
    return pr_gates.issue_errors(pr, pr_gates.closing_issues(pr.get('body')), issue.Reader(gh))


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
    gaps = issue_gaps(gh, pr)
    if gaps:
        raise GitHubError(f'Auto-merge not enabled: #{number}: ' + '; '.join(gaps))
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


def review_running(gh, number):
    """True while a code-review run started by a comment on this pull request is queued or running; its run name is
    "code-review #<number>" (run-name of .github/workflows/code-review.yml)."""
    path = f'/repos/{gh.repo}/actions/workflows/code-review.yml/runs?event=issue_comment&per_page=50'
    try:
        runs = gh.call('GET', path).get('workflow_runs', [])
    except GitHubError as error:
        if getattr(error, 'status', None) == 404:  # the workflow is not on main yet
            return False
        raise
    return any(run.get('display_title') == f'code-review #{number}'
               and run.get('status') in ('queued', 'in_progress', 'waiting', 'pending', 'requested') for run in runs)


def checks(gh, number, wait):
    deadline = time.monotonic() + wait
    while True:
        pr = gh.pull(number)
        sha = pr['head']['sha']
        workflows = gh.call('GET', f'/repos/{gh.repo}/actions/runs?head_sha={sha}&per_page=100')['workflow_runs']
        found = latest_checks(gh, sha)
        running = [item for item in [*workflows, *found.values()] if item['status'] != 'completed']
        # The run of code-review that a verdict comment starts (issue_comment) is not attached to the head, so it is
        # not in workflows: while one is queued or running, its failure of the push before is not final (DEC-150 (3)).
        review_pending = found.get('code-review', {}).get('conclusion') == 'failure' and review_running(gh, number)
        if review_pending:
            running.append({'name': 'code-review', 'status': 'in_progress'})
        # A run cancelled by a newer one of the same workflow counts only once nothing runs any more.
        failed = [c for c in found.values() if c['status'] == 'completed' and c['conclusion'] not in PASSED
                  and not (c['conclusion'] == 'cancelled' and running)
                  and not (c['name'] == 'code-review' and review_pending)]
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


def template(closes):
    """The pull request template, with the Closes line filled when issues are given."""
    text = read(TEMPLATE)
    if closes:
        text = text.replace('Closes #<issue>', ', '.join(f'Closes #{number}' for number in closes))
    return text


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
    p = commands.add_parser('template')
    p.add_argument('--closes', type=int, action='append', default=[])
    args = parser.parse_args()

    if args.command == 'template':
        sys.stdout.reconfigure(encoding='utf-8')
        sys.stdout.write(template(args.closes))
        return 0
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
