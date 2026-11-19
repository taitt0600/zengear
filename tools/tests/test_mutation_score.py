"""Tests of tools/mutation_score.py (DEC-152 (7)): the score counts only the mutants the unit tests cover, a score
below the break fails, NoCoverage is listed per file and never fails, and an unreadable report is an error.
Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import io
import json
import os
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import mutation_score  # noqa: E402


def report(*files):
    """A report of the mutation-testing-elements schema: files given as (path, [statuses])."""
    return {'schemaVersion': '2', 'projectRoot': '/repo/api/src/ZenGear.Services', 'thresholds': {},
            'files': {path: {'language': 'cs', 'source': '', 'mutants': [
                {'id': str(i), 'mutatorName': 'x', 'status': status,
                 'location': {'start': {'line': i + 1, 'column': 1}, 'end': {'line': i + 1, 'column': 2}}}
                for i, status in enumerate(statuses)]} for path, statuses in files}}


class MutationScoreTests(unittest.TestCase):
    def run_score(self, data, threshold, raw=None):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / 'mutation-report.json'
            path.write_text(raw if raw is not None else json.dumps(data), encoding='utf-8')
            summary = Path(folder) / 'summary.md'
            out = io.StringIO()
            with mock.patch.dict(os.environ, {'GITHUB_STEP_SUMMARY': str(summary)}), redirect_stdout(out):
                code = mutation_score.main([str(path), '--break', str(threshold)])
            written = summary.read_text(encoding='utf-8') if summary.exists() else ''
        return code, out.getvalue(), written

    def test_only_the_covered_mutants_count(self):
        # 3 detected of 4 covered = 75 %; the 10 NoCoverage mutants would make it 21 % in Stryker's own score.
        data = report(('/repo/api/src/ZenGear.Services/A.cs', ['Killed', 'Killed', 'Timeout', 'Survived'] +
                       ['NoCoverage'] * 10 + ['CompileError', 'Ignored']))
        code, out, written = self.run_score(data, 75)
        self.assertEqual(code, 0, out)
        self.assertIn('Covered score 75.00 % passes the break of 75.0 %', out)
        self.assertIn('no coverage 10', out)
        self.assertIn('Covered score 75.00 %', written)

    def test_below_the_break_fails(self):
        data = report(('/repo/api/src/ZenGear.Services/A.cs', ['Killed', 'Survived', 'Survived']))
        code, out, _ = self.run_score(data, 50)
        self.assertEqual(code, 1)
        self.assertIn('::error::the covered mutation score 33.33 % is below the break of 50.0 %', out)

    def test_no_covered_mutant_passes_and_lists_the_files(self):
        data = report(('/repo/api/src/ZenGear.Services/Identity/B.cs', ['NoCoverage', 'NoCoverage']),
                      ('/repo/api/src/ZenGear.Services/Identity/A.cs', ['NoCoverage', 'Ignored']))
        code, out, written = self.run_score(data, 23)
        self.assertEqual(code, 0, out)
        self.assertIn('nothing to judge', out)
        self.assertLess(out.index('Identity/A.cs'), out.index('Identity/B.cs'))
        self.assertIn('Identity/B.cs`: 2', written.replace('\\', '/'))

    def test_an_empty_report_passes(self):
        code, out, _ = self.run_score(report(), 23)
        self.assertEqual(code, 0, out)

    def test_an_unreadable_report_is_an_error(self):
        for raw in ('not json', json.dumps({'schemaVersion': '2'})):
            with self.subTest(raw=raw):
                code, out, _ = self.run_score(None, 23, raw=raw)
                self.assertEqual(code, 2)
                self.assertIn('::error::the mutation report', out)


if __name__ == '__main__':
    unittest.main()
