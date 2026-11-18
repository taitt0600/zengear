"""Tests of the commit message convention (DEC-150 (1), AGENTS.md, Commits): the schema_pattern of .cz.toml as cz check
applies it (re.match on the whole message, comments removed), and .githooks/commit-msg with Commitizen when it is
installed (CI installs tools/requirements.txt).

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import os
import re
import shutil
import subprocess
import tempfile
import tomllib
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
CONFIG = tomllib.loads((REPO / '.cz.toml').read_text(encoding='utf-8'))['tool']['commitizen']
PATTERN = re.compile(CONFIG['customize']['schema_pattern'])

VALID = ['feat(api): add role-based access control\n\nWhat and why.\n\nRefs: AUT-04, #12\n',
         'docs: x\n', 'ci: no final newline', 'feat!: drop the old route\n', 'fix(web): keep 1 total\n',
         'feat(api): add role-based access control (AUT-04)', 'feat: ' + 'a' * 72 + '\n',
         'revert: undo the gate\n\nThis reverts commit abc.\n', 'build(api): pin x\n\n']
INVALID = ['Feat: add x\n', 'feat(api): Add x\n', 'feat(api): add x.\n', 'feat(api): add x\nbody\n',
           'feature: add x\n', 'feat(core): add x\n', 'feat: ' + 'a' * 73 + '\n', 'feat:add x\n', 'feat: \n',
           'feat(api) add x\n', '']


def checked(message):
    """The verdict of cz check for a message, without cz: allowed prefixes pass, else the pattern from the start."""
    message = '\n'.join(line for line in message.split('\n') if not line.startswith('#'))
    if any(message.startswith(prefix) for prefix in CONFIG['allowed_prefixes']):
        return True
    return bool(message) and bool(PATTERN.match(message))


class PatternTests(unittest.TestCase):
    def test_valid_messages(self):
        for message in VALID:
            with self.subTest(message=message):
                self.assertTrue(checked(message))

    def test_invalid_messages(self):
        for message in INVALID:
            with self.subTest(message=message):
                self.assertFalse(checked(message))

    def test_git_messages_of_the_old_hook_pass(self):
        for message in ('Merge branch x\n', 'Revert "feat: x"\n', 'fixup! feat: x\n', 'squash! x\n', 'amend! x\n'):
            with self.subTest(message=message):
                self.assertTrue(checked(message))

    def test_comment_lines_are_ignored(self):
        self.assertTrue(checked('# Please enter the commit message\nfix: y\n'))

    def test_types_and_scopes_of_claude_md(self):
        self.assertIn('(feat|fix|docs|refactor|perf|test|build|ci|chore|revert)', PATTERN.pattern)
        self.assertIn('(api|web|db|seed|docs|ci)', PATTERN.pattern)


def commitizen():
    for command in (['cz'], ['python3', '-m', 'commitizen'], ['python', '-m', 'commitizen']):
        if shutil.which(command[0]) and subprocess.run([*command, 'version'], capture_output=True).returncode == 0:
            return command
    return None


class PullRequestCheckTests(unittest.TestCase):
    """The job pr-title refuses the prefixes that the local hook allows (fixup!, squash!, amend!, Merge)."""

    def test_every_cz_check_of_gates_yml_empties_the_allowed_prefixes(self):
        text = (REPO / '.github' / 'workflows' / 'gates.yml').read_text(encoding='utf-8')
        calls = [line.strip() for line in text.splitlines() if 'cz check' in line and not line.strip().startswith('#')]
        self.assertEqual(len(calls), 2, calls)
        for call in calls:
            with self.subTest(call=call):
                self.assertTrue(call.endswith('--allowed-prefixes'), call)

    @unittest.skipUnless(commitizen(), 'Commitizen (tools/requirements.txt) is needed')
    def test_cz_refuses_a_fixup_with_the_option_of_ci(self):
        command = commitizen()
        for message, code in (('fixup! feat: x', False), ('Merge branch x', False), ('squash! x', False),
                              ('docs: x', True)):
            with self.subTest(message=message):
                ci = subprocess.run([*command, 'check', '--message', message, '--allowed-prefixes'], cwd=REPO,
                                    capture_output=True)
                self.assertEqual(ci.returncode == 0, code)
        local = subprocess.run([*command, 'check', '--message', 'fixup! feat: x'], cwd=REPO, capture_output=True)
        self.assertEqual(local.returncode, 0)


@unittest.skipUnless(shutil.which('sh') and commitizen(), 'sh and Commitizen (tools/requirements.txt) are needed')
class HookTests(unittest.TestCase):
    """The hook in a throw-away repository with .cz.toml, as git runs it from the root of the clone."""

    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        shutil.copy(REPO / '.cz.toml', self.dir / '.cz.toml')
        for args in (['init', '-q'], ['config', 'user.name', 'taitt0600'], ['config', 'user.email', 'owner@example.com']):
            subprocess.run(['git', *args], cwd=self.dir, check=True, capture_output=True)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def hook(self, message):
        path = self.dir / 'MSG'
        path.write_text(message, encoding='utf-8', newline='')
        run = subprocess.run(['sh', str(REPO / '.githooks' / 'commit-msg'), str(path)], cwd=self.dir,
                             capture_output=True, text=True, env={**os.environ})
        return run.returncode, run.stderr

    def test_valid_and_invalid(self):
        self.assertEqual(self.hook('feat(api): add x\n\nRefs: AUT-04, #12\n')[0], 0)
        code, err = self.hook('feat(api): Add x\n')
        self.assertEqual(code, 1)
        self.assertIn('does not follow the convention', err)

    def test_a_windows_line_ending_passes(self):
        self.assertEqual(self.hook('docs: x\r\n\r\nbody\r\n')[0], 0)

    def test_a_claude_trailer_fails(self):
        code, err = self.hook('docs: x\n\nCo-Authored-By: Claude <noreply@anthropic.com>\n')
        self.assertEqual(code, 1)
        self.assertIn('trailers', err)


if __name__ == '__main__':
    unittest.main()
