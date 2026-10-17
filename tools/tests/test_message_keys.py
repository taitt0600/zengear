"""Tests of tools/message_keys.py on a throw-away folder: files with the same keys pass whatever their order and
values, a single file or no folder passes (Phase 1, before the web shell), and every kind of mismatch fails with a
message, never a traceback (DEC-104 (6), DEC-111 (7)).

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
SCRIPT = REPO / 'tools' / 'message_keys.py'


class MessageKeysTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        self.messages = self.dir / 'messages'

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def file(self, name, content):
        self.messages.mkdir(exist_ok=True)
        text = content if isinstance(content, str) else json.dumps(content, ensure_ascii=False)
        (self.messages / name).write_bytes(text.encode())

    def run_keys(self):
        run = subprocess.run([sys.executable, str(SCRIPT), str(self.messages)],
                             capture_output=True, text=True, encoding='utf-8')
        self.assertNotIn('Traceback', run.stdout + run.stderr)
        return run.returncode, run.stdout

    def assertPasses(self):
        code, out = self.run_keys()
        self.assertEqual(code, 0, out)

    def assertFails(self, *messages):
        code, out = self.run_keys()
        self.assertEqual(code, 1, out)
        for message in messages:
            self.assertIn(message, out)

    def test_no_folder_passes(self):
        self.assertPasses()

    def test_an_empty_folder_passes(self):
        self.messages.mkdir()
        self.assertPasses()

    def test_a_single_file_passes(self):
        self.file('vi.json', {'cart': {'title': 'Giỏ hàng'}})
        self.assertPasses()

    def test_the_same_keys_in_another_order_pass(self):
        self.file('vi.json', {'cart': {'title': 'Giỏ hàng', 'empty': 'Trống'}, 'ok': 'Đồng ý'})
        self.file('en.json', {'ok': 'OK', 'cart': {'empty': 'Empty', 'title': 'Cart'}})
        self.assertPasses()

    def test_a_missing_nested_key_fails_in_either_file(self):
        self.file('vi.json', {'cart': {'title': 'Giỏ hàng', 'empty': 'Trống'}})
        self.file('en.json', {'cart': {'title': 'Cart'}, 'ok': 'OK'})
        self.assertFails('en.json: missing cart.empty', 'vi.json: missing ok')

    def test_a_key_outside_ascii_is_printed(self):
        self.file('vi.json', {'giỏ': 'x'})
        self.file('en.json', {})
        self.assertFails('en.json: missing giỏ')

    def test_a_folder_named_like_a_file_cannot_be_read(self):
        self.file('vi.json', {'a': 'x'})
        (self.messages / 'en.json').mkdir()
        self.assertFails('en.json: cannot be read')

    def test_a_leaf_and_an_object_differ(self):
        self.file('vi.json', {'a': 'x'})
        self.file('en.json', {'a': {'b': 'x'}})
        self.assertFails('en.json: missing a', 'vi.json: missing a.b')

    def test_an_empty_object_gives_no_key(self):
        self.file('vi.json', {'a': 'x', 'b': {}})
        self.file('en.json', {'a': 'x'})
        self.assertPasses()

    def test_invalid_json_fails(self):
        self.file('vi.json', '{"a": ')
        self.assertFails('vi.json: not valid JSON')

    def test_a_top_level_array_fails(self):
        self.file('vi.json', ['a'])
        self.assertFails('vi.json: the top level is not a JSON object')

    def test_a_key_with_a_dot_fails(self):
        self.file('vi.json', {'cart': {'item.count': 'x'}})
        self.assertFails('vi.json: a key contains ".": cart.item.count')

    def test_a_long_list_is_cut(self):
        self.file('vi.json', {f'k{i:02}': 'x' for i in range(25)})
        self.file('en.json', {})
        self.assertFails('en.json: missing k00', 'and 5 more')
        self.assertNotIn('k24', self.run_keys()[1])

    def test_usage(self):
        for arguments in (['--help'], ['a', 'b']):
            with self.subTest(arguments=arguments):
                run = subprocess.run([sys.executable, str(SCRIPT), *arguments],
                                     capture_output=True, text=True, encoding='utf-8')
                self.assertEqual(run.returncode, 2)


if __name__ == '__main__':
    unittest.main()
