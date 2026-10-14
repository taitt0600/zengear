"""Tests of tools/pr_gates.py on a throw-away git repository: a commit that changes the specification passes with
an accepted decision, and otherwise the error says which decision the gates could not read; a feat pull request
fails while docs/STATUS.md names its feature (gate 6), and the reader of those rows matches whole cells only.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import importlib.util
import os
import shutil
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SCRIPT = Path(os.environ.get('PR_GATES_SCRIPT', REPO / 'tools' / 'pr_gates.py'))
DECISIONS = 'docs/blueprint/decisions.md'
HEADER = ('| Decision ID | Topic | Context | Decision | Consequences | Source | Phase | Status |\n'
          '|---|---|---|---|---|---|---|---|\n')
# The real column order: feature_rules reads Rules as the sixth cell.
FEATURES = ('| Feature ID | Epic | Feature | User Story | Acceptance Criteria | Rules | Entities | Depends On | Priority '
            '| Phase |\n|---|---|---|---|---|---|---|---|---|---|\n'
            '| POS-01 | POS | Sell at counter | Story | Criteria | — | — | — | P1 | Phase 1 |\n')
NEXT = '## Next\n\n| Key | Item | Who | Source |\n|---|---|---|---|\n'
WAITING = '## Waiting for a trigger\n\n| Trigger | Item | Source |\n|---|---|---|\n'


def row(identifier, status='Accepted'):
    return f'| {identifier} | Topic | Context | Decision | Consequences | Source | Phase 1 | {status} |\n'


def status(*tables):
    return '# Status\n\n' + '\n'.join(tables)


def load_gates():
    spec = importlib.util.spec_from_file_location('pr_gates_under_test', SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@unittest.skipUnless(shutil.which('git'), 'git is needed')
class GitRepositoryTestCase(unittest.TestCase):
    def setUp(self):
        self.dir = Path(tempfile.mkdtemp())
        for args in (['init', '-q', '-b', 'main'], ['config', 'user.name', 'taitt0600'],
                     ['config', 'user.email', 'taitt.dev@gmail.com'], ['config', 'commit.gpgsign', 'false'],
                     ['config', 'core.hooksPath', '/dev/null']):
            self.git(*args)

    def tearDown(self):
        shutil.rmtree(self.dir, ignore_errors=True)

    def git(self, *args):
        return subprocess.run(['git', *args], cwd=self.dir, capture_output=True, text=True, check=True).stdout.strip()

    def commit(self, files, message):
        """Write each file (None deletes it) and commit them all."""
        for name, text in files.items():
            path = self.dir / name
            if text is None:
                path.unlink()
                continue
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding='utf-8')
        self.git('add', '-A')
        self.git('commit', '-q', '-m', message)
        return self.git('rev-parse', 'HEAD')

    def run_check(self, base, head, title):
        run = subprocess.run([sys.executable, str(SCRIPT), 'check', base, head, title],
                             cwd=self.dir, capture_output=True, text=True, encoding='utf-8')
        return run.returncode, run.stdout


class SpecificationDecisionTests(GitRepositoryTestCase):
    def setUp(self):
        super().setUp()
        self.base = self.commit({DECISIONS: HEADER + row('DEC-001')}, 'docs: record DEC-001 for a start\n\nRefs: DEC-001\n')

    def check(self, decisions, message):
        head = self.commit({DECISIONS: decisions}, message)
        return self.run_check(self.base, head, message.splitlines()[0])

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


class WorkStateReaderTests(unittest.TestCase):
    """The reader of gate 6; test_feature_context.py checks the same cases on the dossier (DEC-110 (5))."""

    @classmethod
    def setUpClass(cls):
        cls.rows = staticmethod(load_gates().work_state_rows)

    def test_a_next_row_keyed_by_the_feature(self):
        self.assertEqual(self.rows(status(NEXT + '| POS-01 | Sell | local | x |\n'), 'POS-01'),
                         ['| POS-01 | Sell | local | x |'])

    def test_a_waiting_row_triggered_before_the_feature(self):
        text = status(NEXT + '| web | Other | local | x |\n', WAITING + '| before POS-01 | Decide | DEC-1 |\n')
        self.assertEqual(self.rows(text, 'POS-01'), ['| before POS-01 | Decide | DEC-1 |'])

    def test_a_key_in_backticks_matches(self):
        self.assertEqual(self.rows(status(NEXT + '| `POS-01` | Sell | local | x |\n'), 'POS-01'),
                         ['| `POS-01` | Sell | local | x |'])

    def test_an_id_only_in_the_item_does_not_match(self):
        text = status(NEXT + '| web-shell | After POS-01 | local | x |\n', WAITING + '| storefront deploy | POS-01 | x |\n')
        self.assertEqual(self.rows(text, 'POS-01'), [])

    def test_pos_01_does_not_match_pos_010(self):
        text = status(NEXT + '| POS-010 | Other | local | x |\n', WAITING + '| before POS-010 | Other | x |\n')
        self.assertEqual(self.rows(text, 'POS-01'), [])

    def test_no_file_gives_no_row(self):
        self.assertEqual(self.rows('', 'POS-01'), [])

    def test_a_file_with_only_next(self):
        self.assertEqual(self.rows(status(NEXT + '| POS-01 | Sell | local | x |\n'), 'POS-01'),
                         ['| POS-01 | Sell | local | x |'])

    def test_headings_without_a_table_give_no_row(self):
        self.assertEqual(self.rows(status('## Next\n\nNothing yet.\n', '## Waiting for a trigger\n\nNone.\n'), 'POS-01'), [])

    def test_next_without_a_table_then_a_waiting_row(self):
        text = status('## Next\n\nNothing yet.\n', WAITING + '| before POS-01 | Decide | DEC-1 |\n')
        self.assertEqual(self.rows(text, 'POS-01'), ['| before POS-01 | Decide | DEC-1 |'])

    def test_an_escaped_pipe_stays_inside_its_row(self):
        self.assertEqual(self.rows(status(NEXT + r'| POS-01 | a \| b | local | x |' + '\n'), 'POS-01'),
                         [r'| POS-01 | a \| b | local | x |'])


class WorkStateGateTests(GitRepositoryTestCase):
    """Gate 6 alone: the head commit changes only docs/STATUS.md, and check runs without the app (gate 5)."""

    MESSAGE = 'docs/STATUS.md at the head still has a row that names POS-01'

    def setUp(self):
        super().setUp()
        self.base_files = {'docs/blueprint/features.md': FEATURES, DECISIONS: HEADER + row('DEC-001')}

    def check(self, base_status, head_status, title='feat(api): add selling at the counter (POS-01)'):
        files = dict(self.base_files)
        if base_status is not None:
            files['docs/STATUS.md'] = base_status
        base = self.commit(files, 'docs: start\n\nRefs: DEC-001\n')
        head = self.commit({'docs/STATUS.md': head_status}, 'docs: change the work state\n')
        return self.run_check(base, head, title)

    def assertPasses(self, result):
        code, out = result
        self.assertEqual(code, 0, out)
        self.assertIn('PR gates passed', out)

    def assertFailsOn(self, result, line):
        code, out = result
        self.assertEqual(code, 1, out)
        self.assertIn(f'{self.MESSAGE}: {line}; the pull request that does the work deletes it', out)

    def test_passes_without_a_row(self):
        self.assertPasses(self.check(None, status(NEXT + '| web | Other | local | x |\n')))

    def test_fails_on_a_next_row_keyed_by_the_feature(self):
        self.assertFailsOn(self.check(None, status(NEXT + '| POS-01 | Sell | local | x |\n')),
                           '| POS-01 | Sell | local | x |')

    def test_fails_on_a_waiting_row_triggered_before_the_feature(self):
        self.assertFailsOn(self.check(None, status(NEXT, WAITING + '| before POS-01 | Decide | DEC-1 |\n')),
                           '| before POS-01 | Decide | DEC-1 |')

    def test_passes_when_the_id_is_only_in_another_item(self):
        self.assertPasses(self.check(None, status(NEXT + '| web-shell | After POS-01 | local | x |\n')))

    def test_pos_01_does_not_match_pos_010(self):
        self.assertPasses(self.check(None, status(NEXT + '| POS-010 | Other | local | x |\n',
                                                  WAITING + '| before POS-010 | Other | x |\n')))

    def test_a_fix_pull_request_is_not_checked(self):
        self.assertPasses(self.check(None, status(NEXT + '| POS-01 | Sell | local | x |\n'),
                                     title='fix(api): keep the counter total (POS-01)'))

    def test_fails_when_the_pull_request_edits_another_row_only(self):
        before = status(NEXT + '| POS-01 | Sell | local | x |\n| web-shell | Shell | local | x |\n')
        after = status(NEXT + '| POS-01 | Sell | local | x |\n| web-shell | Shell, then screens | local | x |\n')
        self.assertFailsOn(self.check(before, after), '| POS-01 | Sell | local | x |')

    def test_passes_when_the_head_has_no_status_file(self):
        self.assertPasses(self.check(status(NEXT + '| POS-01 | Sell | local | x |\n'), None))


if __name__ == '__main__':
    unittest.main()
