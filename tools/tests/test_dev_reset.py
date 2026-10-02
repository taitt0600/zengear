"""Tests of tools/dev_reset.ps1: it refuses anything that is not the local development environment, and -WhatIf
changes nothing and never prints a secret.

Every run uses a throw-away user-secrets store (HOME and APPDATA point to a temporary folder), so the developer's
own secrets are neither read nor changed, and a PATH without Docker, psql or python, so a step that ran by mistake
would fail loudly. Skipped when pwsh or dotnet is missing.
Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import json
import os
import re
import shutil
import subprocess
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / 'tools' / 'dev_reset.ps1'
CANARY = 'CANARY-5d1e2b'
LOCAL = {
    'ConnectionStrings:ZenGear': f'Host=localhost;Port=5432;Database=zengear;Username=app_login;Password={CANARY}',
    'ConnectionStrings:ZenGearMigrations': f'Host=127.0.0.1;Database=zengear;Username=owner;Password={CANARY}',
    'ConnectionStrings:Redis': 'localhost:6379,abortConnect=false',
    'Firebase:DefaultStaffPassword': CANARY,
}


def user_secrets_id():
    csproj = (REPO / 'api' / 'src' / 'ZenGear.Api' / 'ZenGear.Api.csproj').read_text(encoding='utf-8')
    return re.search(r'<UserSecretsId>([^<]+)</UserSecretsId>', csproj).group(1)


@unittest.skipUnless(shutil.which('pwsh') and shutil.which('dotnet'), 'pwsh and dotnet are needed')
class DevResetTests(unittest.TestCase):
    def run_script(self, secrets, *args, emulator=None):
        with tempfile.TemporaryDirectory() as tmp:
            home = Path(tmp)
            # Where dotnet user-secrets looks: ~/.microsoft/usersecrets (Linux, macOS), %APPDATA% (Windows).
            for folder in (home / '.microsoft' / 'usersecrets' / user_secrets_id(),
                           home / 'AppData' / 'Microsoft' / 'UserSecrets' / user_secrets_id()):
                folder.mkdir(parents=True)
                (folder / 'secrets.json').write_text(json.dumps(secrets), encoding='utf-8')
            tools = {str(Path(shutil.which(name)).resolve().parent) for name in ('pwsh', 'dotnet')}
            if os.name == 'nt':
                tools.add(os.path.join(os.environ.get('SystemRoot', r'C:\Windows'), 'System32'))
            env = {key: value for key, value in os.environ.items() if key.upper() != 'FIREBASE_AUTH_EMULATOR_HOST'}
            env.update({'HOME': str(home), 'USERPROFILE': str(home), 'APPDATA': str(home / 'AppData'),
                        'DOTNET_CLI_HOME': str(home), 'DOTNET_NOLOGO': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1',
                        'DOTNET_CLI_TELEMETRY_OPTOUT': '1', 'PATH': os.pathsep.join(sorted(tools))})
            if emulator:
                env['FIREBASE_AUTH_EMULATOR_HOST'] = emulator
            run = subprocess.run(['pwsh', '-NoProfile', '-File', str(SCRIPT), *args],
                                 capture_output=True, text=True, env=env, timeout=300)
        for stream in (run.stdout, run.stderr):
            self.assertNotIn(CANARY, stream)
        return run

    def assert_refused(self, run, words):
        self.assertEqual(run.returncode, 2, run.stdout + run.stderr)
        self.assertIn('REFUSED', run.stdout)
        self.assertIn(words, run.stdout)

    def test_refuses_a_remote_postgresql(self):
        secrets = {**LOCAL, 'ConnectionStrings:ZenGearMigrations': f'Host=db.example.com;Database=zengear;Password={CANARY}'}
        self.assert_refused(self.run_script(secrets, '-WhatIf'), 'ConnectionStrings:ZenGearMigrations')

    def test_refuses_another_postgresql_port(self):
        secrets = {**LOCAL, 'ConnectionStrings:ZenGear': f'Host=localhost;Port=55432;Database=zengear;Password={CANARY}'}
        self.assert_refused(self.run_script(secrets, '-WhatIf'), 'ConnectionStrings:ZenGear ')

    def test_refuses_another_database(self):
        secrets = {**LOCAL, 'ConnectionStrings:ZenGearMigrations': f'Host=localhost;Database=postgres;Password={CANARY}'}
        self.assert_refused(self.run_script(secrets, '-WhatIf'), 'another database')

    def test_refuses_a_remote_redis(self):
        secrets = {**LOCAL, 'ConnectionStrings:Redis': 'cache.example.com:6379'}
        self.assert_refused(self.run_script(secrets, '-WhatIf'), 'ConnectionStrings:Redis')

    def test_refuses_a_remote_emulator(self):
        run = self.run_script(LOCAL, '-WhatIf', emulator='auth.example.com:9099')
        self.assert_refused(run, 'FIREBASE_AUTH_EMULATOR_HOST')

    def test_refuses_without_whatif_too(self):
        secrets = {**LOCAL, 'ConnectionStrings:Redis': '10.0.0.5:6379'}
        self.assert_refused(self.run_script(secrets), 'ConnectionStrings:Redis')

    def test_a_missing_secret_is_a_missing_prerequisite(self):
        secrets = {key: value for key, value in LOCAL.items() if key != 'ConnectionStrings:ZenGearMigrations'}
        run = self.run_script(secrets, '-WhatIf')
        self.assertEqual(run.returncode, 1, run.stdout + run.stderr)
        self.assertIn('MISSING: user-secret ConnectionStrings:ZenGearMigrations', run.stdout)

    def test_whatif_lists_every_step_and_changes_nothing(self):
        run = self.run_script(LOCAL, '-WhatIf', emulator='127.0.0.1:9099')
        self.assertEqual(run.returncode, 0, run.stdout + run.stderr)
        for step in ('zengear_01_database.sql', 'dotnet ef database update', 'zengear_08_roles.sql', '01_roles.sql',
                     'delete every account', 'bootstrap-admin', 'FLUSHALL'):
            self.assertIn(step, run.stdout)
        # Docker, psql and python are not on PATH: a step that ran instead of being described would have failed.
        self.assertNotIn('MISSING', run.stdout)
        self.assertNotIn('applied', run.stdout)


if __name__ == '__main__':
    unittest.main()
