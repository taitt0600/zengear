"""Tests of tools/screen_docs.py: a small fake docs/screens/ folder, then the real one.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import contextlib
import io
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))

import screen_docs  # noqa: E402  (imported after the path is set)

HEADER = '| ID | Screen | Route | Feature | Permission | Blocked by |\n|---|---|---|---|---|---|\n'
ROWS = ('| [SCR-AAA-01](SCR-AAA-01.md) | First | `/a` | AAA-01 | — | |\n'
        '| [SCR-BBB-01](SCR-BBB-01.md) | Second | `/b` | AAA-01, BBB-02 | `b.read` | gap 2 |\n')
GAPS = ('# Known gaps\n\nIntro.\n\n'
        '1. **One** (DEC-001 (2)) — closed. Text that names gap 2.\n'
        '2. **Two** — closed for SCR-AAA-01: still open\n   for SCR-BBB-01.\n'
        '3. **Three.** Open.\n')


class FakeFolderTests(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        self.folder = Path(tmp.name)
        self.write('README.md', '# Screens\n\nIntro.\n\n## Screen list\n\n### Admin\n\n' + HEADER + ROWS
                   + '\nThe paragraph under the list names gaps 1\nand 3.\n')
        self.write('gaps.md', GAPS)
        self.write('conventions.md', '# Conventions for every screen\n\n- A gap of 32px is no gap.\n')
        self.write('SCR-AAA-01.md', '# SCR-AAA-01 First\n\nWaits for gaps 1 to 3.\n\n## Layout\n\n- Design.\n')
        self.write('SCR-BBB-01.md', '# SCR-BBB-01 Second\n\nBody.\n')

    def write(self, name, text):
        (self.folder / name).write_text(text, encoding='utf-8')

    def edit(self, name, old, new):
        path = self.folder / name
        text = path.read_text(encoding='utf-8')
        self.assertIn(old, text)
        path.write_text(text.replace(old, new), encoding='utf-8')

    def errors(self):
        return '\n'.join(screen_docs.check(self.folder))

    def test_a_consistent_folder_passes(self):
        self.assertEqual(screen_docs.check(self.folder), [])

    def test_reads_every_table_of_the_screen_list_with_its_part(self):
        self.edit('README.md', '\nThe paragraph', '\n### POS\n\n' + HEADER
                  + '| [SCR-POS-01](SCR-POS-01.md) | Sale | `/pos` | POS-01 | — | |\n\nThe paragraph')
        self.write('SCR-POS-01.md', '# SCR-POS-01 Sale\n')
        rows = screen_docs.screen_list(self.folder)
        self.assertEqual([(screen_docs.row_id(row), row['Part']) for row in rows],
                         [('SCR-AAA-01', 'Admin'), ('SCR-BBB-01', 'Admin'), ('SCR-POS-01', 'POS')])
        self.assertEqual(screen_docs.check(self.folder), [])

    def test_features_of_a_row(self):
        rows = screen_docs.screen_list(self.folder)
        self.assertEqual([screen_docs.features(row) for row in rows], [['AAA-01'], ['AAA-01', 'BBB-02']])

    def test_gap_items_keep_their_continuation_lines(self):
        items = screen_docs.gap_items(self.folder)
        self.assertEqual(sorted(items), [1, 2, 3])
        self.assertEqual(items[2], '2. **Two** — closed for SCR-AAA-01: still open\n   for SCR-BBB-01.')

    def test_gap_refs_of_every_form(self):
        self.assertEqual(screen_docs.gap_refs('Gap 3, then gaps 2, 5, and 7, gaps 9 to 11 and gap\n4; a gap 12px'),
                         {2, 3, 4, 5, 7, 9, 10, 11})

    def test_a_row_with_too_few_cells(self):
        self.edit('README.md', '| `/b` | AAA-01, BBB-02 | `b.read` | gap 2 |', '| `/b` |')
        self.assertIn('has 3 cells, expected 6', self.errors())

    def test_an_id_that_is_not_a_link_to_its_file(self):
        for cell in ('SCR-AAA-01', '[SCR-AAA-01](SCR-BBB-01.md)', '[SCR-AAA-1](SCR-AAA-1.md)'):
            with self.subTest(cell=cell):
                self.setUp()
                self.edit('README.md', '[SCR-AAA-01](SCR-AAA-01.md)', cell)
                self.assertIn('is not a link', self.errors())

    def test_a_row_without_its_file(self):
        (self.folder / 'SCR-BBB-01.md').unlink()
        self.assertIn('SCR-BBB-01 has no file SCR-BBB-01.md', self.errors())

    def test_a_file_without_its_row(self):
        self.write('SCR-CCC-01.md', '# SCR-CCC-01 Third\n')
        self.assertIn('SCR-CCC-01.md has no row', self.errors())

    def test_two_rows_of_one_screen(self):
        self.edit('README.md', ROWS, ROWS + ROWS.splitlines(keepends=True)[0])
        self.assertIn('SCR-AAA-01 has two rows', self.errors())

    def test_a_first_line_that_differs_from_the_row(self):
        for first in ('# SCR-AAA-01 Other name', '## SCR-AAA-01 First', 'SCR-AAA-01 First'):
            with self.subTest(first=first):
                self.write('SCR-AAA-01.md', first + '\n\nBody.\n')
                self.assertIn("expected '# SCR-AAA-01 First'", self.errors())

    def test_a_feature_cell_that_is_not_feature_ids(self):
        for cell in ('', 'AAA-1', 'AAA-01 and BBB-02'):
            with self.subTest(cell=cell):
                self.setUp()
                self.edit('README.md', '| `/a` | AAA-01 |', f'| `/a` | {cell} |')
                self.assertIn('Feature cell', self.errors())

    def test_gaps_numbered_with_a_hole_or_twice(self):
        self.edit('gaps.md', '3. **Three.**', '4. **Three.**')
        self.assertIn('expected [1, 2, 3]', self.errors())
        self.edit('gaps.md', '4. **Three.**', '2. **Three.**')
        self.assertIn('gap 2 is numbered twice', self.errors())

    def test_a_reference_to_a_gap_that_does_not_exist(self):
        for name, old, new in (('SCR-BBB-01.md', 'Body.', 'Waits for gap 4.'),
                               ('SCR-AAA-01.md', 'gaps 1 to 3', 'gaps 1 to 4'),
                               ('README.md', 'gaps 1\nand 3', 'gaps 1\nand 4'),
                               ('gaps.md', 'names gap 2', 'names gaps 2, 3 and 4')):
            with self.subTest(name=name, new=new):
                self.setUp()
                self.edit(name, old, new)
                self.assertIn(f'{name} names gap 4, which gaps.md does not have', self.errors())

    def test_blocked_by_a_closed_gap(self):
        self.edit('README.md', '| gap 2 |', '| gap 1 |')
        self.assertIn('SCR-BBB-01 is blocked by gap 1, which gaps.md says is closed', self.errors())

    def test_a_gap_closed_for_one_screen_only_is_open(self):
        self.assertEqual(screen_docs.gaps(self.folder)[0], {1: True, 2: False, 3: False})

    def test_a_table_outside_a_part_heading(self):
        self.edit('README.md', '### Admin\n\n', '')
        self.assertIn('outside a "### <part>" heading', self.errors())

    def test_columns_other_than_the_expected_ones(self):
        self.edit('README.md', '| Route | Feature |', '| Route | Part | Feature |')
        self.assertIn('the columns are', self.errors())

    def test_no_screen_list(self):
        self.edit('README.md', '## Screen list', '## Screens')
        self.assertIn('no heading "## Screen list"', self.errors())

    def test_tables_after_the_screen_list_are_not_rows(self):
        self.edit('README.md', '\nThe paragraph', '\n## Other\n\n| A | B |\n|---|---|\n| x | y |\n\nThe paragraph')
        self.assertEqual(len(screen_docs.screen_list(self.folder)), 2)

    def test_main_exit_codes(self):
        with contextlib.redirect_stdout(io.StringIO()) as out:
            self.assertEqual(screen_docs.main([str(self.folder)]), 0)
        self.assertIn('agrees (2 screens)', out.getvalue())
        (self.folder / 'SCR-BBB-01.md').unlink()
        with contextlib.redirect_stderr(io.StringIO()) as err:
            self.assertEqual(screen_docs.main([str(self.folder)]), 1)
        self.assertIn('screen_docs: README.md: SCR-BBB-01 has no file', err.getvalue())
        with contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(screen_docs.main(['a', 'b']), 2)
            self.assertEqual(screen_docs.main(['--help']), 2)
            self.assertEqual(screen_docs.main([str(self.folder / 'missing')]), 1)


class RealFolderTests(unittest.TestCase):
    """docs/screens/ of this repository agrees with itself."""

    def test_the_real_folder_agrees(self):
        self.assertEqual(screen_docs.check(REPO / 'docs' / 'screens'), [])


if __name__ == '__main__':
    unittest.main()
