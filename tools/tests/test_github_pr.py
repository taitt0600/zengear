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

LOCK = 'web/src/components/ui/.shadcn-lock.json'

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
                self.assertIn(check, names, f'no job of .github/workflows/ is named "{check}"')

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
        self.base = self.commit({LOCK: json.dumps({'button.tsx': 'a' * 64}), 'tools/a.py': 'x = 1\n'})
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

    def test_a_new_lock_entry_is_not_an_owner_path(self):
        head = self.commit({LOCK: json.dumps({'button.tsx': 'a' * 64, 'card.tsx': 'b' * 64})})
        self.assertEqual(github_pr.owned_files(self.pull_request(head)), [])

    def test_a_changed_hash_is_an_owner_path(self):
        head = self.commit({LOCK: json.dumps({'button.tsx': 'c' * 64})})
        self.assertEqual(github_pr.owned_files(self.pull_request(head)), [LOCK])

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

    def call(self, method, path, body=None):
        self.calls.append((method, path))
        if path.endswith('/branches/main'):
            return {'protection': {'required_status_checks': {'contexts': list(REQUIRED_CHECKS),
                                                              'enforcement_level': 'everyone'}}}
        return None


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


if __name__ == '__main__':
    unittest.main()
