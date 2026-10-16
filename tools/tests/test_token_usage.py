"""Tests of tools/token_usage.py on synthetic Claude Code transcripts.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import contextlib
import io
import json
import os
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))

import token_usage  # noqa: E402  (imported after the path is set)

CANARY = 'CANARY-7f3a9e-never-printed'
BRANCH = 'feat/x'


def usage(fresh_input=0, creation=0, read=0, output=0):
    return {'input_tokens': fresh_input, 'cache_creation_input_tokens': creation,
            'cache_read_input_tokens': read, 'output_tokens': output}


def call(message_id, use, branch=BRANCH, model='claude-opus-5-5', text='hello'):
    return {'type': 'assistant', 'gitBranch': branch, 'timestamp': '2026-10-16T01:02:03Z',
            'message': {'id': message_id, 'model': model, 'role': 'assistant',
                        'content': [{'type': 'text', 'text': text}], 'usage': use}}


def write(root, name, entries):
    path = Path(root, name)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(''.join((e if isinstance(e, str) else json.dumps(e)) + '\n' for e in entries), encoding='utf-8')


def agent(root, session, name, entries, agent_type='code-reviewer', meta=None):
    write(root, f'{session}/subagents/{name}.jsonl', entries)
    if meta is not False:
        text = meta if isinstance(meta, str) else json.dumps({'agentType': agent_type, 'model': 'claude-opus-5-5',
                                                               'description': 'review the diff'})
        Path(root, f'{session}/subagents/{name}.meta.json').write_text(text, encoding='utf-8')


def run(root, *args):
    out, err = io.StringIO(), io.StringIO()
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        code = token_usage.main(['--dir', str(root), *args])
    return code, out.getvalue(), err.getvalue()


def measure(root, **selection):
    return token_usage.measure(Path(root), **selection)


class CountingTest(unittest.TestCase):
    def test_a_message_repeated_on_lines_and_files_is_one_call(self):
        with tempfile.TemporaryDirectory() as root:
            first = call('msg_1', usage(10, 20, 30, 40))
            write(root, 's1.jsonl', [first, first, first])
            write(root, 's2.jsonl', [first, call('msg_2', usage(1, 1, 1, 1))])
            groups, _, _ = measure(root, branch=BRANCH)
        self.assertEqual(groups['main'].calls, 2)
        self.assertEqual(groups['main'].total, 104)

    def test_a_call_whose_lines_grow_counts_its_largest_values(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', usage(1))])
            agent(root, 's1', 'agent-a', [call('a1', usage(2, 100, 50, 3)), call('a1', usage(2, 100, 50, 900)),
                                          call('a1', None)])
            groups, _, _ = measure(root, branch=BRANCH)
        self.assertEqual((groups['agent'].calls, groups['agent'].output, groups['agent'].total), (1, 900, 1052))
        self.assertEqual(groups['agent'].no_usage, 0)

    def test_subagents_are_counted_apart_and_by_agent_type(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', usage(100, 0, 0, 10))])
            agent(root, 's1', 'agent-a', [call('a1', usage(1, 2, 3, 4)), call('a2', usage(1, 0, 0, 1))])
            agent(root, 's1', 'agent-b', [call('b1', usage(0, 0, 10, 5), model='claude-sonnet-5-5')])
            agent(root, 's1', 'agent-c', [call('c1', usage(0, 0, 0, 7))], agent_type='plan-reviewer')
            groups, types, _ = measure(root, branch=BRANCH)
        self.assertEqual((groups['main'].calls, groups['main'].total), (1, 110))
        self.assertEqual((groups['agent'].calls, groups['agent'].total), (4, 34))
        reviewer = types['code-reviewer']
        self.assertEqual((reviewer.runs, reviewer.calls, reviewer.total, reviewer.output), (2, 3, 27, 10))
        self.assertEqual(reviewer.models, {'claude-opus-5-5', 'claude-sonnet-5-5'})
        self.assertEqual((types['plan-reviewer'].runs, types['plan-reviewer'].total), (1, 7))

    def test_a_missing_or_malformed_meta_gives_type_question_mark(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', usage(1))])
            agent(root, 's1', 'agent-a', [call('a1', usage(1))], meta=False)
            agent(root, 's1', 'agent-b', [call('b1', usage(1))], meta='{not json')
            agent(root, 's1', 'agent-c', [call('c1', usage(1))], meta='["a list"]')
            _, types, _ = measure(root, branch=BRANCH)
        self.assertEqual(list(types), ['?'])
        self.assertEqual(types['?'].runs, 3)

    def test_malformed_lines_are_skipped_and_counted(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', ['{"type": "assistant", "message": {"id"', '[1, 2]', '',
                                     {'type': 'assistant', 'gitBranch': BRANCH, 'message': 'text'},
                                     call('m1', usage(5, 0, 0, 5))])
            groups, _, stats = measure(root, branch=BRANCH)
        self.assertEqual(groups['main'].calls, 1)
        self.assertEqual(stats['skipped'], 3)

    def test_a_call_without_usage_counts_with_no_tokens_and_synthetic_lines_are_not_calls(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', None), call('m2', {'input_tokens': 'many', 'output_tokens': 3}),
                                     call('m3', usage(), model='<synthetic>')])
            groups, _, _ = measure(root, branch=BRANCH)
        self.assertEqual((groups['main'].calls, groups['main'].total, groups['main'].no_usage), (2, 3, 1))
        self.assertEqual(groups['main'].models, {'claude-opus-5-5'})

    def test_branch_keeps_only_its_lines(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m0', usage(1000), branch='main'), call('m1', usage(1)),
                                     call('m2', usage(100), branch='fix/y')])
            agent(root, 's1', 'agent-a', [call('a1', usage(7)), call('a2', usage(70), branch='fix/y')])
            groups, _, _ = measure(root, branch=BRANCH)
        self.assertEqual((groups['main'].total, groups['agent'].total), (1, 7))

    def test_session_keeps_its_file_and_its_subagents(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 'abc1.jsonl', [call('m1', usage(1))])
            write(root, 'def2.jsonl', [call('m2', usage(10))])
            agent(root, 'abc1', 'agent-a', [call('a1', usage(5))])
            agent(root, 'def2', 'agent-b', [call('b1', usage(50))])
            groups, _, _ = measure(root, session='abc')
        self.assertEqual((groups['main'].total, groups['agent'].total), (1, 5))

    def test_an_ambiguous_session_prefix_exits_1_with_the_matching_ids(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 'abc1.jsonl', [call('m1', usage(1))])
            write(root, 'abc2.jsonl', [call('m2', usage(1))])
            code, out, err = run(root, '--session', 'abc')
        self.assertEqual(code, 1)
        self.assertIn('abc1', err)
        self.assertIn('abc2', err)

    def test_an_empty_session_is_not_every_session(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 'abc1.jsonl', [call('m1', usage(1))])
            write(root, 'abc2.jsonl', [call('m2', usage(1))])
            code, _, _ = run(root, '--session', '')
        self.assertEqual(code, 1)


class OutputTest(unittest.TestCase):
    def test_line_gives_total_main_agent_output_fresh_calls_and_peak(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', usage(10, 20, 300, 40)), call('m2', usage(1, 2, 1000, 3))])
            agent(root, 's1', 'agent-a', [call('a1', usage(5, 6, 7, 8))])
            code, out, err = run(root, '--branch', BRANCH, '--line')
        self.assertEqual(code, 0, err)
        self.assertEqual(out.strip(), 'Token: 1402 (chính 1376 / agent 26; output 51, ghi mới 44), 3 lần gọi, '
                                      'context lớn nhất 1003')

    def test_table_names_the_groups_and_the_agent_types(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', usage(1, 2, 3, 4))])
            agent(root, 's1', 'agent-a', [call('a1', usage(1))], agent_type='plan-reviewer')
            code, out, _ = run(root, '--branch', BRANCH)
        self.assertEqual(code, 0)
        for word in ('main', 'agent', 'total', 'plan-reviewer', 'claude-opus-5-5'):
            self.assertIn(word, out)

    def test_a_missing_folder_exits_2_without_a_traceback(self):
        with tempfile.TemporaryDirectory() as root:
            code, out, err = run(Path(root, 'absent'), '--branch', BRANCH)
        self.assertEqual(code, 2)
        self.assertNotIn('Traceback', err)
        self.assertIn('absent', err)

    def test_no_call_on_the_branch_exits_1(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', usage(1), branch='main')])
            code, _, err = run(root, '--branch', BRANCH, '--line')
        self.assertEqual(code, 1)
        self.assertIn(BRANCH, err)

    def test_the_text_of_a_transcript_never_reaches_the_output(self):
        with tempfile.TemporaryDirectory() as root:
            tool = {'type': 'assistant', 'gitBranch': BRANCH,
                    'message': {'id': 'm2', 'model': 'claude-opus-5-5', 'usage': usage(1),
                                'content': [{'type': 'tool_use', 'name': 'Bash', 'input': {'command': CANARY}}]}}
            result = {'type': 'user', 'gitBranch': BRANCH, 'toolUseResult': CANARY,
                      'message': {'role': 'user', 'content': [{'type': 'tool_result', 'content': CANARY}]}}
            write(root, 's1.jsonl', [call('m1', usage(1, 2, 3, 4), text=CANARY), tool, result])
            agent(root, 's1', 'agent-a', [call('a1', usage(1), text=CANARY)],
                  meta=json.dumps({'agentType': 'code-reviewer', 'description': CANARY}))
            agent(root, 's1', 'agent-b', [call('b1', usage(1))], meta=json.dumps({'agentType': CANARY + ' text'}))
            printed = [run(root, '--branch', BRANCH), run(root, '--branch', BRANCH, '--line'),
                       run(root, '--session', 's1')]
        for code, out, err in printed:
            self.assertEqual(code, 0, err)
            self.assertNotIn(CANARY, out + err)

    def test_the_line_is_utf8_whatever_the_console_code_page(self):
        with tempfile.TemporaryDirectory() as root:
            write(root, 's1.jsonl', [call('m1', usage(1, 2, 3, 4))])
            env = {**os.environ, 'PYTHONIOENCODING': 'cp1252', 'PYTHONUTF8': '0'}
            done = subprocess.run([sys.executable, str(REPO / 'tools' / 'token_usage.py'), '--dir', root,
                                   '--branch', BRANCH, '--line'], capture_output=True, env=env)
        self.assertEqual(done.returncode, 0, done.stderr)
        self.assertIn('chính 10', done.stdout.decode('utf-8'))


if __name__ == '__main__':
    unittest.main()
