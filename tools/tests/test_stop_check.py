"""Tests of .claude/hooks/stop-check.ps1 in a throw-away git repository, with a fake dotnet, tsc and next first on
PATH: the backend part builds and runs the architecture tests as before, the web part runs next typegen, then
tsc --noEmit, in web/ with the local binaries when web/ changed (DEC-104 (11), DEC-111 (7)), each state is checked
once, and the findings of both parts come back in one answer. Skipped when pwsh or git is missing.
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
HOOK = Path(os.environ.get('STOP_CHECK_SCRIPT', REPO / '.claude' / 'hooks' / 'stop-check.ps1'))
GIT = shutil.which('git')
# Resolved once: on POSIX a bare name is looked up in the PATH of the child, which a test empties of dotnet.
PWSH = shutil.which('pwsh')

# A fake <name> logs its working folder and arguments, then prints FAKE_<NAME>_OUTPUT and exits FAKE_<NAME>_EXIT.
FAKE = '''import json, os, sys
name = {name!r}
with open(os.environ['FAKE_LOG'], 'a', encoding='utf-8') as log:
    log.write(json.dumps({{'tool': name, 'cwd': os.getcwd(), 'args': sys.argv[1:]}}) + '\\n')
sys.stdout.write(os.environ.get(f'FAKE_{{name.upper()}}_OUTPUT', ''))
sys.exit(int(os.environ.get(f'FAKE_{{name.upper()}}_EXIT', '0')))
'''

CS_ERROR = r'D:\r\api\src\A.cs(3,1): error CS1002: ; expected [D:\r\api\src\A.csproj]'
LOCKED = r'D:\r\api\src\A.csproj : error MSB3027: Could not copy "x.dll". The file is locked by: "ZenGear.Api"'
TS_ERROR = "src/a.ts(1,7): error TS2322: Type 'number' is not assignable to type 'string'."


@unittest.skipUnless(PWSH and GIT, 'pwsh and git are needed')
class StopCheckTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp()).resolve()
        self.root = self.dir / 'repo'
        self.fakes = self.dir / 'fakes'
        self.log = self.dir / 'calls.log'
        install(self.fakes, 'dotnet', FAKE.format(name='dotnet'))
        for name, text in {'api/src/A.cs': 'class A {}\n', 'web/src/a.ts': 'export const a = 1\n'}.items():
            self.write(name, text)
        self.git('init', '-q')
        self.git('config', 'user.name', 'test')
        self.git('config', 'user.email', 'test@example.com')
        self.git('config', 'core.autocrlf', 'false')
        self.git('add', '.')
        self.git('commit', '-q', '-m', 'start')

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def git(self, *args):
        subprocess.run([GIT, *args], cwd=self.root, check=True, capture_output=True)

    def write(self, relative, text):
        path = self.root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding='utf-8')

    def npm_ci(self, tsc=True, next=True):
        (self.root / 'web' / 'node_modules').mkdir(parents=True, exist_ok=True)
        for name, wanted in (('tsc', tsc), ('next', next)):
            if wanted:
                install(self.root / 'web' / 'node_modules' / '.bin', name, FAKE.format(name=name))

    def run_hook(self, stop_hook_active=False, dotnet_on_path=True, **outputs):
        """outputs: dotnet=, tsc=, next=(text, exit code). Returns the parsed answer, {} when silent."""
        env = {**os.environ, 'CLAUDE_PROJECT_DIR': str(self.root), 'FAKE_LOG': str(self.log)}
        for name, (text, code) in outputs.items():
            env[f'FAKE_{name.upper()}_OUTPUT'], env[f'FAKE_{name.upper()}_EXIT'] = text, str(code)
        if dotnet_on_path:
            env['PATH'] = str(self.fakes) + os.pathsep + os.environ['PATH']
        else:
            # Only git: the PATH of a runner holds a real dotnet next to git and python (/usr/bin).
            only_git = self.dir / 'only-git'
            install(only_git, 'git', target=GIT)
            env['PATH'] = os.pathsep.join([str(only_git)] + ([os.path.join(os.environ.get('SystemRoot', r'C:\Windows'),
                                                                            'System32')] if os.name == 'nt' else []))
        run = subprocess.run([PWSH, '-NoProfile', '-File', str(HOOK)],
                             input=json.dumps({'stop_hook_active': stop_hook_active}), capture_output=True,
                             text=True, encoding='utf-8', env=env, cwd=self.root, timeout=120)
        self.assertEqual(run.returncode, 0, run.stderr)
        return json.loads(run.stdout) if run.stdout.strip() else {}

    def calls(self, tool):
        if not self.log.exists():
            return []
        return [c for c in map(json.loads, self.log.read_text(encoding='utf-8').splitlines()) if c['tool'] == tool]

    # Backend: the behaviour before the web part.

    def test_nothing_changed_is_silent(self):
        self.npm_ci()
        self.assertEqual(self.run_hook(), {})
        self.assertEqual(self.log.exists(), False)

    def test_backend_passes_once(self):
        self.write('api/src/A.cs', 'class A { }\n')
        self.assertEqual(self.run_hook(), {})
        self.assertEqual([c['args'][:1] for c in self.calls('dotnet')], [['build'], ['test']])
        self.assertTrue((self.root / 'api' / 'obj' / 'stop-check.stamp').read_text().startswith('pass '))
        self.assertEqual(self.run_hook(), {})
        self.assertEqual(len(self.calls('dotnet')), 2)

    def test_backend_error_blocks(self):
        self.write('api/src/A.cs', 'class A {\n')
        answer = self.run_hook(dotnet=(CS_ERROR, 1))
        self.assertEqual(answer['decision'], 'block')
        self.assertIn('dotnet build or ZenGear.ArchitectureTests fails', answer['reason'])
        self.assertIn('error CS1002', answer['reason'])
        self.assertNotIn('systemMessage', answer)
        self.assertEqual(len(self.calls('dotnet')), 1)

    def test_backend_failure_is_reported_once(self):
        self.write('api/src/A.cs', 'class A {\n')
        self.run_hook(dotnet=(CS_ERROR, 1))
        answer = self.run_hook(stop_hook_active=True, dotnet=(CS_ERROR, 1))
        self.assertEqual(answer, {'systemMessage': 'stop-check: the build or the architecture tests still fail; see the errors above.'})
        self.assertEqual(self.run_hook(dotnet=(CS_ERROR, 1)), {})
        self.assertEqual(len(self.calls('dotnet')), 1)

    def test_backend_error_after_a_stop_hook_goes_to_the_user(self):
        self.write('api/src/A.cs', 'class A {\n')
        answer = self.run_hook(stop_hook_active=True, dotnet=(CS_ERROR, 1))
        self.assertNotIn('decision', answer)
        self.assertIn('stop-check: dotnet build or ZenGear.ArchitectureTests fails', answer['systemMessage'])

    def test_locked_output_is_only_reported(self):
        self.write('api/src/A.cs', 'class A { }\n')
        answer = self.run_hook(dotnet=(LOCKED, 1))
        self.assertEqual(answer, {'systemMessage': 'stop-check: not checked, the build output is locked (the API or Visual Studio is running).'})
        self.assertEqual(self.run_hook(dotnet=(LOCKED, 1)), {})

    def test_no_dotnet_is_reported(self):
        self.write('api/src/A.cs', 'class A { }\n')
        answer = self.run_hook(dotnet_on_path=False)
        self.assertEqual(answer, {'systemMessage': 'stop-check: not checked, dotnet is not on the PATH of pwsh.'})

    # Web.

    def test_web_before_npm_ci_is_not_checked(self):
        self.write('web/src/a.ts', 'export const a = 2\n')
        self.assertEqual(self.run_hook(), {})
        self.assertEqual(self.calls('tsc'), [])

    def test_web_passes_once(self):
        self.npm_ci()
        self.write('web/src/b.tsx', 'export const b = 1\n')
        self.assertEqual(self.run_hook(), {})
        [typegen] = self.calls('next')
        self.assertEqual(typegen['args'], ['typegen'])
        self.assertEqual(Path(typegen['cwd']).resolve(), self.root / 'web')
        [call] = self.calls('tsc')
        self.assertEqual(call['args'], ['--noEmit', '--pretty', 'false'])
        self.assertEqual(Path(call['cwd']).resolve(), self.root / 'web')
        self.assertEqual(self.calls('dotnet'), [])
        self.assertEqual(self.run_hook(), {})
        self.assertEqual(len(self.calls('tsc')), 1)

    def test_web_error_blocks(self):
        self.npm_ci()
        self.write('web/src/a.ts', "export const a: string = 1\n")
        answer = self.run_hook(tsc=('noise\n' + TS_ERROR + '\n', 2))
        self.assertEqual(answer['decision'], 'block')
        self.assertIn('tsc --noEmit fails in web/', answer['reason'])
        self.assertIn('error TS2322', answer['reason'])
        self.assertNotIn('noise', answer['reason'])

    def test_web_failure_is_reported_once(self):
        self.npm_ci()
        self.write('web/src/a.ts', "export const a: string = 1\n")
        self.run_hook(tsc=(TS_ERROR, 2))
        answer = self.run_hook(stop_hook_active=True, tsc=(TS_ERROR, 2))
        self.assertEqual(answer, {'systemMessage': 'stop-check: the typecheck of web/ still fails; see the errors above.'})
        self.assertEqual(len(self.calls('tsc')), 1)

    def test_typegen_runs_before_tsc(self):
        self.npm_ci()
        self.write('web/src/a.ts', 'export const a = 2\n')
        self.run_hook()
        calls = [json.loads(line)['tool'] for line in self.log.read_text(encoding='utf-8').splitlines()]
        self.assertEqual(calls, ['next', 'tsc'])

    def test_typegen_failure_blocks_without_tsc(self):
        self.npm_ci()
        self.write('web/src/a.ts', 'export const a = 2\n')
        answer = self.run_hook(next=('Error: Cannot find module ./src/env-schema\n', 1))
        self.assertEqual(answer['decision'], 'block')
        self.assertIn('next typegen fails in web/', answer['reason'])
        self.assertIn('Cannot find module', answer['reason'])
        self.assertEqual(self.calls('tsc'), [])

    def test_web_without_next_runs_tsc_alone(self):
        self.npm_ci(next=False)
        self.write('web/src/a.ts', 'export const a = 2\n')
        self.assertEqual(self.run_hook(), {})
        self.assertEqual(len(self.calls('tsc')), 1)

    def test_web_without_tsc_is_reported(self):
        self.npm_ci(tsc=False)
        self.write('web/src/a.ts', 'export const a = 2\n')
        self.assertIn('web not typechecked', self.run_hook()['systemMessage'])

    def test_files_the_tools_write_are_not_changes(self):
        self.npm_ci()
        for name in ('web/node_modules/x/index.js', 'web/next-env.d.ts', 'web/tsconfig.tsbuildinfo',
                     'web/src/x.tsbuildinfo', 'web/.next/types/routes.d.ts'):
            self.write(name, 'x\n')
        self.assertEqual(self.run_hook(), {})
        self.assertEqual(self.calls('tsc'), [])

    def test_files_near_the_excluded_ones_are_changes(self):
        self.npm_ci()
        for name in ('web/.nextrc', 'web/src/next-env.d.ts.bak'):
            with self.subTest(file=name):
                self.write(name, 'x\n')
                self.run_hook()
                self.assertTrue(self.calls('tsc'))
                self.log.unlink()

    def test_web_error_without_a_ts_line_shows_the_end(self):
        self.npm_ci()
        self.write('web/src/a.ts', 'export const a = 2\n')
        answer = self.run_hook(tsc=("error TS5083: Cannot read file '/r/web/tsconfig.json'.\n", 1))
        self.assertEqual(answer['decision'], 'block')
        self.assertIn('error TS5083', answer['reason'])

    # Both.

    def test_both_failures_in_one_block(self):
        self.npm_ci()
        self.write('api/src/A.cs', 'class A {\n')
        self.write('web/src/a.ts', "export const a: string = 1\n")
        answer = self.run_hook(dotnet=(CS_ERROR, 1), tsc=(TS_ERROR, 2))
        self.assertEqual(answer['decision'], 'block')
        self.assertIn('error CS1002', answer['reason'])
        self.assertIn('error TS2322', answer['reason'])

    def test_both_failures_after_a_stop_hook_go_to_the_user(self):
        self.npm_ci()
        self.write('api/src/A.cs', 'class A {\n')
        self.write('web/src/a.ts', "export const a: string = 1\n")
        answer = self.run_hook(stop_hook_active=True, dotnet=(CS_ERROR, 1), tsc=(TS_ERROR, 2))
        self.assertNotIn('decision', answer)
        self.assertIn('error CS1002', answer['systemMessage'])
        self.assertIn('error TS2322', answer['systemMessage'])

    def test_locked_backend_and_failing_web(self):
        self.npm_ci()
        self.write('api/src/A.cs', 'class A { }\n')
        self.write('web/src/a.ts', "export const a: string = 1\n")
        answer = self.run_hook(dotnet=(LOCKED, 1), tsc=(TS_ERROR, 2))
        self.assertEqual(answer['decision'], 'block')
        self.assertIn('error TS2322', answer['reason'])
        self.assertNotIn('MSB3027', answer['reason'])
        self.assertIn('the build output is locked', answer['systemMessage'])


if __name__ == '__main__':
    unittest.main()
