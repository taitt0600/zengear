"""Tests of tools/export_blueprint.py: IDs Excel can spoil are refused before anything is written, and the
exported Markdown of docs/blueprint/ is what the workbook gives (nobody edited it by hand).

Needs openpyxl (CI installs it in the tools workflow); skipped without it.
Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import datetime
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

try:
    import openpyxl
except ImportError:
    openpyxl = None

REPO = Path(__file__).resolve().parents[2]
SCRIPT = REPO / 'tools' / 'export_blueprint.py'
DECISION_HEAD = ['Decision ID', 'Topic', 'Context', 'Decision', 'Consequences', 'Source', 'Phase', 'Status']


def decision(identifier, status='Accepted'):
    return [identifier, 'Topic', 'Context', 'Decision', 'Consequences', 'Source', 'Phase 1', status]


@unittest.skipUnless(openpyxl, 'openpyxl is needed')
class ExportTests(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp())
        self.out = self.tmp / 'blueprint'

    def tearDown(self):
        shutil.rmtree(self.tmp)

    def export(self, decisions, features=(('AUT-04', 'Login'),)):
        wb = openpyxl.Workbook()
        ws = wb.active
        ws.title = 'Features'
        ws.append(['Feature ID', 'Feature'])
        for row in features:
            ws.append(list(row))
        ws = wb.create_sheet('Decisions')
        ws.append(DECISION_HEAD)
        for row in decisions:
            ws.append(row)
        workbook = self.tmp / 'workbook.xlsx'
        wb.save(workbook)
        return subprocess.run([sys.executable, str(SCRIPT), str(workbook), str(self.out)],
                              capture_output=True, text=True, encoding='utf-8')

    def test_writes_every_sheet_when_the_ids_are_right(self):
        run = self.export([decision('DEC-001'), decision('DEC-002', 'Superseded by DEC-001')])
        self.assertEqual(run.returncode, 0, run.stderr)
        self.assertIn('| DEC-002 |', (self.out / 'decisions.md').read_text(encoding='utf-8'))
        self.assertIn('- [Decisions](decisions.md) — 2 rows', (self.out / 'README.md').read_text(encoding='utf-8'))
        self.assertNotIn(b'\r\n', (self.out / 'decisions.md').read_bytes())

    def assert_refused(self, run, *fragments):
        self.assertEqual(run.returncode, 1)
        for fragment in fragments:
            self.assertIn(fragment, run.stderr)
        self.assertIn('nothing written', run.stderr)
        self.assertFalse(self.out.exists(), 'nothing may be written when an ID is wrong')

    def test_refuses_an_id_with_a_hidden_character(self):
        self.assert_refused(self.export([decision('DEC-001'), decision('﻿DEC-002')]),
                            'Decisions row 3', 'U+FEFF', "'DEC-099")

    def test_refuses_an_id_excel_turned_into_a_date(self):
        self.assert_refused(self.export([decision(datetime.datetime(1998, 12, 1))]), 'Decisions row 2', 'datetime')

    def test_refuses_a_repeated_id(self):
        self.assert_refused(self.export([decision('DEC-001'), decision('DEC-001')]), 'repeats row 2')

    def test_refuses_a_status_with_a_space_around_it(self):
        self.assert_refused(self.export([decision('DEC-001', 'Accepted ')]), "the Status 'Accepted '")

    def test_refuses_a_malformed_feature_id(self):
        self.assert_refused(self.export([decision('DEC-001')], features=(('AUT-4', 'Login'),)), 'Features row 2')


@unittest.skipUnless(openpyxl, 'openpyxl is needed')
class RealWorkbookTests(unittest.TestCase):
    def test_docs_blueprint_is_the_export_of_the_workbook(self):
        out = Path(tempfile.mkdtemp())
        try:
            run = subprocess.run([sys.executable, str(SCRIPT), str(REPO / 'docs' / 'product-domain-blueprint-MVP.xlsx'),
                                  str(out)], capture_output=True, text=True, encoding='utf-8')
            self.assertEqual(run.returncode, 0, run.stderr)
            committed = REPO / 'docs' / 'blueprint'
            self.assertEqual(sorted(p.name for p in out.iterdir()), sorted(p.name for p in committed.iterdir()))
            for exported in out.iterdir():
                with self.subTest(file=exported.name):
                    self.assertEqual(exported.read_text(encoding='utf-8').splitlines(),
                                     (committed / exported.name).read_text(encoding='utf-8').splitlines(),
                                     f'docs/blueprint/{exported.name} differs from the workbook: edit the workbook and export')
        finally:
            shutil.rmtree(out)


if __name__ == '__main__':
    unittest.main()
