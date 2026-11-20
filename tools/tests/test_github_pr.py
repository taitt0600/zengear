"""Tests of tools/github_pr.py: every check that auto-merge requires of main is the name of a job of a workflow in
.github/workflows/, so that renaming a job cannot leave auto-merge waiting for a check that never runs; and every
job is named after its id. auto-merge judges the owner paths on the branch of the pull request fetched from origin
(DEC-111 (4)), and turns auto-merge off when the head moved while it was enabled.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import contextlib
import io
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path
from unittest import mock

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))
import github_pr  # noqa: E402
from github_pr import REQUIRED_CHECKS  # noqa: E402

MANIFEST = 'web/package.json'
APPROVED = {'next': 'DEC-104', 'left-pad': 'DEC-104'}

# The name of a job: "name:" indented by four spaces under "jobs:" (the workflows are indented by two spaces).
JOB_NAME = re.compile(r'^    name: *(?P<quote>[\'"]?)(?P<name>.+?)(?P=quote) *$', re.MULTILINE)
# A job id ("  <id>:") followed at once by its name; every job of the repository is written this way.
JOB = re.compile(r'^  (?P<id>[A-Za-z0-9_-]+):\n    name: *(?P<quote>[\'"]?)(?P<name>.+?)(?P=quote) *$', re.MULTILINE)


def job_names():
    names = {}
    for workflow in sorted((REPO / '.github' / 'workflows').glob('*.yml')):
        text = workflow.read_text(encoding='utf-8')
        jobs = text.split('\njobs:\n', 1)
        for match in JOB_NAME.finditer(jobs[1] if len(jobs) == 2 else ''):
            names.setdefault(match['name'], []).append(workflow.name)
    return names


class RequiredChecksTests(unittest.TestCase):
    def test_every_required_check_is_a_job_of_a_workflow(self):
        names = job_names()
        for check in REQUIRED_CHECKS:
            with self.subTest(check=check):
                if check == 'code-review':  # a check run that a job posts (test below)
                    continue
                self.assertIn(check, names, f'no job of .github/workflows/ is named "{check}"')

    def test_code_review_is_required_and_posted_by_the_job_review_check(self):
        # DEC-150 (3): required since the owner made it a required check of main after its first run (#251). The job
        # that posts it is review-check; no job may be named code-review, or its own check run would pass for the verdict.
        self.assertIn('code-review', REQUIRED_CHECKS)
        names = job_names()
        self.assertEqual(names.get('review-check'), ['code-review.yml'])
        self.assertNotIn('code-review', names)
        import issue

        class Gh:
            repo = 'taitt0600/zengear'
            posted = []

            def pages(self, path, limit=1000):
                return []

            def call(self, method, path, body=None):
                self.posted.append((method, path, body))

        gh = Gh()
        event = {'pull_request': {'number': 7, 'head': {'sha': 'a' * 40}, 'user': {'login': 'taitt0600'}}}
        with contextlib.redirect_stdout(io.StringIO()):
            issue.review_check(event, gh)
        (method, path, body), = gh.posted
        self.assertEqual((method, path, body['name'], body['head_sha']),
                         ('POST', '/repos/taitt0600/zengear/check-runs', 'code-review', 'a' * 40))

    def test_every_job_is_named_after_its_id(self):
        for workflow in sorted((REPO / '.github' / 'workflows').glob('*.yml')):
            jobs = workflow.read_text(encoding='utf-8').split('\njobs:\n', 1)
            for match in JOB.finditer(jobs[1] if len(jobs) == 2 else ''):
                with self.subTest(workflow=workflow.name, job=match['id']):
                    self.assertEqual(match['name'], match['id'])

    def test_ci_ok_and_the_gates_run_where_the_docstring_says(self):
        names = job_names()
        self.assertEqual(names.get('ci-ok'), ['pipeline.yml'])
        self.assertEqual(names.get('pr-gates'), ['gates.yml'])
        self.assertEqual(names.get('pr-title'), ['gates.yml'])


@unittest.skipUnless(shutil.which('git'), 'git is needed')
class OwnedFilesTests(unittest.TestCase):
    """owned_files against a bare repository as origin, the pull request at refs/pull/1/head as on GitHub."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        self.work = self.dir / 'work'
        self.work.mkdir()
        self.git('init', '-q', '--bare', '-b', 'main', str(self.dir / 'origin.git'))
        for args in (['init', '-q', '-b', 'main'], ['config', 'user.name', 'taitt0600'],
                     ['config', 'user.email', 'taitt.dev@gmail.com'], ['config', 'commit.gpgsign', 'false'],
                     ['config', 'core.hooksPath', '/dev/null'], ['remote', 'add', 'origin', str(self.dir / 'origin.git')]):
            self.git(*args)
        self.base = self.commit({MANIFEST: json.dumps({'dependencies': {'next': '16.0.0'}}),
                                 'web/approved-packages.json': json.dumps(APPROVED), 'tools/a.py': 'x = 1\n'})
        self.git('push', '-q', 'origin', 'main')
        self.cwd = os.getcwd()
        os.chdir(self.work)

    def tearDown(self):
        os.chdir(self.cwd)
        shutil.rmtree(self.dir, ignore_errors=True)

    def git(self, *args):
        return subprocess.run(['git', *args], cwd=self.work, capture_output=True, text=True, check=True).stdout.strip()

    def commit(self, files):
        for name, text in files.items():
            path = self.work / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding='utf-8')
        self.git('add', '-A')
        self.git('commit', '-q', '-m', 'chore: change')
        return self.git('rev-parse', 'HEAD')

    def pull_request(self, head):
        self.git('push', '-q', '--force', 'origin', f'{head}:refs/pull/1/head')
        return {'number': 1, 'head': {'sha': head}}

    def test_an_approved_package_at_an_exact_version_is_not_an_owner_path(self):
        head = self.commit({MANIFEST: json.dumps({'dependencies': {'next': '16.0.0', 'left-pad': '1.3.0'}})})
        self.assertEqual(github_pr.owned_files(self.pull_request(head)), [])

    def test_a_new_script_is_an_owner_path(self):
        head = self.commit({MANIFEST: json.dumps({'dependencies': {'next': '16.0.0'}, 'scripts': {'build': 'x'}})})
        self.assertEqual(github_pr.owned_files(self.pull_request(head)), [MANIFEST])

    def test_a_rename_lists_the_old_path(self):
        (self.work / 'docs').mkdir()
        self.git('mv', 'tools/a.py', 'docs/a.py')
        head = self.commit({})
        self.assertEqual(github_pr.owned_files(self.pull_request(head)), ['tools/a.py'])

    def test_a_non_ascii_name_is_not_quoted_past_the_owner_paths(self):
        head = self.commit({'tools/é.py': 'x = 1\n'})
        self.assertEqual(github_pr.owned_files(self.pull_request(head)), ['tools/é.py'])

    def test_a_head_that_moved_is_refused(self):
        head = self.commit({'docs/x.md': 'x\n'})
        self.pull_request(self.commit({'docs/y.md': 'y\n'}))
        with self.assertRaisesRegex(github_pr.GitHubError, 'moved'):
            github_pr.owned_files({'number': 1, 'head': {'sha': head}})

    def test_a_failed_fetch_is_a_github_error(self):
        head = self.commit({'docs/x.md': 'x\n'})
        self.git('remote', 'set-url', 'origin', str(self.dir / 'missing.git'))
        with self.assertRaisesRegex(github_pr.GitHubError, 'cannot read #1 with git'):
            github_pr.owned_files({'number': 1, 'head': {'sha': head}})


class FakeGitHub:
    """The calls auto_merge makes through the proxy routes; pull() returns the heads given, one per call."""
    proxy = True
    repo = 'taitt0600/zengear'

    def __init__(self, heads):
        self.heads = iter(heads)
        self.calls = []

    def pull(self, number):
        return {'number': number, 'state': 'open', 'base': {'ref': 'main'}, 'draft': False, 'node_id': 'PR_x',
                'head': {'sha': next(self.heads)}, 'auto_merge': {'merge_method': 'rebase'}}

    def pages(self, path, limit=1000):
        return self.call('GET', f'{path}?per_page=100&page=1')

    def call(self, method, path, body=None):
        self.calls.append((method, path))
        if '/rules/branches/main' in path:
            return []
        if path.endswith('/branches/main'):
            return {'protection': {'required_status_checks': {'contexts': list(REQUIRED_CHECKS),
                                                              'enforcement_level': 'everyone'}}}
        return None


def status_rule(*contexts):
    return {'type': 'required_status_checks',
            'parameters': {'strict_required_status_checks_policy': True,
                           'required_status_checks': [{'context': c} for c in contexts]}}


class ProtectionGitHub:
    """The GETs of protection_gaps, on the exact paths GitHub.pages and GitHub.call send."""
    repo = 'taitt0600/zengear'

    def __init__(self, rules=(), rulesets=(), classic=None):
        self.rules, self.rulesets, self.classic = list(rules), {r['id']: r for r in rulesets}, classic
        self.calls = []

    def pages(self, path, limit=1000):
        return self.call('GET', f'{path}?per_page=100&page=1')

    def call(self, method, path, body=None):
        self.calls.append(path)
        base = f'/repos/{self.repo}'
        if path == f'{base}/rules/branches/main?per_page=100&page=1':
            return self.rules
        if path == f'{base}/rulesets?per_page=100&page=1':
            return [{'id': i} for i in self.rulesets]
        if path.startswith(f'{base}/rulesets/'):
            return self.rulesets[int(path.rsplit('/', 1)[1])]
        if path == f'{base}/branches/main':
            return {'protection': self.classic} if self.classic else {'protection': {
                'enabled': False, 'required_status_checks': {'enforcement_level': 'off', 'contexts': []}}}
        raise AssertionError(path)


def ruleset(rid=1, checks=REQUIRED_CHECKS, bypass=(), **extra):
    data = {'id': rid, 'name': 'main', 'target': 'branch', 'enforcement': 'active', 'bypass_actors': list(bypass),
            'rules': [{'type': 'deletion'}, status_rule(*checks)]}
    data.update(extra)
    return data


ADMIN = {'actor_id': 5, 'actor_type': 'RepositoryRole', 'bypass_mode': 'always'}


class ProtectionGapsTests(unittest.TestCase):
    def test_a_ruleset_with_the_four_checks_and_no_bypass_is_enough(self):
        gh = ProtectionGitHub([status_rule(*REQUIRED_CHECKS)], [ruleset()])
        self.assertEqual(github_pr.protection_gaps(gh), [])

    def test_a_check_the_ruleset_misses_is_a_gap(self):
        checks = [c for c in REQUIRED_CHECKS if c != 'pr-title']
        gh = ProtectionGitHub([status_rule(*checks)], [ruleset(checks=checks)])
        self.assertEqual(github_pr.protection_gaps(gh), ['main does not require "pr-title"'])

    def test_a_bypass_actor_is_a_gap(self):
        for mode in ('always', 'pull_request', 'exempt'):
            with self.subTest(mode=mode):
                gh = ProtectionGitHub([status_rule(*REQUIRED_CHECKS)], [ruleset(bypass=[dict(ADMIN, bypass_mode=mode)])])
                gaps = github_pr.protection_gaps(gh)
                self.assertEqual(len(gaps), 1)
                self.assertIn('ruleset "main" (#1) lets someone bypass', gaps[0])
                self.assertIn(f'RepositoryRole 5 ({mode})', gaps[0])

    def test_bypass_actors_not_returned_is_a_gap(self):
        data = ruleset()
        del data['bypass_actors']
        gaps = github_pr.protection_gaps(ProtectionGitHub([status_rule(*REQUIRED_CHECKS)], [data]))
        self.assertEqual(len(gaps), 1)
        self.assertIn('cannot read who bypasses ruleset "main" (#1)', gaps[0])

    def test_checks_without_a_readable_ruleset_are_a_gap(self):
        gh = ProtectionGitHub([status_rule(*REQUIRED_CHECKS)], [ruleset(enforcement='disabled')])
        gaps = github_pr.protection_gaps(gh)
        self.assertEqual(len(gaps), 1)
        self.assertIn('cannot find the ruleset', gaps[0])

    def test_rulesets_without_status_checks_or_of_tags_are_not_judged(self):
        gh = ProtectionGitHub([status_rule(*REQUIRED_CHECKS)],
                              [ruleset(), ruleset(2, bypass=[ADMIN], rules=[{'type': 'deletion'}]),
                               ruleset(3, bypass=[ADMIN], target='tag')])
        self.assertEqual(github_pr.protection_gaps(gh), [])

    def test_each_ruleset_is_read_once(self):
        gh = ProtectionGitHub([status_rule('ci-ok', 'pr-gates'), status_rule('pr-title', 'code-review')],
                              [ruleset(rules=[status_rule('ci-ok', 'pr-gates'), status_rule('pr-title', 'code-review')])])
        self.assertEqual(github_pr.protection_gaps(gh), [])
        self.assertEqual(gh.calls.count('/repos/taitt0600/zengear/rulesets/1'), 1)

    def test_the_classic_protection_alone_is_enough(self):
        classic = {'required_status_checks': {'contexts': list(REQUIRED_CHECKS), 'enforcement_level': 'everyone'}}
        gh = ProtectionGitHub(classic=classic)
        self.assertEqual(github_pr.protection_gaps(gh), [])
        self.assertNotIn('/repos/taitt0600/zengear/rulesets?per_page=100&page=1', gh.calls)

    def test_a_classic_protection_administrators_bypass_is_a_gap_even_with_a_complete_ruleset(self):
        classic = {'required_status_checks': {'contexts': ['ci-ok'], 'enforcement_level': 'non_admins'}}
        gh = ProtectionGitHub([status_rule(*REQUIRED_CHECKS)], [ruleset()], classic)
        self.assertEqual(github_pr.protection_gaps(gh),
                         ['main lets administrators bypass the required checks ("Do not allow bypassing" is off)'])

    def test_checks_split_between_the_two_sources_are_enough(self):
        classic = {'required_status_checks': {'contexts': ['ci-ok', 'pr-gates'], 'enforcement_level': 'everyone'}}
        gh = ProtectionGitHub([status_rule('pr-title', 'code-review')],
                              [ruleset(checks=('pr-title', 'code-review'))], classic)
        self.assertEqual(github_pr.protection_gaps(gh), [])

    def test_no_protection_at_all_misses_every_check(self):
        self.assertEqual(github_pr.protection_gaps(ProtectionGitHub()),
                         [f'main does not require "{name}"' for name in REQUIRED_CHECKS])


@mock.patch('github_pr.owned_files', return_value=[])
class AutoMergeHeadTests(unittest.TestCase):
    def test_auto_merge_stays_on_when_the_head_did_not_move(self, _):
        gh = FakeGitHub(['a1', 'a1'])
        with contextlib.redirect_stdout(io.StringIO()):
            github_pr.auto_merge(gh, 1, False)
        self.assertIn(('PUT', '/repos/taitt0600/zengear/pulls/1/ccr/auto_merge'), gh.calls)
        self.assertNotIn(('DELETE', '/repos/taitt0600/zengear/pulls/1/ccr/auto_merge'), gh.calls)

    def test_auto_merge_is_turned_off_when_the_head_moved(self, _):
        gh = FakeGitHub(['a1', 'b2'])
        with self.assertRaisesRegex(github_pr.GitHubError, 'moved while auto-merge was enabled'):
            github_pr.auto_merge(gh, 1, False)
        self.assertEqual(gh.calls[-1], ('DELETE', '/repos/taitt0600/zengear/pulls/1/ccr/auto_merge'))


class ReviewRunningTests(unittest.TestCase):
    """A failure of code-review is not final while the run started by the verdict comment is queued or running."""

    class Gh:
        repo = 'taitt0600/zengear'

        def __init__(self, runs=None, error=None):
            self.runs, self.error = runs or [], error

        def call(self, method, path, body=None):
            if self.error:
                raise self.error
            return {'workflow_runs': self.runs}

    def test_a_queued_run_of_this_pull_request(self):
        runs = [{'status': 'completed', 'display_title': 'code-review #7'},
                {'status': 'queued', 'display_title': 'code-review #7'}]
        self.assertTrue(github_pr.review_running(self.Gh(runs), 7))

    def test_a_run_of_another_pull_request_does_not_count(self):
        runs = [{'status': 'in_progress', 'display_title': 'code-review #70'},
                {'status': 'queued', 'display_title': 'code-review #8'}]
        self.assertFalse(github_pr.review_running(self.Gh(runs), 7))

    def test_no_run(self):
        self.assertFalse(github_pr.review_running(self.Gh([{'status': 'completed', 'display_title': 'code-review #7'}]), 7))

    def test_workflow_not_on_main_yet(self):
        self.assertFalse(github_pr.review_running(self.Gh(error=github_pr.GitHubError('HTTP 404', 404)), 7))

    def test_the_run_name_of_the_workflow_holds_the_number(self):
        text = (REPO / '.github' / 'workflows' / 'code-review.yml').read_text(encoding='utf-8')
        self.assertIn("run-name: 'code-review #${{ github.event.pull_request.number || github.event.issue.number }}'",
                      text)


class TemplateTests(unittest.TestCase):
    """DEC-150 (2): the body starts from .github/pull_request_template.md and create refuses one without its sections."""

    def test_template_fills_the_closes_line(self):
        text = github_pr.template([12, 13])
        self.assertIn('Closes #12, Closes #13', text)
        self.assertNotIn('#<issue>', text)
        self.assertEqual(github_pr.body_errors(text, False), [])

    def test_create_refuses_a_body_without_the_sections(self):
        with tempfile.TemporaryDirectory() as folder:
            body = Path(folder) / 'body.md'
            body.write_text('Closes #1\n', encoding='utf-8')
            with mock.patch('github_pr.run', side_effect=['docs/1-x', 'abc refs/heads/docs/1-x', 'abc']):
                with self.assertRaisesRegex(github_pr.GitHubError, 'no section "## Needs decision"'):
                    github_pr.create(FakeGitHub([]), 'docs: x', str(body), False)

    def test_create_refuses_an_open_needs_decision_that_is_not_a_draft(self):
        text = github_pr.template([1]).replace('## Needs decision\n\nKhông có', '## Needs decision\n\n1. A or B')
        with tempfile.TemporaryDirectory() as folder:
            body = Path(folder) / 'body.md'
            body.write_text(text, encoding='utf-8')
            with mock.patch('github_pr.run', side_effect=['docs/1-x', 'abc refs/heads/docs/1-x', 'abc']):
                with self.assertRaisesRegex(github_pr.GitHubError, 'stays a draft'):
                    github_pr.create(FakeGitHub([]), 'docs: x', str(body), False)


@mock.patch('github_pr.owned_files', return_value=[])
class AutoMergeIssueTests(unittest.TestCase):
    def test_an_open_blocker_refuses_auto_merge(self, _):
        gh = FakeGitHub(['a1'])
        with mock.patch('github_pr.issue_gaps', return_value=['#1 is still blocked by the open issue #7']):
            with self.assertRaisesRegex(github_pr.GitHubError, 'blocked by the open issue #7'):
                github_pr.auto_merge(gh, 1, False)
        self.assertFalse([c for c in gh.calls if c[0] == 'PUT'])

    def test_issue_gaps_only_for_a_feat_pull_request_of_a_feature(self, _):
        self.assertEqual(github_pr.issue_gaps(FakeGitHub([]), {'title': 'docs: x', 'body': ''}), [])

    def test_issue_gaps_skip_a_blocker_the_body_closes(self, _):
        class Reader:
            def __init__(self, gh):
                pass

            def issue(self, number):
                return {'number': number, 'state': 'open', 'type': {'name': 'Feature' if number == 1 else 'Task'}}

            def feature_id(self, number):
                return 'POS-01' if number == 1 else None

            def open_feature_issues(self, fid):
                return [1]

            def open_blockers(self, number):
                return [{'number': 7, 'title': 'Decide'}, {'number': 8, 'title': 'Other'}]

        pr = {'title': 'feat(api): sell (POS-01)', 'body': 'Closes #1, closes #7', 'head': {'ref': 'feat/pos-01'}}
        with mock.patch('issue.Reader', Reader):
            gaps = github_pr.issue_gaps(FakeGitHub([]), pr)
        self.assertEqual(len(gaps), 1)
        self.assertIn('open issue #8', gaps[0])


if __name__ == '__main__':
    unittest.main()
