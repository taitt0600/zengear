"""Every GraphQL query of tools/ as it is sent, with balanced braces (Task #314): the tests of the callers mock
graphql(), so a query GitHub cannot parse (a missing closing brace) passed them. Each call site of .graphql( in the
Python files of tools/ (its folders included, tests/ aside) is found in the source and run against a client that records the query; a new call site without a case
here fails the test.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import ast
import inspect
import sys
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
TOOLS = REPO / 'tools'
sys.path.insert(0, str(TOOLS))
import github_pr  # noqa: E402
import issue  # noqa: E402


class Sent(Exception):
    """Raised by the recording client once it has the query: the callers' handling of an answer is tested elsewhere."""


class RecordingGitHub:
    repo = 'taitt0600/zengear'
    owner = 'taitt0600'
    proxy = False

    def __init__(self):
        self.sent = []

    def issue(self, number):
        return {'number': number, 'node_id': f'I_{number}'}

    def graphql(self, query, variables):
        caller = inspect.stack()[1]
        self.sent.append((Path(caller.filename).resolve().relative_to(TOOLS).as_posix(), caller.positions.lineno,
                          query))
        raise Sent


def call_sites():
    """(path under tools/, line) of every call of a .graphql( method in the Python files of tools/ and its folders,
    the tests aside; the client's own definition is not a call."""
    found = set()
    for path in sorted(TOOLS.rglob('*.py')):
        name = path.relative_to(TOOLS).as_posix()
        if name.startswith('tests/'):
            continue
        for node in ast.walk(ast.parse(path.read_text(encoding='utf-8'), filename=name)):
            if isinstance(node, ast.Call) and isinstance(node.func, ast.Attribute) and node.func.attr == 'graphql':
                found.add((name, node.lineno))
    return found


def balance_error(query):
    """Where the braces of a query do not balance, or None. The queries hold no string literal, so every brace counts."""
    depth = 0
    for index, char in enumerate(query):
        depth += {'{': 1, '}': -1}.get(char, 0)
        if depth < 0:
            return f'a "}}" with no "{{" open at {index}'
    return f'{depth} "{{" not closed' if depth else None


class GraphQLQueryTests(unittest.TestCase):
    def setUp(self):
        self.gh = RecordingGitHub()
        reader = issue.Reader(self.gh)
        pr = {'number': 7, 'node_id': 'PR_7'}
        cases = [
            lambda: reader.fields(12),
            lambda: reader.open_feature_issues('STF-09'),
            lambda: reader.open_issues(),
            lambda: reader.roadmap_issue(),
            lambda: issue.set_field(self.gh, 12, 'Priority', 'High'),
            lambda: github_pr.set_draft(self.gh, pr, True),
            lambda: github_pr.set_draft(self.gh, pr, False),
            lambda: github_pr.set_auto_merge(self.gh, pr, True),
            lambda: github_pr.set_auto_merge(self.gh, pr, False),
        ]
        for case in cases:
            with self.assertRaises(Sent):
                case()

    def test_every_call_site_has_a_case(self):
        self.assertEqual({(name, line) for name, line, _ in self.gh.sent}, call_sites())

    def test_every_query_has_balanced_braces(self):
        for name, line, query in self.gh.sent:
            with self.subTest(site=f'{name}:{line}'):
                self.assertIsNone(balance_error(query), query)

    def test_balance_error_finds_a_missing_and_an_extra_brace(self):
        self.assertIsNone(balance_error('query{a{b}}'))
        self.assertEqual(balance_error('query{a{b}'), '1 "{" not closed')
        self.assertEqual(balance_error('query{a}}'), 'a "}" with no "{" open at 8')


if __name__ == '__main__':
    unittest.main()
