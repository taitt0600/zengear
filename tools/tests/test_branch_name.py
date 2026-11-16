"""Tests of tools/branch_name.py (DEC-148 (10)): the name made from an issue, and the check of a branch against the
title of its pull request that tools/pr_gates.py runs.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import branch_name  # noqa: E402


class MakeTests(unittest.TestCase):
    def test_feature(self):
        self.assertEqual(branch_name.make(12, 'Feature', feature='STF-09'), 'feat/stf-09')

    def test_feature_web(self):
        self.assertEqual(branch_name.make(12, 'Feature', feature='STF-02', web=True), 'feat/stf-02-web')

    def test_feature_whose_name_has_merged_takes_the_next_number(self):
        self.assertEqual(branch_name.make(12, 'Feature', feature='STF-02', web=True,
                                          taken=['feat/stf-02-web']), 'feat/stf-02-web-2')
        self.assertEqual(branch_name.make(12, 'Feature', feature='AUT-04',
                                          taken=['feat/aut-04', 'feat/aut-04-2']), 'feat/aut-04-3')

    def test_bug(self):
        self.assertEqual(branch_name.make(31, 'Bug', feature='AUT-04'), 'fix/aut-04-31')

    def test_task_takes_its_type_label(self):
        self.assertEqual(branch_name.make(40, 'Task', labels=['docs'], title='Cập nhật tài liệu: Roadmap & STATUS'),
                         'docs/40-cap-nhat-tai-lieu-roadmap-status')

    def test_design_is_docs(self):
        self.assertEqual(branch_name.make(7, 'Design', title='SCR-STF-04 Phân quyền'), 'docs/7-scr-stf-04-phan-quyen')

    def test_vietnamese_d_is_kept_as_d(self):
        self.assertEqual(branch_name.make(8, 'Task', labels=['chore'], title='Đổi đường dẫn'), 'chore/8-doi-duong-dan')

    def test_at_most_50_characters_cut_at_a_word(self):
        name = branch_name.make(123, 'Task', labels=['refactor'],
                                title='move the shared version check of every service into the common folder')
        self.assertLessEqual(len(name), 50)
        self.assertFalse(name.endswith('-'))
        self.assertEqual(name, 'refactor/123-move-the-shared-version-check-of')
        self.assertIsNone(branch_name.problem(name, 'refactor: x'))

    def test_task_needs_exactly_one_type_label(self):
        with self.assertRaises(ValueError):
            branch_name.make(40, 'Task', labels=[], title='x')
        with self.assertRaises(ValueError):
            branch_name.make(40, 'Task', labels=['docs', 'ci'], title='x')

    def test_other_labels_are_ignored(self):
        self.assertEqual(branch_name.make(41, 'Task', labels=['defect', 'ci', 'invalid'], title='Gate'), 'ci/41-gate')

    def test_feature_needs_a_feature_id(self):
        with self.assertRaises(ValueError):
            branch_name.make(12, 'Feature')
        with self.assertRaises(ValueError):
            branch_name.make(12, 'Feature', feature='STF-9')

    def test_unknown_type(self):
        with self.assertRaises(ValueError):
            branch_name.make(12, 'Epic', title='x')

    def test_title_without_letters_gives_the_number_alone(self):
        self.assertEqual(branch_name.make(9, 'Task', labels=['ci'], title='!!!'), 'ci/9')


class ProblemTests(unittest.TestCase):
    def ok(self, branch, title):
        self.assertIsNone(branch_name.problem(branch, title), branch)

    def bad(self, branch, title, words):
        message = branch_name.problem(branch, title)
        self.assertIsNotNone(message, branch)
        self.assertIn(words, message)

    def test_valid_names(self):
        self.ok('feat/stf-09', 'feat(api): add staff approval (STF-09)')
        self.ok('feat/stf-02-web-2', 'feat(web): build the staff page (STF-02)')
        self.ok('fix/aut-04-31', 'fix(api): refuse a locked account (AUT-04)')
        self.ok('docs/40-roadmap', 'docs: describe the roadmap')
        self.ok('ci/41-gate', 'ci(ci): check the body')
        self.ok('build/42-commitizen', 'build!: pin commitizen')

    def test_prefix_must_match_the_title_type(self):
        self.bad('docs/40-roadmap', 'chore: describe the roadmap', 'type')
        self.bad('feat/stf-09', 'fix(api): x (STF-09)', 'type')

    def test_malformed(self):
        self.bad('feat/STF-09', 'feat: x', 'pattern')
        self.bad('fix/aut-04-name', 'fix: x', 'pattern')
        self.bad('docs/roadmap', 'docs: x', 'pattern')
        self.bad('docs/40-Roadmap', 'docs: x', 'pattern')
        self.bad('feature/stf-09', 'feat: x', 'pattern')
        self.bad('claude/abc-123', 'docs: x', 'pattern')
        self.bad('docs/40-a--b', 'docs: x', 'pattern')

    def test_too_long(self):
        self.bad('docs/40-' + 'a' * 43, 'docs: x', '50')

    def test_issue_number(self):
        self.assertEqual(branch_name.issue_number('fix/aut-04-31'), 31)
        self.assertEqual(branch_name.issue_number('docs/40-roadmap'), 40)
        self.assertEqual(branch_name.issue_number('ci/9'), 9)
        self.assertIsNone(branch_name.issue_number('feat/stf-09'))
        self.assertIsNone(branch_name.issue_number('feat/stf-09-2'))

    def test_feature_id(self):
        self.assertEqual(branch_name.feature_id('feat/stf-02-web-2'), 'STF-02')
        self.assertEqual(branch_name.feature_id('fix/aut-04-31'), 'AUT-04')
        self.assertIsNone(branch_name.feature_id('docs/40-roadmap'))


class CommandLineTests(unittest.TestCase):
    def test_make_prints_the_name(self):
        import contextlib
        import io
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = branch_name.main(['make', '--number', '12', '--type', 'Feature', '--feature', 'STF-09',
                                     '--taken', 'feat/stf-09'])
        self.assertEqual(code, 0)
        self.assertEqual(out.getvalue().strip(), 'feat/stf-09-2')

    def test_check_fails_with_the_reason(self):
        import contextlib
        import io
        err = io.StringIO()
        with contextlib.redirect_stderr(err):
            code = branch_name.main(['check', 'docs/roadmap', 'docs: x'])
        self.assertEqual(code, 1)
        self.assertIn('pattern', err.getvalue())


if __name__ == '__main__':
    unittest.main()
