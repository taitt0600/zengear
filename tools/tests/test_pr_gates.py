"""Tests of tools/pr_gates.py on a throw-away git repository: a commit that changes the specification passes with
an accepted decision, and otherwise the error says which decision the gates could not read.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = Path(os.environ.get('PR_GATES_SCRIPT', REPO / 'tools' / 'pr_gates.py'))
HEADER = ('| Decision ID | Topic | Context | Decision | Consequences | Source | Phase | Status |\n'
          '|---|---|---|---|---|---|---|---|\n')


def row(identifier, status='Accepted'):
    return f'| {identifier} | Topic | Context | Decision | Consequences | Source | Phase 1 | {status} |\n'


@unittest.skipUnless(shutil.which('git'), 'git is needed')
class SpecificationDecisionTests(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        for args in (['init', '-q', '-b', 'main'], ['config', 'user.name', 'taitt0600'],
                     ['config', 'user.email', 'taitt.dev@gmail.com'], ['config', 'commit.gpgsign', 'false'],
                     ['config', 'core.hooksPath', '/dev/null']):
            self.git(*args)
        self.base = self.commit(HEADER + row('DEC-001'), 'docs: record DEC-001 for a start\n\nRefs: DEC-001\n')

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def git(self, *args):
        return subprocess.run(['git', *args], cwd=self.dir, capture_output=True, text=True, check=True).stdout.strip()

    def commit(self, decisions, message):
        path = self.dir / 'docs' / 'blueprint' / 'decisions.md'
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(decisions, encoding='utf-8')
        self.git('add', '-A')
        self.git('commit', '-q', '-m', message)
        return self.git('rev-parse', 'HEAD')

    def check(self, decisions, message):
        head = self.commit(decisions, message)
        run = subprocess.run([sys.executable, str(SCRIPT), 'check', self.base, head, message.splitlines()[0]],
                             cwd=self.dir, capture_output=True, text=True, encoding='utf-8')
        return run.returncode, run.stdout

    def test_passes_with_an_accepted_decision(self):
        code, out = self.check(HEADER + row('DEC-001') + row('DEC-002'), 'docs: record DEC-002 for x\n\nRefs: DEC-002\n')
        self.assertEqual(code, 0, out)
        self.assertIn('PR gates passed', out)

    def test_names_a_decision_whose_id_cell_has_a_hidden_character(self):
        code, out = self.check(HEADER + row('DEC-001') + row('DEC-002​'), 'docs: record DEC-002 for x\n\nRefs: DEC-002\n')
        self.assertEqual(code, 1)
        self.assertIn('naming DEC-002', out)
        self.assertIn('hidden character', out)

    def test_names_a_decision_that_is_not_accepted(self):
        code, out = self.check(HEADER + row('DEC-001') + row('DEC-002', 'Proposed'), 'docs: record DEC-002 for x\n\nRefs: DEC-002\n')
        self.assertEqual(code, 1)
        self.assertIn('naming DEC-002', out)

    def test_says_when_the_message_names_no_decision(self):
        code, out = self.check(HEADER + row('DEC-001') + row('DEC-002'), 'docs: change the decisions\n')
        self.assertEqual(code, 1)
        self.assertIn('without an accepted decision (DEC-xxx) in its message', out)


if __name__ == '__main__':
    unittest.main()
