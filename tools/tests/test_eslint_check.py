"""Tests of .claude/hooks/eslint-check.ps1 with a fake eslint in a throw-away project (DEC-104 (11), DEC-111 (7)):
an edited .ts or .tsx file under web/ is linted alone with the local binary and without --fix, its errors go back to
Claude (exit 2), and everything else (another file, no web/node_modules, a parse error, ESLint's own failure) is
silent. Skipped when pwsh is missing.
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

REPO = Path(__file__).resolve().parents[2]
HOOK = Path(os.environ.get('ESLINT_CHECK_SCRIPT', REPO / '.claude' / 'hooks' / 'eslint-check.ps1'))

# The fake eslint logs its working folder and arguments, then prints FAKE_ESLINT_OUTPUT and exits FAKE_ESLINT_EXIT.
FAKE = '''import json, os, sys
with open(os.environ['FAKE_ESLINT_LOG'], 'a', encoding='utf-8') as log:
    log.write(json.dumps({'cwd': os.getcwd(), 'args': sys.argv[1:]}) + '\\n')
sys.stdout.write(os.environ.get('FAKE_ESLINT_OUTPUT', '[]'))
sys.exit(int(os.environ.get('FAKE_ESLINT_EXIT', '0')))
'''


def install_fake(bin_dir, name, script):
    """A fake <name> in bin_dir, for Windows (<name>.cmd) and POSIX, that runs script with this Python."""
    bin_dir.mkdir(parents=True, exist_ok=True)
    (bin_dir / f'fake_{name}.py').write_text(script, encoding='utf-8')
    (bin_dir / f'{name}.cmd').write_text(f'@"{sys.executable}" "%~dp0fake_{name}.py" %*\r\n', encoding='utf-8')
    posix = bin_dir / name
    posix.write_text(f'#!/bin/sh\nexec "{sys.executable}" "$(dirname "$0")/fake_{name}.py" "$@"\n', encoding='utf-8')
    posix.chmod(0o755)


def results(*messages, more=()):
    """ESLint's JSON: one result with messages, then one result per list of more."""
    return json.dumps([{'filePath': 'x', 'messages': list(messages)}] +
                      [{'filePath': 'y', 'messages': list(m)} for m in more])


ERROR = {'ruleId': 'no-unused-vars', 'severity': 2, 'message': "'a' is assigned a value but never used.",
         'line': 3, 'column': 7}
FATAL = {'ruleId': None, 'severity': 2, 'fatal': True, 'message': 'Parsing error: Unexpected token', 'line': 1,
         'column': 1}


@unittest.skipUnless(shutil.which('pwsh'), 'pwsh is needed')
class EslintCheckTests(unittest.TestCase):
    def setUp(self):
        self.root = Path(tempfile.mkdtemp()).resolve()
        self.web = self.root / 'web'
        self.log = self.root / 'eslint.log'
        install_fake(self.web / 'node_modules' / '.bin', 'eslint', FAKE)

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def file(self, relative):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text('const a = 1\n', encoding='utf-8')
        return path

    def run_hook(self, path, output='[]', exit_code=0, stdin=None):
        env = {**os.environ, 'CLAUDE_PROJECT_DIR': str(self.root), 'FAKE_ESLINT_LOG': str(self.log),
               'FAKE_ESLINT_OUTPUT': output, 'FAKE_ESLINT_EXIT': str(exit_code)}
        payload = stdin if stdin is not None else json.dumps({'tool_input': {'file_path': str(path)}})
        return subprocess.run(['pwsh', '-NoProfile', '-File', str(HOOK)], input=payload, capture_output=True,
                              text=True, encoding='utf-8', env=env, cwd=self.root, timeout=60)

    def calls(self):
        if not self.log.exists():
            return []
        return [json.loads(line) for line in self.log.read_text(encoding='utf-8').splitlines()]

    def test_errors_go_back_to_claude(self):
        run = self.run_hook(self.file('web/src/app/page.tsx'), results(ERROR), 1)
        self.assertEqual(run.returncode, 2, run.stderr)
        self.assertIn('ESLint errors in web/src/app/page.tsx', run.stderr)
        self.assertIn("web/src/app/page.tsx:3:7 no-unused-vars 'a' is assigned a value but never used.", run.stderr)
        [call] = self.calls()
        self.assertEqual(Path(call['cwd']).resolve(), self.web)
        self.assertEqual(call['args'][:4], ['--quiet', '--no-warn-ignored', '--format', 'json'])
        self.assertEqual(Path(call['args'][4]).as_posix(), 'src/app/page.tsx')
        self.assertFalse([a for a in call['args'] if a.startswith('--fix')])

    def test_a_route_with_brackets_and_parentheses(self):
        run = self.run_hook(self.file('web/src/app/[locale]/(shop)/page.tsx'), results(ERROR), 1)
        self.assertEqual(run.returncode, 2, run.stderr)
        self.assertIn('web/src/app/[locale]/(shop)/page.tsx:3:7 ', run.stderr)
        [call] = self.calls()
        self.assertEqual(Path(call['args'][4]).as_posix(), 'src/app/[locale]/(shop)/page.tsx')

    def test_only_the_errors_of_every_result_go_back(self):
        warning = {**ERROR, 'severity': 1, 'ruleId': 'a-warning'}
        second = {**ERROR, 'line': 9, 'ruleId': 'second-result'}
        run = self.run_hook(self.file('web/src/a.ts'), results(warning, FATAL, ERROR, more=[[second]]), 1)
        self.assertEqual(run.returncode, 2, run.stderr)
        self.assertIn(':3:7 no-unused-vars', run.stderr)
        self.assertIn(':9:7 second-result', run.stderr)
        self.assertNotIn('a-warning', run.stderr)
        self.assertNotIn('Parsing error', run.stderr)

    def test_a_ts_file_is_linted(self):
        run = self.run_hook(self.file('web/src/lib/money.ts'), results(ERROR), 1)
        self.assertEqual(run.returncode, 2, run.stderr)

    def test_no_error_is_silent(self):
        run = self.run_hook(self.file('web/src/a.ts'))
        self.assertEqual((run.returncode, run.stdout, run.stderr), (0, '', ''))
        self.assertEqual(len(self.calls()), 1)

    def test_a_parse_error_is_left_to_the_lsp(self):
        run = self.run_hook(self.file('web/src/a.ts'), results(FATAL), 1)
        self.assertEqual((run.returncode, run.stderr), (0, ''))

    def test_a_failure_of_eslint_is_silent(self):
        run = self.run_hook(self.file('web/src/a.ts'), 'Oops! Something went wrong!', 2)
        self.assertEqual((run.returncode, run.stderr), (0, ''))

    def test_output_that_is_not_json_is_silent(self):
        run = self.run_hook(self.file('web/src/a.ts'), 'not json', 1)
        self.assertEqual((run.returncode, run.stderr), (0, ''))

    def test_errors_are_cut_at_twenty(self):
        many = [{**ERROR, 'line': n} for n in range(1, 31)]
        run = self.run_hook(self.file('web/src/a.ts'), results(*many), 1)
        self.assertEqual(run.returncode, 2)
        self.assertIn(':20:7 ', run.stderr)
        self.assertNotIn(':21:7 ', run.stderr)

    def test_other_files_are_not_linted(self):
        for relative in ('web/messages/vi.json', 'web/src/app/globals.css', 'tools/x.ts', 'api/x.ts',
                         'web-old/x.ts'):
            with self.subTest(file=relative):
                run = self.run_hook(self.file(relative), results(ERROR), 1)
                self.assertEqual((run.returncode, run.stderr), (0, ''))
        self.assertEqual(self.calls(), [])

    def test_a_missing_file_is_not_linted(self):
        run = self.run_hook(self.web / 'src' / 'gone.ts', results(ERROR), 1)
        self.assertEqual(run.returncode, 0)
        self.assertEqual(self.calls(), [])

    def test_nothing_before_npm_ci(self):
        shutil.rmtree(self.web / 'node_modules')
        run = self.run_hook(self.file('web/src/a.ts'), results(ERROR), 1)
        self.assertEqual((run.returncode, run.stderr), (0, ''))
        self.assertEqual(self.calls(), [])

    def test_invalid_input_is_silent(self):
        run = self.run_hook(None, stdin='not json')
        self.assertEqual((run.returncode, run.stderr), (0, ''))


if __name__ == '__main__':
    unittest.main()
