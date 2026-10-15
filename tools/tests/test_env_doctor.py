"""Tests of tools/env_doctor.ps1: its output shape, and that it never prints the value of a user-secret.

The script runs against a throw-away user-secrets store (HOME and APPDATA point to a temporary folder),
so the developer's own secrets are neither read nor changed. Skipped when pwsh or dotnet is missing.
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
SCRIPT = REPO / 'tools' / 'env_doctor.ps1'
CANARY = 'CANARY-7f3a9c'
SECRETS = {
    'ConnectionStrings:ZenGear': f'Host=canary-host;Username=zengear_app_login;Password={CANARY}',
    'ConnectionStrings:ZenGearMigrations': f'Host=canary-host;Username=owner;Password={CANARY}-owner',
}


def user_secrets_id():
    csproj = (REPO / 'api' / 'src' / 'ZenGear.Api' / 'ZenGear.Api.csproj').read_text(encoding='utf-8')
    return re.search(r'<UserSecretsId>([^<]+)</UserSecretsId>', csproj).group(1)


def run_doctor(secrets, *args):
    """env_doctor.ps1 with a throw-away user-secrets store holding <secrets>."""
    with tempfile.TemporaryDirectory() as tmp:
        home = Path(tmp)
        secret_id = user_secrets_id()
        # Where dotnet user-secrets looks: ~/.microsoft/usersecrets (Linux, macOS), %APPDATA% (Windows).
        for folder in (home / '.microsoft' / 'usersecrets' / secret_id,
                       home / 'AppData' / 'Microsoft' / 'UserSecrets' / secret_id):
            folder.mkdir(parents=True)
            (folder / 'secrets.json').write_text(json.dumps(secrets), encoding='utf-8')
        env = {**os.environ, 'HOME': str(home), 'USERPROFILE': str(home), 'APPDATA': str(home / 'AppData'),
               'DOTNET_CLI_HOME': str(home), 'DOTNET_NOLOGO': '1', 'DOTNET_SKIP_FIRST_TIME_EXPERIENCE': '1',
               'DOTNET_CLI_TELEMETRY_OPTOUT': '1'}
        return subprocess.run(['pwsh', '-NoProfile', '-File', str(SCRIPT), *args],
                              capture_output=True, text=True, env=env, timeout=300)


@unittest.skipUnless(shutil.which('pwsh') and shutil.which('dotnet'), 'pwsh and dotnet are needed')
class EnvDoctorTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.json_run = run_doctor(SECRETS, '-Json')
        cls.text_run = run_doctor(SECRETS)
        cls.rows = {row['Check']: row for row in json.loads(cls.json_run.stdout)}

    def test_never_prints_a_secret_value(self):
        for run in (self.json_run, self.text_run):
            for stream in (run.stdout, run.stderr):
                self.assertNotIn(CANARY, stream)
                self.assertNotIn('canary-host', stream)

    def test_reports_the_secrets_by_name(self):
        self.assertEqual(self.rows['Secret ConnectionStrings:ZenGear is set']['Result'], 'PASS')
        self.assertEqual(self.rows['Secret ConnectionStrings:ZenGearMigrations is set']['Result'], 'PASS')
        # Missing Redis only slows requests down (R-SEC-07): a warning, not a failure.
        self.assertEqual(self.rows['Secret ConnectionStrings:Redis is set']['Result'], 'WARN')

    def test_every_row_has_a_known_result_and_a_hint_when_not_passed(self):
        self.assertIn('Docker answers', self.rows)
        for row in self.rows.values():
            self.assertIn(row['Result'], {'PASS', 'WARN', 'FAIL', 'SKIP'})
            self.assertEqual(bool(row['Hint']), row['Result'] != 'PASS', row)

    def test_exit_code_follows_the_failures(self):
        failed = any(row['Result'] == 'FAIL' for row in self.rows.values())
        self.assertEqual(self.json_run.returncode, 1 if failed else 0)
        self.assertEqual(self.text_run.returncode, self.json_run.returncode)

    def test_no_redis_host_row_without_the_secret(self):
        self.assertNotIn('Secret ConnectionStrings:Redis names 127.0.0.1', self.rows)


@unittest.skipUnless(shutil.which('pwsh') and shutil.which('dotnet'), 'pwsh and dotnet are needed')
class EnvDoctorRedisHostTests(unittest.TestCase):
    """localhost resolves to ::1 first, where api/docker-compose.yml does not publish Redis: the cache stays off (#69)."""

    ROW = 'Secret ConnectionStrings:Redis names 127.0.0.1'

    def rows(self, redis):
        """The rows of a -Json run; neither run prints the Redis value, whose password is a canary."""
        secrets = {**SECRETS, 'ConnectionStrings:Redis': redis}
        json_run = run_doctor(secrets, '-Json')
        text_run = run_doctor(secrets)
        for run in (json_run, text_run):
            self.assertNotIn(f'{CANARY}-redis', run.stdout + run.stderr)
        return {row['Check']: row for row in json.loads(json_run.stdout)}

    def test_localhost_is_a_warning_with_the_command_that_fixes_it(self):
        row = self.rows(f'localhost:6379,password={CANARY}-redis')[self.ROW]
        self.assertEqual(row['Result'], 'WARN')
        self.assertIn('dotnet user-secrets set "ConnectionStrings:Redis" "127.0.0.1:6379" --project api/src/ZenGear.Api (',
                      row['Hint'])

    def test_ipv4_loopback_passes_with_options_first(self):
        self.assertEqual(self.rows(f'abortConnect=false,127.0.0.1:6379,password={CANARY}-redis')[self.ROW]['Result'], 'PASS')


if __name__ == '__main__':
    unittest.main()
