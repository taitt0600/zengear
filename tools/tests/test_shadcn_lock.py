"""Tests of tools/shadcn_lock.py on a throw-away folder: a lock written after shadcn add passes, a checkout with CRLF
line endings hashes the same, and check fails on each kind of mismatch (DEC-111 (4)) with a message, never a
traceback.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import json
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / 'tools' / 'shadcn_lock.py'


class ShadcnLockTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        self.ui = self.dir / 'ui'

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def files(self, **contents):
        for name, text in contents.items():
            path = self.ui / name.replace('__', '/')
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(text.encode())

    def run_lock(self, command):
        run = subprocess.run([sys.executable, str(SCRIPT), command, str(self.ui)],
                             capture_output=True, text=True, encoding='utf-8')
        self.assertNotIn('Traceback', run.stdout + run.stderr)
        return run.returncode, run.stdout

    def assertFails(self, message):
        code, out = self.run_lock('check')
        self.assertEqual(code, 1, out)
        self.assertIn(message, out)

    def test_write_then_check_passes(self):
        self.files(**{'button.tsx': 'a\n', 'sub__card.tsx': 'b\n'})
        self.assertEqual(self.run_lock('write')[0], 0)
        entries = json.loads((self.ui / '.shadcn-lock.json').read_text(encoding='utf-8'))
        self.assertEqual(sorted(entries), ['button.tsx', 'sub/card.tsx'])
        code, out = self.run_lock('check')
        self.assertEqual(code, 0, out)

    def test_crlf_hashes_as_lf(self):
        self.files(**{'button.tsx': 'a\nb\n'})
        self.run_lock('write')
        self.files(**{'button.tsx': 'a\r\nb\r\n'})
        self.assertEqual(self.run_lock('check')[0], 0)

    def test_no_folder_and_no_lock_passes(self):
        self.assertEqual(self.run_lock('check')[0], 0)

    def test_files_without_a_lock(self):
        self.files(**{'button.tsx': 'a\n'})
        self.assertFails('has files but no .shadcn-lock.json')

    def test_a_lock_without_files(self):
        self.files(**{'.shadcn-lock.json': '{}\n'})
        self.assertFails('has no component file')

    def test_a_file_without_an_entry(self):
        self.files(**{'button.tsx': 'a\n'})
        self.run_lock('write')
        self.files(**{'card.tsx': 'b\n'})
        self.assertFails('card.tsx: no entry')

    def test_an_entry_without_a_file(self):
        self.files(**{'button.tsx': 'a\n', 'card.tsx': 'b\n'})
        self.run_lock('write')
        (self.ui / 'card.tsx').unlink()
        self.assertFails('card.tsx: in .shadcn-lock.json but no such file')

    def test_a_changed_file(self):
        self.files(**{'button.tsx': 'a\n'})
        self.run_lock('write')
        self.files(**{'button.tsx': 'changed by hand\n'})
        self.assertFails('button.tsx: differs from its hash')

    def test_an_unreadable_lock(self):
        for text in ('{not json', '[]', '{"button.tsx": 1}', '﻿{}'):
            with self.subTest(lock=text):
                self.files(**{'button.tsx': 'a\n', '.shadcn-lock.json': text})
                self.assertFails('.shadcn-lock.json')

    def test_write_without_files_fails(self):
        code, out = self.run_lock('write')
        self.assertEqual(code, 1)
        self.assertIn('no component file to lock', out)


if __name__ == '__main__':
    unittest.main()
