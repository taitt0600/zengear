"""Tests of the gates of tools/pr_gates.py that read the pull request event and its issues: 5 (owner paths with
auto-merge, DEC-149 (1)), 8 (branch name), 9 (Closes), 10 (blockers), 11 (body), 12 (Refs #issue), the commit identity
of DEC-150 (1) and the exemption of dependabot[bot] (DEC-148 (10)).

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import os
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from test_pr_gates import BODY, FakeIssues, GitRepositoryTestCase, load_gates, valid_pull_request  # noqa: E402

FEAT = 'feat(api): add selling at the counter (POS-01)'
FEATURES = ('| Feature ID | Epic | Feature | User Story | Acceptance Criteria | Rules | Entities | Depends On | Priority '
            '| Phase |\n|---|---|---|---|---|---|---|---|---|---|\n'
            '| POS-01 | POS | Sell at counter | Story | Criteria | — | — | — | P1 | Phase 1 |\n')


def issue(number, state='open', type_name='Task', pull_request=False):
    data = {'number': number, 'state': state, 'type': {'name': type_name}, 'title': f'issue {number}'}
    if pull_request:
        data['pull_request'] = {}
    return data


class EventGateTests(GitRepositoryTestCase):
    def setUp(self):
        super().setUp()
        self.base = self.commit({'docs/blueprint/features.md': FEATURES, 'docs/a.md': 'a\n'}, 'docs: start\n')

    def gates(self, title, message=None, files=None, **event):
        self.count = getattr(self, 'count', 0) + 1
        files = {name: f'{text}{self.count}\n' for name, text in (files or {'docs/a.md': 'b\n'}).items()}
        head = self.commit(files, message or f'{title.split(" (")[0]}\n\nRefs: #1\n')
        return self.run_check(self.base, head, title, **event)

    def assertPasses(self, result):
        self.assertEqual(result[0], 0, result[1])

    def assertFails(self, result, words):
        code, out = result
        self.assertEqual(code, 1, out)
        self.assertIn(words, out)

    # 5
    def test_owner_paths_with_auto_merge_fail(self):
        self.assertFails(self.gates('ci: change a tool', files={'tools/x.py': 'x\n'}, pr={'auto_merge': {'merge_method': 'rebase'}}),
                         'auto-merge is on, but the pull request changes files the owner merges')

    def test_owner_paths_without_auto_merge_pass(self):
        self.assertPasses(self.gates('ci: change a tool', files={'tools/x.py': 'x\n'}))

    def test_auto_merge_without_owner_paths_passes(self):
        self.assertPasses(self.gates('docs: change a page', pr={'auto_merge': {'merge_method': 'rebase'}}))

    # 8
    def test_a_branch_outside_the_pattern(self):
        self.assertFails(self.gates('docs: change a page', pr={'head': {'ref': 'docs/dec-148'}}), 'pattern of DEC-148 (10)')

    def test_a_branch_of_another_type(self):
        self.assertFails(self.gates('docs: change a page', pr={'head': {'ref': 'chore/1-change'}}), 'is of type chore')

    # 9
    def test_the_branch_issue_must_be_closed(self):
        self.assertFails(self.gates('docs: change a page', pr={'body': BODY.format(closes=2)},
                                    api=FakeIssues({2: issue(2)})), 'no "Closes #1"')

    def test_a_closed_issue_cannot_be_closed_again(self):
        self.assertFails(self.gates('docs: change a page', api=FakeIssues({1: issue(1, state='closed')})),
                         'Closes #1: the issue is not open')

    def test_a_pull_request_is_not_an_issue(self):
        self.assertFails(self.gates('docs: change a page', api=FakeIssues({1: issue(1, pull_request=True)})),
                         'is a pull request, not an issue')

    def test_feat_closes_its_feature_issue(self):
        self.assertPasses(self.gates(FEAT))
        self.assertFails(self.gates(FEAT, api=FakeIssues({1: issue(1, type_name='Task')})),
                         'closes its open Feature issue')
        self.assertFails(self.gates(FEAT, api=FakeIssues({1: issue(1, type_name='Feature')}, {1: 'POS-010'})),
                         'closes its open Feature issue')

    def test_at_most_one_open_feature_issue_per_id(self):
        api = FakeIssues({1: issue(1, type_name='Feature'), 5: issue(5, type_name='Feature')}, {1: 'POS-01', 5: 'POS-01'})
        self.assertFails(self.gates(FEAT, api=api), 'more than one open Feature issue: #1, #5')

    def test_an_unreadable_github_fails(self):
        self.assertFails(self.gates('docs: change a page', api=FakeIssues(error=OSError('HTTP 401'))),
                         'cannot read the issues of the pull request from GitHub: HTTP 401')

    # 10
    def test_an_open_blocker_fails_a_feat_pull_request(self):
        api = FakeIssues({1: issue(1, type_name='Feature')}, {1: 'POS-01'}, {1: [{'number': 7, 'title': 'Decide'}]})
        self.assertFails(self.gates(FEAT, api=api), '#1 is still blocked by the open issue #7 "Decide"')

    def test_a_blocker_closed_by_this_pull_request_passes(self):
        # DEC-148 (6): a blocker may be done in the feature's own pull request, which then closes it.
        api = FakeIssues({1: issue(1, type_name='Feature'), 7: issue(7)}, {1: 'POS-01'},
                         {1: [{'number': 7, 'title': 'Decide', 'state': 'open'}]})
        body = BODY.format(closes=1).replace('Closes #1', 'Closes #1, closes #7')
        self.assertPasses(self.gates(FEAT, pr={'body': body}, api=api))

    def test_a_blocker_not_named_in_closes_fails(self):
        api = FakeIssues({1: issue(1, type_name='Feature'), 7: issue(7)}, {1: 'POS-01'},
                         {1: [{'number': 7, 'title': 'Decide', 'state': 'open'}, {'number': 8, 'title': 'Other',
                                                                                  'state': 'open'}]})
        body = BODY.format(closes=1).replace('Closes #1', 'Closes #1, closes #7')
        result = self.gates(FEAT, pr={'body': body}, api=api)
        self.assertFails(result, '#1 is still blocked by the open issue #8 "Other"')
        self.assertNotIn('open issue #7', result[1])

    def test_a_closed_blocker_passes(self):
        api = FakeIssues({1: issue(1, type_name='Feature')}, {1: 'POS-01'},
                         {1: [{'number': 7, 'title': 'Decide', 'state': 'closed'}]})
        self.assertPasses(self.gates(FEAT, api=api))

    # 11
    def test_a_body_without_the_template(self):
        self.assertFails(self.gates('docs: change a page', pr={'body': 'Closes #1'}),
                         'the body has no section "## Needs decision"')

    def test_a_summary_without_its_four_parts(self):
        body = BODY.format(closes=1).replace('**Rủi ro và kết quả rà**', '**Risks**')
        self.assertFails(self.gates('docs: change a page', pr={'body': body}), 'no part "Rủi ro và kết quả rà"')

    def test_an_open_needs_decision_needs_a_draft(self):
        body = BODY.format(closes=1).replace('## Needs decision\n\nKhông có', '## Needs decision\n\n1. Pick A or B')
        self.assertFails(self.gates('docs: change a page', pr={'body': body}), 'stays a draft')
        self.assertPasses(self.gates('docs: change a page', pr={'body': body, 'draft': True}))

    def test_an_empty_needs_decision(self):
        body = BODY.format(closes=1).replace('## Needs decision\n\nKhông có', '## Needs decision\n\n<!-- x -->')
        self.assertFails(self.gates('docs: change a page', pr={'body': body}), 'is empty')

    # 12
    def test_a_feat_commit_names_its_issue(self):
        self.assertFails(self.gates(FEAT, message='feat(api): add selling\n\nRefs: POS-01\n'),
                         'its Refs: footer names no issue this pull request closes')
        self.base = self.git('rev-parse', 'HEAD')
        self.assertPasses(self.gates(FEAT, message='feat(api): add selling\n\nRefs: POS-01, #1\n'))

    def test_a_docs_commit_needs_no_issue_in_refs(self):
        self.assertPasses(self.gates('docs: change a page', message='docs: change a page\n'))

    # identity
    def test_a_commit_of_another_identity_fails(self):
        self.git('config', 'user.email', 'someone@example.com')
        self.assertFails(self.gates('docs: change a page'), 'not by the owner')

    # dependabot
    def test_dependabot_is_exempt_from_branch_body_closes_and_identity(self):
        self.git('config', 'user.name', 'dependabot[bot]')
        self.git('config', 'user.email', '49699333+dependabot[bot]@users.noreply.github.com')
        result = self.gates('build(deps): bump x', message='build(deps): bump x\n',
                            pr={'head': {'ref': 'dependabot/pip/tools/x-1.0'}, 'body': 'Bumps x.',
                                'user': {'login': 'dependabot[bot]'}}, api=FakeIssues(error=AssertionError('no read')))
        self.assertPasses(result)


class LocalEventTests(GitRepositoryTestCase):
    def test_check_local_builds_the_event_of_the_current_branch(self):
        self.commit({'docs/a.md': 'a\n'}, 'docs: start\n')
        self.git('switch', '-q', '-c', 'docs/1-change')
        body = self.dir / 'body.md'
        body.write_text(BODY.format(closes=1), encoding='utf-8')
        cwd = os.getcwd()
        try:
            os.chdir(self.dir)
            event = load_gates().local_event('docs: change', str(body), True)
        finally:
            os.chdir(cwd)
        pr = event['pull_request']
        self.assertEqual((pr['head']['ref'], pr['draft'], pr['auto_merge'], pr['title']),
                         ('docs/1-change', True, None, 'docs: change'))
        self.assertIn('Closes #1', pr['body'])


class BodyErrorTests(unittest.TestCase):
    def test_the_template_passes(self):
        gates = load_gates()
        template = (Path(__file__).resolve().parents[2] / '.github' / 'pull_request_template.md').read_text(encoding='utf-8')
        self.assertEqual(gates.body_errors(template, draft=False), [])

    def test_a_body_without_how_to_review_is_refused(self):
        # DEC-152 (7): the section the template gained with the CI quality gates.
        gates = load_gates()
        template = (Path(__file__).resolve().parents[2] / '.github' / 'pull_request_template.md').read_text(encoding='utf-8')
        body = template.replace('## How to review', '## Notes')
        self.assertIn('the body has no section "## How to review" of .github/pull_request_template.md',
                      gates.body_errors(body, draft=False))

    def test_closing_keywords(self):
        gates = load_gates()
        self.assertEqual(gates.closing_issues('Closes #3, fixes #4\nResolved: #5\nprefix #6 <!-- closes #7 -->'), [3, 4, 5])
        self.assertEqual(gates.closing_issues('Closes #<issue>'), [])

    def test_valid_pull_request_helper_is_valid(self):
        pr, _ = valid_pull_request('docs: x')
        self.assertEqual(load_gates().body_errors(pr['body'], False), [])


if __name__ == '__main__':
    unittest.main()
