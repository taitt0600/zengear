"""Tests of tools/git_tidy.ps1 (DEC-120 (2)) in a throw-away clone of a bare origin, with a fake gh first on PATH:
the dry run changes nothing; -Apply deletes a local branch whose upstream is gone only when gh shows a merged pull
request of this repository at the same head, removing its worktree first, and keeps every other branch, main,
release/* and the current branch; without a working gh it deletes nothing; -SyncMain switches to main and pulls
only on a clean tree. Skipped when pwsh or git is missing.
Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import json
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from fakes import install  # noqa: E402

REPO = Path(__file__).resolve().parents[2]
SCRIPT = Path(os.environ.get('GIT_TIDY_SCRIPT', REPO / 'tools' / 'git_tidy.ps1'))
GIT = shutil.which('git')
PWSH = shutil.which('pwsh')

# The fake gh answers `gh pr list --head <branch> ...` from FAKE_GH_PRS ({branch: [pull requests]}) and fails for the
# branches of FAKE_GH_FAIL (comma-separated, * for all).
FAKE_GH = '''import json, os, sys
args = sys.argv[1:]
head = args[args.index('--head') + 1] if '--head' in args else ''
failing = os.environ.get('FAKE_GH_FAIL', '').split(',')
if '*' in failing or head in failing:
    sys.stderr.write('gh: failed\\n')
    sys.exit(1)
sys.stdout.write(json.dumps(json.loads(os.environ.get('FAKE_GH_PRS', '{}')).get(head, [])))
'''


@unittest.skipUnless(PWSH and GIT, 'pwsh and git are needed')
class GitTidyTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp()).resolve()
        self.origin = self.dir / 'origin.git'
        self.work = self.dir / 'work'
        self.work.mkdir()
        self.fakes = self.dir / 'fakes'
        install(self.fakes, 'gh', FAKE_GH)
        subprocess.run([GIT, 'init', '-q', '--bare', '-b', 'main', str(self.origin)], check=True, capture_output=True)
        self.git('init', '-q', '-b', 'main')
        for key, value in (('user.name', 'test'), ('user.email', 'test@example.com'), ('core.autocrlf', 'false'),
                           ('commit.gpgsign', 'false'), ('core.hooksPath', str(self.dir / 'no-hooks'))):
            self.git('config', key, value)
        self.git('remote', 'add', 'origin', str(self.origin))
        self.commit('a.txt')
        self.git('push', '-q', '-u', 'origin', 'main')

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def git(self, *args, cwd=None):
        return subprocess.run([GIT, *args], cwd=cwd or self.work, check=True, capture_output=True, text=True,
                              encoding='utf-8').stdout.strip()

    def commit(self, name):
        (self.work / name).write_text(name + '\n', encoding='utf-8')
        self.git('add', name)
        self.git('commit', '-q', '-m', f'add {name}')

    def branch(self, name, gone=True):
        """A pushed branch with one commit; gone: deleted on origin, as GitHub does after the merge. Returns its head."""
        self.git('switch', '-q', '-c', name, 'main')
        self.commit(name.replace('/', '-') + '.txt')
        self.git('push', '-q', '-u', 'origin', name)
        self.git('switch', '-q', 'main')
        if gone:
            self.git('branch', '-q', '-D', name, cwd=self.origin)
        return self.git('rev-parse', name)

    def branches(self):
        return set(self.git('for-each-ref', '--format=%(refname:short)', 'refs/heads').splitlines())

    @staticmethod
    def merged(head, number=1, fork=False):
        return [{'number': number, 'headRefOid': head, 'isCrossRepository': fork}]

    def run_tidy(self, *args, prs=None, fail='', gh=True):
        env = {**os.environ, 'FAKE_GH_PRS': json.dumps(prs or {}), 'FAKE_GH_FAIL': fail}
        if gh:
            env['PATH'] = str(self.fakes) + os.pathsep + os.environ['PATH']
        elif os.name == 'nt':
            # Git for Windows holds no gh, but git fetch needs its other folders (a crash without usr/bin).
            root = next((p for p in Path(GIT).parents if (p / 'usr' / 'bin').is_dir()), Path(GIT).parent)
            env['PATH'] = os.pathsep.join([str(Path(GIT).parent), str(root / 'cmd'), str(root / 'mingw64' / 'bin'),
                                           str(root / 'usr' / 'bin'),
                                           os.path.join(os.environ.get('SystemRoot', r'C:\Windows'), 'System32')])
        else:
            # A runner keeps a real gh next to git in /usr/bin, so only a wrapper of git is on PATH.
            only_git = self.dir / 'only-git'
            install(only_git, 'git', target=GIT)
            env['PATH'] = str(only_git)
        run = subprocess.run([PWSH, '-NoProfile', '-File', str(SCRIPT), *args], capture_output=True, text=True,
                             encoding='utf-8', env=env, cwd=self.work, timeout=120)
        return run.returncode, run.stdout + run.stderr

    def test_the_dry_run_changes_nothing(self):
        head = self.branch('feat/a')
        self.git('fetch', '-q', '--prune')
        code, out = self.run_tidy(prs={'feat/a': self.merged(head)})
        self.assertEqual(code, 0, out)
        self.assertIn('would delete feat/a', out)
        self.assertIn('feat/a', self.branches())

    def test_a_merged_gone_branch_at_the_same_head_is_deleted(self):
        head = self.branch('feat/a')
        code, out = self.run_tidy('-Apply', prs={'feat/a': self.merged(head)})
        self.assertEqual(code, 0, out)
        self.assertIn('delete feat/a (pull request #1 merged)', out)
        self.assertEqual(self.branches(), {'main'})

    def test_every_other_branch_is_kept(self):
        heads = {name: self.branch(name) for name in ('feat/fork', 'feat/moved', 'feat/no-pr', 'release/1.0',
                                                      'feat/current')}
        alive = self.branch('feat/alive', gone=False)
        self.git('branch', 'feat/local')
        self.git('switch', '-q', 'feat/current')
        prs = {'feat/fork': self.merged(heads['feat/fork'], fork=True), 'feat/moved': self.merged('0' * 40),
               'release/1.0': self.merged(heads['release/1.0']), 'feat/current': self.merged(heads['feat/current']),
               'feat/alive': self.merged(alive), 'feat/local': self.merged(self.git('rev-parse', 'feat/local'))}
        code, out = self.run_tidy('-Apply', prs=prs)
        self.assertEqual(code, 0, out)
        self.assertEqual(self.branches(), {'main', 'feat/fork', 'feat/moved', 'feat/no-pr', 'release/1.0',
                                           'feat/current', 'feat/alive', 'feat/local'})
        for reason in ('kept feat/fork: pull request #1 is from a fork', 'kept feat/moved: its head differs',
                       'kept feat/no-pr: no merged pull request', 'kept release/1.0: never deleted',
                       'kept feat/current: the current branch'):
            self.assertIn(reason, out)
        # No upstream, or an upstream still there: never a candidate, whatever gh would say.
        self.assertNotIn('feat/local', out)
        self.assertNotIn('feat/alive', out)

    def test_a_worktree_is_removed_before_its_branch(self):
        head = self.branch('build/approve-x')
        worktree = self.dir / 'approve-x'
        self.git('worktree', 'add', '-q', str(worktree), 'build/approve-x')
        code, out = self.run_tidy('-Apply', prs={'build/approve-x': self.merged(head)})
        self.assertEqual(code, 0, out)
        self.assertFalse(worktree.exists())
        self.assertEqual(self.branches(), {'main'})

    def test_a_dirty_worktree_keeps_its_branch(self):
        head = self.branch('build/approve-x')
        worktree = self.dir / 'approve-x'
        self.git('worktree', 'add', '-q', str(worktree), 'build/approve-x')
        (worktree / 'notes.txt').write_text('not committed\n', encoding='utf-8')
        code, out = self.run_tidy('-Apply', prs={'build/approve-x': self.merged(head)})
        self.assertEqual(code, 1, out)
        self.assertIn('kept build/approve-x: its worktree could not be removed', out)
        self.assertTrue((worktree / 'notes.txt').exists())
        self.assertIn('build/approve-x', self.branches())

    def test_a_failed_gh_call_after_a_working_one_keeps_that_branch(self):
        heads = {name: self.branch(name) for name in ('feat/a', 'feat/b')}
        prs = {name: self.merged(head) for name, head in heads.items()}
        code, out = self.run_tidy('-Apply', prs=prs, fail='feat/b')
        self.assertEqual(code, 1, out)
        self.assertIn('kept feat/b: gh pr list failed', out)
        self.assertEqual(self.branches(), {'main', 'feat/b'})

    def test_without_a_working_gh_only_pruning_happens(self):
        head = self.branch('feat/a')
        for label, options in (('missing', {'gh': False}), ('failing', {'fail': '*'})):
            with self.subTest(gh=label):
                code, out = self.run_tidy('-Apply', prs={'feat/a': self.merged(head)}, **options)
                self.assertEqual(code, 0, out)
                self.assertIn('fetch with --prune', out)
                self.assertIn('gh is not available', out)
                self.assertIn('feat/a', self.branches())

    def test_sync_main_without_apply_only_says_it(self):
        head = self.branch('feat/x')
        self.git('fetch', '-q', '--prune')
        self.git('switch', '-q', 'feat/x')
        code, out = self.run_tidy('-SyncMain', prs={'feat/x': self.merged(head)})
        self.assertEqual(code, 0, out)
        self.assertIn('would switch to main', out)
        # As -Apply would, once on main: the branch it leaves is no longer the current one.
        self.assertIn('would delete feat/x', out)
        self.assertEqual(self.git('branch', '--show-current'), 'feat/x')
        self.assertIn('feat/x', self.branches())

    def test_sync_main_refuses_a_dirty_tree(self):
        self.branch('feat/x', gone=False)
        self.git('switch', '-q', 'feat/x')
        (self.work / 'a.txt').write_text('changed\n', encoding='utf-8')
        code, out = self.run_tidy('-Apply', '-SyncMain')
        self.assertEqual(code, 1, out)
        self.assertIn('refused to switch to main', out)
        self.assertEqual(self.git('branch', '--show-current'), 'feat/x')

    def test_sync_main_switches_pulls_and_frees_the_branch_it_left(self):
        head = self.branch('feat/x')
        self.git('switch', '-q', 'feat/x')
        other = self.dir / 'other'
        subprocess.run([GIT, 'clone', '-q', str(self.origin), str(other)], check=True, capture_output=True)
        for key, value in (('user.name', 'test'), ('user.email', 'test@example.com'), ('commit.gpgsign', 'false')):
            self.git('config', key, value, cwd=other)
        (other / 'b.txt').write_text('b\n', encoding='utf-8')
        self.git('add', 'b.txt', cwd=other)
        self.git('commit', '-q', '-m', 'add b', cwd=other)
        self.git('push', '-q', 'origin', 'main', cwd=other)
        code, out = self.run_tidy('-Apply', '-SyncMain', prs={'feat/x': self.merged(head)})
        self.assertEqual(code, 0, out)
        self.assertEqual(self.git('branch', '--show-current'), 'main')
        self.assertEqual(self.git('rev-parse', 'main'), self.git('rev-parse', 'HEAD', cwd=other))
        self.assertEqual(self.branches(), {'main'})


if __name__ == '__main__':
    unittest.main()
