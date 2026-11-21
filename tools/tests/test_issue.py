"""Tests of tools/issue.py with a fake GitHub: the body of a form as GitHub renders it (checked on issue #69), the lint
of issue-lint, the blocked-by requests of issue-deps with every case named by DEC-151, the check run of code-review
(DEC-150 (3)), the reader the PR gates and the dossier use, and the order of next work (DEC-148 (7)).

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import contextlib
import io
import tempfile
import subprocess
import sys
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))
import comment_templates as ct  # noqa: E402
import issue  # noqa: E402
from github_api import GitHubError  # noqa: E402

SHA = 'b' * 40
OTHER = 'c' * 40
try:
    import yaml  # noqa: F401
    HAS_YAML = True
except ImportError:
    HAS_YAML = False


class FakeGitHub:
    repo = 'taitt0600/zengear'
    owner = 'taitt0600'
    proxy = False

    def __init__(self, issues=None, blocked_by=None, comments=None, pulls=None, graphql=None):
        self.issues = issues or {}
        self.blocked_by = blocked_by or {}
        self.comments = comments or {}
        self.pulls = pulls or {}
        self.graphql_answer = graphql or {}
        self.calls = []

    def issue(self, number):
        if number not in self.issues:
            raise GitHubError(f'GET /issues/{number}: HTTP 404', 404)
        return self.issues[number]

    def pull(self, number):
        return self.pulls[number]

    def pages(self, path, limit=1000):
        self.calls.append(('GET', path))
        parts = path.split('/')
        if path.endswith('/dependencies/blocked_by'):
            return self.blocked_by.get(int(parts[-3]), [])
        if path.endswith('/comments'):
            return self.comments.get(int(parts[-2]), [])
        if path.endswith('/pulls?state=open'):
            return list(self.pulls.values())
        if '/sub_issues' in path:
            return self.issues_list
        raise AssertionError(path)

    def call(self, method, path, body=None):
        self.calls.append((method, path, body))
        return {'id': 1, 'html_url': 'x'}

    def graphql(self, query, variables):
        self.calls.append(('GRAPHQL', variables))
        for key, answer in self.graphql_answer.items():
            if key in query:
                return answer(variables) if callable(answer) else answer
        raise AssertionError(query)

    def posted(self):
        return [body['body'] for method, path, body in (c for c in self.calls if c[0] == 'POST')
                if path.endswith('/comments')]


def an_issue(number, state='open', type_name=None, title='x', labels=(), body='', pull_request=False):
    data = {'number': number, 'id': 1000 + number, 'state': state, 'title': title, 'body': body,
            'labels': [{'name': name} for name in labels], 'type': {'name': type_name} if type_name else None,
            'user': {'login': 'taitt0600'}}
    if pull_request:
        data['pull_request'] = {'url': 'x'}
    return data


def comment_event(body, number=10, login='taitt0600', app=None, on_pull_request=False, comment_id=555):
    return {'issue': an_issue(number, pull_request=on_pull_request),
            'comment': {'id': comment_id, 'body': body, 'user': {'login': login}, 'performed_via_github_app': app}}


class ImportTests(unittest.TestCase):
    def test_importing_issue_loads_no_yaml(self):
        # pr-gates, issue-deps and code-review run the system python3 without packages.
        run = subprocess.run([sys.executable, '-c', 'import sys; sys.path.insert(0, "tools"); import issue; '
                              'print("yaml" in sys.modules)'], cwd=REPO, capture_output=True, text=True)
        self.assertEqual(run.stdout.strip(), 'False', run.stderr)


@unittest.skipUnless(HAS_YAML, 'PyYAML (tools/requirements.txt) is needed')
class FormTests(unittest.TestCase):
    def test_render_follows_the_format_of_github(self):
        body = issue.render('defect', {'feature': 'AUT-04', 'steps': '1. a', 'expected': 'b', 'actual': 'c'})
        self.assertEqual(body, '### Feature ID\n\nAUT-04\n\n### Pull request tested\n\n_No response_\n\n'
                               '### Steps\n\n1. a\n\n### Expected result\n\nb\n\n### Actual result\n\nc\n')

    def test_render_refuses_an_unknown_or_missing_required_field(self):
        with self.assertRaisesRegex(ValueError, 'unknown'):
            issue.render('defect', {'feature': 'AUT-04', 'nope': 'x', 'steps': 'a', 'expected': 'b', 'actual': 'c'})
        with self.assertRaisesRegex(ValueError, 'required'):
            issue.render('defect', {'feature': 'AUT-04'})

    def test_every_form_has_a_type_and_renders(self):
        types = {}
        for name in issue.FORM_OF_TYPE.values():
            form = issue.load_form(name)
            types[form['type']] = name
            fields = {item['id']: 'x' for item in issue.inputs(form)}
            self.assertEqual(issue.lint_problems(an_issue(1, type_name=form['type'], title=issue.EXAMPLE_TITLES[name],
                                                          labels=['docs'] if name == 'task' else [],
                                                          body=issue.render(name, fields))), [], name)
        self.assertEqual(sorted(types), ['Bug', 'Design', 'Feature', 'Task'])

    def test_defect_keeps_its_label(self):
        self.assertIn('defect', issue.load_form('defect')['labels'])

    def test_lint_lists_what_is_missing(self):
        body = '### Source\n\n_No response_\n\n### Done when\n\nx\n'
        problems = issue.lint_problems(an_issue(1, type_name='Task', title='Do x', labels=[], body=body))
        self.assertTrue(any('Source' in p for p in problems), problems)
        self.assertTrue(any('type label' in p for p in problems), problems)

    def test_lint_titles(self):
        body = issue.render('feature', {'source': 'docs/blueprint/features.md'})
        self.assertTrue(issue.lint_problems(an_issue(1, type_name='Feature', title='Approve staff', body=body)))
        self.assertEqual(issue.lint_problems(an_issue(1, type_name='Feature', title='STF-09 web', body=body)), [])

    def test_lint_without_type(self):
        self.assertEqual(issue.lint_problems(an_issue(24, labels=['log'])), [])
        self.assertTrue(any('Issue type' in p for p in issue.lint_problems(an_issue(2, body='free text'))))
        body = issue.render('defect', {'feature': 'AUT-04', 'steps': 'a', 'expected': 'b', 'actual': 'c'})
        self.assertEqual(issue.lint_problems(an_issue(3, labels=['defect'], title='AUT-04: x', body=body)), [])

    def test_lint_labels_and_comments(self):
        event = {'issue': an_issue(5, type_name='Task', title='x', labels=['docs'], body='nothing')}
        gh = FakeGitHub(issues={5: event['issue']})
        with contextlib.redirect_stdout(io.StringIO()):
            issue.lint(event, gh)
        methods = [(c[0], c[1]) for c in gh.calls]
        self.assertIn(('POST', '/repos/taitt0600/zengear/issues/5/labels'), methods)
        self.assertEqual(ct.parse(gh.posted()[0])[0], 'issue-lint')
        fixed = {'issue': an_issue(5, type_name='Task', title='x', labels=['docs', 'invalid'],
                                   body=issue.render('task', {'source': 'a', 'done': 'b'}))}
        gh = FakeGitHub(issues={5: fixed['issue']},
                        comments={5: [{'id': 9, 'body': ct.issue_lint(['x']), 'user': {'login': 'github-actions[bot]'}}]})
        with contextlib.redirect_stdout(io.StringIO()):
            issue.lint(fixed, gh)
        methods = [(c[0], c[1]) for c in gh.calls]
        self.assertIn(('DELETE', '/repos/taitt0600/zengear/issues/5/labels/invalid'), methods)
        self.assertIn(('DELETE', '/repos/taitt0600/zengear/issues/comments/9'), methods)

    def test_lint_cleans_the_comment_the_owner_token_posted(self):
        fixed = {'issue': an_issue(5, type_name='Task', title='x', labels=['docs'],
                                   body=issue.render('task', {'source': 'a', 'done': 'b'}))}
        comments = {5: [{'id': 9, 'body': ct.issue_lint(['x']), 'user': {'login': 'taitt0600'}},
                        {'id': 10, 'body': ct.issue_lint(['x']), 'user': {'login': 'someone'}}]}
        gh = FakeGitHub(issues={5: fixed['issue']}, comments=comments)
        with contextlib.redirect_stdout(io.StringIO()):
            issue.lint(fixed, gh)
        deleted = [c[1] for c in gh.calls if c[0] == 'DELETE']
        self.assertIn('/repos/taitt0600/zengear/issues/comments/9', deleted)
        self.assertNotIn('/repos/taitt0600/zengear/issues/comments/10', deleted)

    def test_lint_reads_the_issue_as_it_is_now(self):
        # #274: an issue made through the GitHub MCP has no type in its opened payload; the type set after it, the
        # labels, the title and the body are read from the API when the workflow runs.
        body = issue.render('feature', {'source': 'docs/blueprint/features.md STF-09'})
        opened = {'issue': an_issue(7, type_name=None, title='STF-09 Approve staff accounts', body=body)}
        now = an_issue(7, type_name='Feature', title='STF-09 Approve staff accounts', body=body)
        fields = {'repository': {'issue': {'issueFieldValues': {'nodes': [
            {'value': 'STF-09', 'field': {'name': 'Feature ID'}}]}}}}
        gh = FakeGitHub(issues={7: now}, graphql={'issueFieldValues': fields})
        with contextlib.redirect_stdout(io.StringIO()) as out:
            issue.lint(opened, gh)
        self.assertIn('#7 follows its form', out.getvalue())
        self.assertFalse([c for c in gh.calls if c[0] == 'POST'])


class DepsTests(unittest.TestCase):
    """DEC-151 (1) and its Consequences."""

    def run_deps(self, event, gh):
        with contextlib.redirect_stdout(io.StringIO()) as out:
            code = issue.deps(event, gh)
        return code, out.getvalue()

    def result(self, gh):
        return ct.parse(gh.posted()[-1])[1]

    def test_a_valid_marker_adds_the_relation_by_id(self):
        gh = FakeGitHub(issues={12: an_issue(12)})
        code, _ = self.run_deps(comment_event(ct.blocked_by(12)), gh)
        self.assertEqual(code, 0)
        self.assertIn(('POST', '/repos/taitt0600/zengear/issues/10/dependencies/blocked_by', {'issue_id': 1012}),
                      gh.calls)
        self.assertEqual(self.result(gh), {'request': '555', 'result': 'ok'})

    def test_another_author_is_ignored(self):
        gh = FakeGitHub(issues={12: an_issue(12)})
        code, out = self.run_deps(comment_event(ct.blocked_by(12), login='someone'), gh)
        self.assertEqual((code, gh.calls), (0, []))
        self.assertIn('ignored', out)

    def test_a_comment_on_a_pull_request_is_ignored(self):
        gh = FakeGitHub(issues={12: an_issue(12)})
        self.assertEqual(self.run_deps(comment_event(ct.blocked_by(12), on_pull_request=True), gh)[0], 0)
        self.assertEqual(gh.calls, [])

    def test_a_free_comment_is_ignored(self):
        gh = FakeGitHub()
        self.run_deps(comment_event('blocked by #12, I think'), gh)
        self.assertEqual(gh.calls, [])

    def test_an_issue_that_does_not_exist(self):
        gh = FakeGitHub()
        code, _ = self.run_deps(comment_event(ct.blocked_by(99)), gh)
        self.assertEqual(code, 1)
        self.assertEqual(self.result(gh)['result'], 'error')

    def test_the_issue_itself(self):
        gh = FakeGitHub(issues={10: an_issue(10)})
        code, _ = self.run_deps(comment_event(ct.blocked_by(10)), gh)
        self.assertEqual((code, self.result(gh)['result']), (1, 'error'))
        self.assertFalse([c for c in gh.calls if c[0] == 'POST' and 'dependencies' in c[1]])

    def test_a_pull_request_cannot_block(self):
        gh = FakeGitHub(issues={12: an_issue(12, pull_request=True)})
        self.assertEqual(self.run_deps(comment_event(ct.blocked_by(12)), gh)[0], 1)
        self.assertEqual(self.result(gh)['result'], 'error')

    def test_a_relation_that_exists_is_ok_without_a_call(self):
        gh = FakeGitHub(issues={12: an_issue(12)}, blocked_by={10: [an_issue(12)]})
        self.assertEqual(self.run_deps(comment_event(ct.blocked_by(12)), gh)[0], 0)
        self.assertEqual(self.result(gh)['result'], 'ok')
        self.assertFalse([c for c in gh.calls if c[0] == 'POST' and 'dependencies' in c[1]])

    def test_unblock_through_the_app_while_the_blocker_is_open_is_refused(self):
        gh = FakeGitHub(issues={12: an_issue(12)}, blocked_by={10: [an_issue(12)]})
        code, _ = self.run_deps(comment_event(ct.unblock(12), app={'slug': 'claude'}), gh)
        self.assertEqual((code, self.result(gh)['result']), (0, 'refused'))
        self.assertFalse([c for c in gh.calls if c[0] == 'DELETE'])

    def test_unblock_through_the_app_once_the_blocker_is_closed(self):
        gh = FakeGitHub(issues={12: an_issue(12, state='closed')}, blocked_by={10: [an_issue(12, state='closed')]})
        self.run_deps(comment_event(ct.unblock(12), app={'slug': 'claude'}), gh)
        self.assertIn(('DELETE', '/repos/taitt0600/zengear/issues/10/dependencies/blocked_by/1012', None), gh.calls)

    def test_unblock_by_the_owner_while_open(self):
        gh = FakeGitHub(issues={12: an_issue(12)}, blocked_by={10: [an_issue(12)]})
        self.run_deps(comment_event(ct.unblock(12)), gh)
        self.assertIn(('DELETE', '/repos/taitt0600/zengear/issues/10/dependencies/blocked_by/1012', None), gh.calls)
        self.assertEqual(self.result(gh)['result'], 'ok')


class ReviewCheckTests(unittest.TestCase):
    """DEC-150 (3): success only when the latest unedited code-review marker of the owner for the head says pass."""

    def check_run(self, comments, login='taitt0600', event=None):
        pr = {'number': 7, 'head': {'sha': SHA}, 'user': {'login': login}}
        gh = FakeGitHub(comments={7: comments}, pulls={7: pr})
        event = event or {'issue': an_issue(7, pull_request=True), 'comment': {'id': 1}}
        with contextlib.redirect_stdout(io.StringIO()):
            issue.review_check(event, gh)
        posted = [c[2] for c in gh.calls if c[0] == 'POST' and c[1].endswith('/check-runs')]
        self.assertEqual(len(posted), 1)
        self.assertEqual((posted[0]['name'], posted[0]['head_sha']), ('code-review', SHA))
        return posted[0]['conclusion']

    def marker(self, sha, verdict, created='2026-11-16T10:00:00Z', updated=None, login='taitt0600', cid=1):
        return {'id': cid, 'body': ct.code_review(sha, verdict), 'user': {'login': login},
                'created_at': created, 'updated_at': updated or created}

    def test_pass_for_the_head(self):
        self.assertEqual(self.check_run([self.marker(SHA, 'pass')]), 'success')

    def test_no_marker(self):
        self.assertEqual(self.check_run([]), 'failure')

    def test_pass_for_another_commit(self):
        self.assertEqual(self.check_run([self.marker(OTHER, 'pass')]), 'failure')

    def test_latest_marker_for_the_head_wins(self):
        comments = [self.marker(SHA, 'pass', '2026-11-16T10:00:00Z', cid=1),
                    self.marker(SHA, 'blocking', '2026-11-16T11:00:00Z', cid=2)]
        self.assertEqual(self.check_run(comments), 'failure')
        comments.append(self.marker(SHA, 'pass', '2026-11-16T12:00:00Z', cid=3))
        self.assertEqual(self.check_run(comments), 'success')

    def test_an_edited_comment_does_not_count(self):
        self.assertEqual(self.check_run([self.marker(SHA, 'pass', updated='2026-11-16T10:05:00Z')]), 'failure')

    def test_another_author_does_not_count(self):
        self.assertEqual(self.check_run([self.marker(SHA, 'pass', login='someone')]), 'failure')

    def test_dependabot_is_exempt(self):
        self.assertEqual(self.check_run([], login='dependabot[bot]'), 'success')

    def test_pull_request_event_reads_its_head(self):
        event = {'pull_request': {'number': 7, 'head': {'sha': SHA}, 'user': {'login': 'taitt0600'}}}
        self.assertEqual(self.check_run([self.marker(SHA, 'pass')], event=event), 'success')


class ReaderTests(unittest.TestCase):
    def fields_answer(self, value):
        return {'repository': {'issue': {'issueFieldValues': {'nodes': [
            {'value': value, 'field': {'name': 'Feature ID'}}, {'value': '2026-11-16', 'field': {'name': 'Start date'}},
            {'name': 'High', 'field': {'name': 'Priority'}}]}}}}

    def test_feature_id_and_fields(self):
        gh = FakeGitHub(graphql={'issueFieldValues': self.fields_answer('STF-09')})
        reader = issue.Reader(gh)
        self.assertEqual(reader.feature_id(12), 'STF-09')
        self.assertEqual(reader.fields(12), {'Feature ID': 'STF-09', 'Start date': '2026-11-16', 'Priority': 'High'})

    def test_open_feature_issues_match_the_whole_id(self):
        nodes = [{'number': 1, 'title': 'POS-01 Sell', 'issueType': {'name': 'Feature'},
                  'issueFieldValues': {'nodes': [{'value': 'POS-01', 'field': {'name': 'Feature ID'}}]}},
                 {'number': 2, 'title': 'POS-010 Other', 'issueType': {'name': 'Feature'},
                  'issueFieldValues': {'nodes': [{'value': 'POS-010', 'field': {'name': 'Feature ID'}}]}},
                 {'number': 3, 'title': 'POS-01 web', 'issueType': {'name': 'Task'},
                  'issueFieldValues': {'nodes': [{'value': 'POS-01', 'field': {'name': 'Feature ID'}}]}}]
        gh = FakeGitHub(graphql={'search(': {'search': {'nodes': nodes}}})
        self.assertEqual(issue.Reader(gh).open_feature_issues('POS-01'), [1])

    def test_start_keeps_the_first_date(self):
        gh = FakeGitHub(issues={12: dict(an_issue(12), node_id='I_12')},
                        graphql={'issueFieldValues': self.fields_answer('STF-09')})
        with contextlib.redirect_stdout(io.StringIO()) as out:
            issue.start(gh, 12)
        self.assertIn('2026-11-16 (kept)', out.getvalue())
        self.assertFalse([c for c in gh.calls if c[0] == 'GRAPHQL' and 'value' in c[1]])

    def test_start_sets_today_when_unset(self):
        answers = {'updateIssue': {'updateIssue': {'issue': {'number': 12}}},
                   'issueFieldValues': {'repository': {'issue': {'issueFieldValues': {'nodes': []}}}}}
        gh = FakeGitHub(issues={12: dict(an_issue(12), node_id='I_12')}, graphql=answers)
        with contextlib.redirect_stdout(io.StringIO()):
            issue.start(gh, 12)
        mutation = [c[1] for c in gh.calls if c[0] == 'GRAPHQL' and 'value' in c[1]]
        self.assertEqual((mutation[0]['id'], mutation[0]['name']), ('I_12', 'Start date'))

    @unittest.skipUnless(HAS_YAML, 'PyYAML (tools/requirements.txt) is needed: the lint after the field reads a form')
    def test_create_reuses_an_open_issue_and_sets_its_feature_id(self):
        existing = dict(an_issue(30, type_name='Feature', title='STF-09 Approve staff accounts'), node_id='I_30',
                        html_url='x')

        class Gh(FakeGitHub):
            def pages(self, path, limit=1000):
                if path.endswith('/issues?state=open'):
                    return [existing]
                return super().pages(path, limit)

        answers = {'updateIssue': {'updateIssue': {'issue': {'number': 30}}},
                   'issueFieldValues': {'repository': {'issue': {'issueFieldValues': {'nodes': []}}}}}
        gh = Gh(issues={30: existing}, graphql=answers)
        with contextlib.redirect_stdout(io.StringIO()) as out:
            issue.create(gh, 'feature', 'STF-09 Approve staff accounts', {'source': 'x'}, feature_id='STF-09')
        self.assertIn('#30', out.getvalue())
        self.assertFalse([c for c in gh.calls if c[0] == 'POST' and c[1].endswith('/issues')])
        mutation = [c[1] for c in gh.calls if c[0] == 'GRAPHQL' and 'value' in c[1]]
        self.assertEqual((mutation[0]['name'], mutation[0]['value']), ('Feature ID', 'STF-09'))

    @unittest.skipUnless(HAS_YAML, 'PyYAML (tools/requirements.txt) is needed: the type comes from the form')
    def test_create_refuses_an_issue_of_another_author_or_type(self):
        for login, type_name in (('someone', 'Feature'), ('taitt0600', 'Task')):
            existing = dict(an_issue(30, type_name=type_name, title='STF-09 Approve staff accounts'),
                            html_url='x', user={'login': login})

            class Gh(FakeGitHub):
                def pages(self, path, limit=1000):
                    if path.endswith('/issues?state=open'):
                        return [existing]
                    return super().pages(path, limit)

            gh = Gh(issues={30: existing})
            with self.subTest(login=login, type_name=type_name), self.assertRaisesRegex(ValueError, '#30'):
                issue.create(gh, 'feature', 'STF-09 Approve staff accounts', {'source': 'x'}, feature_id='STF-09')
            self.assertEqual([c for c in gh.calls if c[0] in ('POST', 'GRAPHQL', 'PATCH')], [])

    @unittest.skipUnless(HAS_YAML, 'PyYAML (tools/requirements.txt) is needed: the type comes from the form')
    def test_create_reuse_keeps_a_matching_feature_id(self):
        existing = dict(an_issue(30, type_name='Feature', title='STF-09 Approve staff accounts'), node_id='I_30',
                        html_url='x')

        class Gh(FakeGitHub):
            def pages(self, path, limit=1000):
                if path.endswith('/issues?state=open'):
                    return [existing]
                return super().pages(path, limit)

        gh = Gh(issues={30: existing}, graphql={'issueFieldValues': self.fields_answer('STF-09')})
        with contextlib.redirect_stdout(io.StringIO()):
            issue.create(gh, 'feature', 'STF-09 Approve staff accounts', {'source': 'x'}, feature_id='STF-09')
        self.assertFalse([c for c in gh.calls if c[0] == 'GRAPHQL' and 'value' in c[1]])

    def test_open_blockers(self):
        gh = FakeGitHub(blocked_by={10: [an_issue(12), an_issue(13, state='closed')]})
        self.assertEqual([b['number'] for b in issue.Reader(gh).open_blockers(10)], [12])


class RoadmapTests(unittest.TestCase):
    """DEC-148 (3), DEC-151 (3): Priority High exactly on the open issues that block a Roadmap sub-issue, any type."""

    class Fake:
        def roadmap_issue(self):
            return 30

        def roadmap(self, parent=None):
            assert parent == 30, parent
            return [40, 41]

        def open_issues(self):
            return [{'number': 30, 'title': 'Roadmap', 'type': 'Task', 'fields': {'Priority': 'High'}},
                    {'number': 40, 'title': 'STF-09 Approve', 'type': 'Feature', 'fields': {}},
                    {'number': 41, 'title': 'Build the layout', 'type': 'Task', 'fields': {}},
                    {'number': 50, 'title': 'Decide tax', 'type': 'Task', 'fields': {'Priority': 'High'}},
                    {'number': 51, 'title': 'Rate limit', 'type': 'Task', 'fields': {'Priority': 'Medium'}},
                    {'number': 52, 'title': 'Old high', 'type': 'Task', 'fields': {'Priority': 'High'}}]

        def open_blockers(self, number):
            return {40: [{'number': 50, 'title': 'Decide tax'}], 41: [{'number': 51, 'title': 'Rate limit'}]}[number]

    def test_relations_of_every_sub_issue_and_the_mismatches(self):
        text = '\n'.join(issue.roadmap_report(self.Fake()))
        self.assertIn('#40 STF-09 Approve: blocked by #50', text)
        self.assertIn('#41 Build the layout: blocked by #51', text)  # a Task sub-issue is read too
        self.assertIn('#51 blocks a Roadmap sub-issue but its Priority is Medium', text)
        self.assertIn('#52 has Priority High but blocks no Roadmap sub-issue', text)
        self.assertNotIn('#50 blocks', text)

    def test_the_roadmap_issue_itself_is_left_out(self):
        text = '\n'.join(issue.roadmap_report(self.Fake()))
        self.assertNotIn('#30', text)

    def test_no_pinned_roadmap(self):
        class NoRoadmap(self.Fake):
            def roadmap_issue(self):
                return None

            def roadmap(self, parent=None):
                raise AssertionError('no Roadmap to read')

        text = '\n'.join(issue.roadmap_report(NoRoadmap()))
        self.assertIn('#52 has Priority High but blocks no Roadmap sub-issue', text)
        self.assertIn('#30 has Priority High', text)  # without a pinned Roadmap, an issue titled so is not left out


class NextTests(unittest.TestCase):
    def test_order_of_dec_148_7(self):
        owner = 'taitt0600'
        open_issues = [
            {'number': 31, 'title': 'AUT-04: locked account', 'author': owner, 'type': 'Bug', 'fields': {}},
            {'number': 40, 'title': 'STF-09 Approve staff', 'author': owner, 'type': 'Feature',
             'fields': {'Feature ID': 'STF-09', 'Start date': '2026-11-10'}},
            {'number': 41, 'title': 'Storefront rate limit', 'author': owner, 'type': 'Task',
             'fields': {'Trigger': 'storefront deploy'}},
            {'number': 42, 'title': 'STF-13 Activity', 'author': owner, 'type': 'Feature',
             'fields': {'Feature ID': 'STF-13'}},
        ]

        class Fake:
            def pulls(self):
                return [{'number': 70, 'title': 'feat(api): x (STF-15)', 'draft': False, 'body': 'Closes #50',
                         'user': {'login': owner}},
                        {'number': 71, 'title': 'feat(api): y (STF-11)', 'draft': True, 'body': '',
                         'user': {'login': owner}}]

            def open_issues(self):
                return open_issues

            def has_handoff(self, number):
                return number == 40

            def roadmap(self):
                return [42, 40]

            def open_blockers(self, number):
                return [{'number': 41, 'title': 'x'}] if number == 42 else []

        lines = issue.next_work(Fake(), ready=lambda fid, screens: fid != 'STF-13')
        text = '\n'.join(lines)
        order = [text.index(marker) for marker in ('#70', '#71', '#31', '#40', '#42', '#41')]
        self.assertEqual(order, sorted(order), text)
        self.assertIn('not ready', text)

    def test_only_the_owner_and_dependabot_items_dec_152_6(self):
        open_issues = [
            {'number': 31, 'title': 'AUT-04: owner bug', 'author': 'taitt0600', 'type': 'Bug', 'fields': {}},
            {'number': 32, 'title': 'AUT-04: stranger bug', 'author': 'someone', 'type': 'Bug', 'fields': {}},
            {'number': 33, 'title': 'Stranger task', 'author': 'someone', 'type': 'Task', 'fields': {'Trigger': 'x'}},
            {'number': 34, 'title': 'Ghost task', 'author': None, 'type': 'Task', 'fields': {'Trigger': 'y'}},
            {'number': 35, 'title': 'STF-09 Approve', 'author': 'taitt0600', 'type': 'Feature',
             'fields': {'Feature ID': 'STF-09', 'Start date': '2026-11-10'}},
        ]

        class Fake:
            def pulls(self):
                return [{'number': 70, 'title': 'owner pr', 'draft': False, 'body': '', 'user': {'login': 'taitt0600'}},
                        {'number': 71, 'title': 'bump x', 'draft': False, 'body': '',
                         'user': {'login': 'dependabot[bot]'}},
                        {'number': 72, 'title': 'fork pr', 'draft': False, 'body': 'Closes #35',
                         'user': {'login': 'someone'}},
                        {'number': 73, 'title': 'fork draft', 'draft': True, 'body': '', 'user': None}]

            def open_issues(self):
                return open_issues

            def has_handoff(self, number):
                return number == 35

            def roadmap(self):
                return [33, 31]

            def open_blockers(self, number):
                return []

        text = '\n'.join(issue.next_work(Fake(), ready=lambda fid, screens: True))
        # A stranger's "Closes #35" does not hide the owner's started issue with a handoff.
        for shown in ('#70', '#71', 'Bug #31', 'Roadmap #31', '4. #35'):
            self.assertIn(shown, text)
        for hidden in ('#72', '#73', '#32', '#33', '#34'):
            self.assertNotIn(hidden, text)

    def test_a_handoff_counts_only_from_the_owner(self):
        body = ct.handoff('STF-09', 9)
        gh = FakeGitHub(comments={40: [{'body': body, 'user': {'login': 'taitt0600'}}],
                                  41: [{'body': body, 'user': {'login': 'someone'}}, {'body': body, 'user': None}]})
        reader = issue.Reader(gh)
        self.assertTrue(reader.has_handoff(40))
        self.assertFalse(reader.has_handoff(41))

    def test_open_issues_carry_their_author(self):
        node = {'number': 5, 'title': 'x', 'author': {'login': 'someone'}, 'issueType': None,
                'labels': {'nodes': []}, 'issueFieldValues': {'nodes': []}}
        ghost = dict(node, number=6, author=None)
        page = {'repository': {'issues': {'pageInfo': {'hasNextPage': False, 'endCursor': None},
                                          'nodes': [node, ghost]}}}
        issues = issue.Reader(FakeGitHub(graphql={'issues(states:OPEN': page})).open_issues()
        self.assertEqual([(i['number'], i['author']) for i in issues], [(5, 'someone'), (6, None)])

    def test_a_roadmap_item_is_judged_by_the_screens_it_names(self):
        owner = 'taitt0600'
        open_issues = [{'number': 42, 'title': 'AUT-01 web', 'author': owner, 'type': 'Feature',
                        'fields': {'Feature ID': 'AUT-01'}, 'screens': ['SCR-AUT-01']}]
        seen = []

        class Fake:
            def pulls(self):
                return []

            def open_issues(self):
                return open_issues

            def roadmap(self):
                return [42]

            def open_blockers(self, number):
                return []

        text = '\n'.join(issue.next_work(Fake(), ready=lambda fid, screens: seen.append((fid, screens)) or True))
        self.assertEqual(seen, [('AUT-01', ['SCR-AUT-01'])])
        self.assertIn('AUT-01 web: ready', text)

        class LayoutOpen(Fake):
            def pulls(self):
                return [{'number': 80, 'title': 'docs: change the layout of SCR-AUT-01', 'draft': False, 'body': '',
                         'user': {'login': owner}},
                        {'number': 81, 'title': 'docs: change the layout of SCR-AUT-01', 'draft': False, 'body': '',
                         'user': {'login': 'someone'}}]

        text = '\n'.join(issue.next_work(LayoutOpen(), ready=lambda fid, screens: True))
        self.assertIn('AUT-01 web: not ready', text)  # its new Layout is still under review (DEC-155 (1))

        class StrangerLayout(Fake):
            def pulls(self):
                return [{'number': 81, 'title': 'docs: change the layout of SCR-AUT-01', 'draft': False, 'body': '',
                         'user': {'login': 'someone'}}]

        text = '\n'.join(issue.next_work(StrangerLayout(), ready=lambda fid, screens: True))
        self.assertIn('AUT-01 web: ready', text)  # a pull request of another account is data (DEC-152 (6))

    def test_open_issues_carry_the_screens_of_their_body(self):
        node = {'number': 5, 'title': 'AUT-01 web', 'author': {'login': 'taitt0600'}, 'issueType': None,
                'body': '### Source\n\nx\n\n### Screens\n\nSCR-AUT-01, SCR-ADM-01\n\n### Notes\n\nSCR-STF-02',
                'labels': {'nodes': []}, 'issueFieldValues': {'nodes': []}}
        empty = dict(node, number=6, body='### Screens\n\n_No response_\n')
        page = {'repository': {'issues': {'pageInfo': {'hasNextPage': False, 'endCursor': None},
                                          'nodes': [node, empty, dict(node, number=7, body=None)]}}}
        issues = issue.Reader(FakeGitHub(graphql={'issues(states:OPEN': page})).open_issues()
        self.assertEqual([i['screens'] for i in issues], [['SCR-ADM-01', 'SCR-AUT-01'], [], []])

    def test_unreadable_github_says_so(self):
        class Broken:
            def pulls(self):
                raise GitHubError('HTTP 401')

        lines = issue.next_work(Broken(), ready=lambda fid, screens: True)
        self.assertIn('could not be read', lines[0])


class ScreensReadyTests(unittest.TestCase):
    """DEC-148 (6) and DEC-155 (1): the screens an issue names, else every screen of its feature."""
    HEADER = '| ID | Screen | Route | Feature | Permission | Blocked by |\n|---|---|---|---|---|---|\n'

    def folder(self, rows, layouts):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        folder = Path(tmp.name)
        lines = ''.join(f'| [{sid}]({sid}.md) | x | `/x` | {fid} | staff | {blocked} |\n' for sid, fid, blocked in rows)
        (folder / 'README.md').write_text(f'# Screens\n\n## Screen list\n\n### Admin\n\n{self.HEADER}{lines}',
                                          encoding='utf-8')
        for sid, _, _ in rows:
            text = f'# {sid}\n\n' + ('## Layout\n\nx\n' if sid in layouts else '')
            (folder / f'{sid}.md').write_text(text, encoding='utf-8')
        return folder

    def test_without_named_screens_every_screen_of_the_feature_counts(self):
        folder = self.folder([('SCR-STF-02', 'STF-09', ''), ('SCR-STF-03', 'STF-09', 'gap 4')],
                             {'SCR-STF-02', 'SCR-STF-03'})
        self.assertFalse(issue.screens_ready('STF-09', folder=folder))
        self.assertTrue(issue.screens_ready('STF-13', folder=folder))

    def test_named_screens_alone_count(self):
        folder = self.folder([('SCR-STF-02', 'STF-09', ''), ('SCR-STF-03', 'STF-09', 'gap 4')],
                             {'SCR-STF-02', 'SCR-STF-03'})
        self.assertTrue(issue.screens_ready('STF-09', ['SCR-STF-02'], folder=folder))
        self.assertFalse(issue.screens_ready('STF-09', ['SCR-STF-02', 'SCR-STF-03'], folder=folder))

    def test_a_named_screen_web_has_already_needs_its_new_layout_and_an_empty_cell(self):
        folder = self.folder([('SCR-AUT-01', 'AUT-01', 'gap 15'), ('SCR-AUT-02', 'AUT-01', '—')], {'SCR-AUT-02'})
        self.assertFalse(issue.screens_ready('AUT-01', ['SCR-AUT-01'], folder=folder))
        self.assertFalse(issue.screens_ready('AUT-01', ['SCR-AUT-02', 'SCR-AUT-09'], folder=folder))
        self.assertTrue(issue.screens_ready('AUT-01', ['SCR-AUT-02'], folder=folder))


if __name__ == '__main__':
    unittest.main()
