"""Tests of tools/pr_gates.py on a throw-away git repository: a commit that changes the specification passes with
an accepted decision, and otherwise the error says which decision the gates could not read; a feat pull request
fails while docs/STATUS.md names its feature (gate 6), and the reader of those rows matches whole cells only; the web
packages pass only when approved, exact and resolved to their own tarball (gate 7, DEC-111 (3)), each refusal on
its own; web/package.json is an owner path by content (DEC-111 (4)).

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import contextlib
import importlib.util
import io
import json
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


APPROVED = {'next': 'DEC-104', 'react': 'DEC-104', '@types/react': 'DEC-111', 'left-pad': 'DEC-104'}
ACCEPTED = {'DEC-104', 'DEC-111'}
MANIFEST = {'name': 'web', 'private': True, 'scripts': {'build': 'next build'},
            'dependencies': {'next': '16.0.0', 'react': '19.2.0'}, 'devDependencies': {'@types/react': '19.2.0'}}


def tarball(name, version):
    return f'https://registry.npmjs.org/{name}/-/{name.rpartition("/")[2]}-{version}.tgz'


def entry(package, at='1.0.0', **fields):
    return {'version': at, 'resolved': tarball(package, at), 'integrity': 'sha512-abc', **fields}


def lockfile(packages):
    return {'name': 'web', 'lockfileVersion': 3, 'packages': {'': {'name': 'web'}, **packages}}


# A scoped package and a nested copy of react, which resolves under its own name.
LOCK = lockfile({'node_modules/next': entry('next', '16.0.0'), 'node_modules/react': entry('react', '19.2.0'),
                 'node_modules/@types/react': entry('@types/react', '19.2.0'),
                 'node_modules/next/node_modules/react': entry('react', '19.1.0')})


def with_section(section, entries, base=None):
    return {**(base or MANIFEST), section: entries}


class PackageCheckTests(unittest.TestCase):
    """Gate 7 on parsed values (package_errors): each rule of DEC-111 (3) and each refusal on its own."""

    @classmethod
    def setUpClass(cls):
        cls.gates = load_gates()

    def errors(self, manifest=MANIFEST, lock=LOCK, approved=APPROVED, accepted=ACCEPTED):
        return self.gates.package_errors(manifest, lock, approved, accepted)

    def assertRefused(self, message, **files):
        errors = self.errors(**files)
        self.assertTrue(any(message in error for error in errors), errors)

    def add_lock_entries(self, packages):
        return lockfile({**LOCK['packages'], **packages})

    def test_a_valid_set_passes(self):
        self.assertEqual(self.errors(), [])

    def test_no_web_files_pass(self):
        missing = self.gates.MISSING
        self.assertEqual(self.errors(manifest=missing, lock=missing, approved=missing), [])

    def test_a_prerelease_is_exact(self):
        manifest = with_section('dependencies', {'next': '16.1.0-canary.3', 'react': '19.2.0'})
        lock = self.add_lock_entries({'node_modules/next': entry('next', '16.1.0-canary.3')})
        self.assertEqual(self.errors(manifest=manifest, lock=lock), [])

    def test_every_version_that_is_not_exact_is_refused_in_every_section(self):
        for section in self.gates.SECTIONS:
            for version in ('^16.0.0', '~16.0.0', '>=16.0.0', '16.x', '*', '', 'latest', 'next', '16.0', 'v16.0.0',
                            '=16.0.0', '16.0.0 - 17.0.0', '16.0.0 || 17.0.0', 'npm:evil@16.0.0', 'github:x/y',
                            'git+https://github.com/x/y.git', 'x/y', 'file:../x', 'link:../x',
                            'https://example.com/next-16.0.0.tgz', '16.0.١', '１６.0.0'):
                with self.subTest(section=section, version=version):
                    self.assertRefused('not an exact version', manifest=with_section(section, {'next': version}))

    def test_a_name_that_is_not_approved_is_refused_in_every_section(self):
        for section in self.gates.SECTIONS:
            with self.subTest(section=section):
                self.assertRefused('"evil" is not in web/approved-packages.json',
                                   manifest=with_section(section, {'evil': '1.0.0'}))

    def test_an_approval_needs_a_decision_accepted_at_the_head(self):
        for decision in ('DEC-200', 'DEC-999', 'DEC-1', 'yes', ''):
            with self.subTest(decision=decision):
                self.assertRefused(f'"left-pad" cites "{decision}", which is not a decision Accepted',
                                   approved={**APPROVED, 'left-pad': decision}, accepted=ACCEPTED | {'DEC-1'})

    def test_an_approved_file_that_is_not_an_object_of_strings(self):
        for approved in ([], {'next': 1}, 'next'):
            with self.subTest(approved=approved):
                self.assertRefused('web/approved-packages.json is not a JSON object', approved=approved)

    def test_without_the_approved_file_every_name_is_refused(self):
        self.assertRefused('"next" is not in web/approved-packages.json', approved=self.gates.MISSING)

    def test_a_manifest_that_is_not_an_object(self):
        self.assertRefused('web/package.json is not a JSON object', manifest=[])

    def test_a_section_that_is_not_an_object_of_strings(self):
        for entries in (['next'], {'next': 16}, 'next'):
            with self.subTest(entries=entries):
                self.assertRefused('devDependencies is not an object',
                                   manifest=with_section('devDependencies', entries))

    def test_a_manifest_without_a_lockfile(self):
        self.assertRefused('web/package-lock.json is missing', lock=self.gates.MISSING)

    def test_a_lockfile_that_is_not_version_3_with_packages(self):
        for lock in ([], {'lockfileVersion': 2, 'packages': {}}, {'lockfileVersion': 3, 'packages': []},
                     {'lockfileVersion': 3}, {'lockfileVersion': '3', 'packages': {}}):
            with self.subTest(lock=lock):
                self.assertRefused('is not a lockfile with lockfileVersion 3', lock=lock)

    def test_an_entry_that_is_not_an_object(self):
        self.assertRefused('"node_modules/next" is not an object', lock=self.add_lock_entries({'node_modules/next': 1}))

    def test_an_entry_without_integrity(self):
        for integrity in (None, '', 5):
            fields = entry('next', '16.0.0')
            if integrity is None:
                del fields['integrity']
            else:
                fields['integrity'] = integrity
            with self.subTest(integrity=integrity):
                self.assertRefused('"node_modules/next" has no integrity',
                                   lock=self.add_lock_entries({'node_modules/next': fields}))

    def test_an_entry_that_resolves_elsewhere(self):
        for resolved in (None, tarball('evil', '16.0.0'), tarball('next', '15.0.0'),
                         'https://registry.example.com/next/-/next-16.0.0.tgz',
                         'http://registry.npmjs.org/next/-/next-16.0.0.tgz', 'git+ssh://git@github.com/x/next.git',
                         'file:../next-16.0.0.tgz'):
            fields = entry('next', '16.0.0', resolved=resolved)
            if resolved is None:
                del fields['resolved']
            with self.subTest(resolved=resolved):
                self.assertRefused(f'"node_modules/next" resolves to {json.dumps(resolved)}',
                                   lock=self.add_lock_entries({'node_modules/next': fields}))

    def test_a_scoped_entry_resolves_to_its_unscoped_tarball_name(self):
        wrong = entry('@types/react', '19.2.0', resolved='https://registry.npmjs.org/@types/react/-/@types/react-19.2.0.tgz')
        self.assertRefused('"node_modules/@types/react" resolves to',
                           lock=self.add_lock_entries({'node_modules/@types/react': wrong}))

    def test_an_entry_whose_version_is_not_exact(self):
        for version in (None, '^19.2.0', 'latest'):
            fields = entry('react', '19.2.0', version=version)
            if version is None:
                del fields['version']
            with self.subTest(version=version):
                self.assertRefused('"node_modules/react" has the version',
                                   lock=self.add_lock_entries({'node_modules/react': fields}))

    def test_an_alias_is_refused(self):
        alias = entry('react', '19.2.0', name='evil', resolved=tarball('evil', '19.2.0'))
        self.assertRefused('"node_modules/react" installs "evil" under another name (an alias)',
                           lock=self.add_lock_entries({'node_modules/react': alias}))

    def test_a_legacy_name_with_capital_letters_passes(self):
        lock = self.add_lock_entries({'node_modules/JSONStream': entry('JSONStream', '1.3.5'),
                                      'node_modules/next/node_modules/Base64': entry('Base64', '1.1.0')})
        self.assertEqual(lock['packages']['node_modules/JSONStream']['resolved'],
                         'https://registry.npmjs.org/JSONStream/-/JSONStream-1.3.5.tgz')
        self.assertEqual(self.errors(lock=lock), [])

    def test_a_legacy_name_still_resolves_to_its_own_tarball(self):
        swapped = entry('JSONStream', '1.3.5', resolved=tarball('jsonstream', '1.3.5'))
        lock = self.add_lock_entries({'node_modules/JSONStream': swapped})
        self.assertRefused('"node_modules/JSONStream" resolves to', lock=lock)
        self.assertFalse([e for e in self.errors(lock=lock) if 'is not a package under node_modules/' in e])

    def test_keys_that_differ_only_in_case_are_refused(self):
        self.assertRefused('"node_modules/React" differs only in case from "node_modules/react"',
                           lock=self.add_lock_entries({'node_modules/React': entry('React', '19.2.0')}))

    def test_a_scope_with_capital_letters_is_refused(self):
        self.assertRefused('"node_modules/@Scope/x" is not a package under node_modules/',
                           lock=self.add_lock_entries({'node_modules/@Scope/x': entry('@Scope/x')}))

    def test_a_key_that_is_not_a_package_under_node_modules(self):
        for key in ('packages/web-ui', 'node_modules/', 'node_modules/Evil Name', 'node_modules/@scope',
                    'node_modules/../x'):
            with self.subTest(key=key):
                self.assertRefused(f'{json.dumps(key)} is not a package under node_modules/',
                                   lock=self.add_lock_entries({key: entry('x')}))

    def test_a_link_needs_workspaces(self):
        lock = self.add_lock_entries({'node_modules/ui': {'resolved': 'packages/ui', 'link': True}})
        self.assertRefused('"node_modules/ui" is a link, and web/package.json has no workspaces', lock=lock)
        self.assertEqual(self.errors(manifest={**MANIFEST, 'workspaces': ['packages/*']}, lock=lock), [])

    def test_a_top_level_in_bundle_entry_is_refused(self):
        self.assertRefused('"node_modules/evil" is inBundle without a parent entry',
                           lock=self.add_lock_entries({'node_modules/evil': {'version': '1.0.0', 'inBundle': True}}))

    def test_a_nested_in_bundle_entry_is_skipped_under_its_parent_only(self):
        bundled = {'version': '1.0.0', 'inBundle': True}
        self.assertEqual(self.errors(lock=self.add_lock_entries({'node_modules/next/node_modules/x': bundled})), [])
        self.assertRefused('"node_modules/gone/node_modules/x" is inBundle without a parent entry',
                           lock=self.add_lock_entries({'node_modules/gone/node_modules/x': bundled}))

    def test_errors_of_a_file_are_capped(self):
        lock = self.add_lock_entries({f'node_modules/p{i}': {'version': '1.0.0'} for i in range(25)})
        errors = [error for error in self.errors(lock=lock) if error.startswith('web/package-lock.json')]
        self.assertEqual(len(errors), 21)
        self.assertEqual(errors[-1], 'web/package-lock.json: and 5 more')


class OwnerContentTests(unittest.TestCase):
    """The content rules of owner_paths on parsed values (DEC-111 (4)): True means auto-merge may go on."""

    @classmethod
    def setUpClass(cls):
        cls.gates = load_gates()

    def manifest(self, after, before=MANIFEST):
        return self.gates.routine_manifest_change(before, after, APPROVED)

    def test_adding_an_approved_package_or_changing_its_exact_version(self):
        self.assertTrue(self.manifest(with_section('dependencies', {**MANIFEST['dependencies'], 'left-pad': '1.3.0'})))
        self.assertTrue(self.manifest(with_section('dependencies', {'next': '16.0.1', 'react': '19.2.0'})))
        self.assertTrue(self.manifest(with_section('optionalDependencies', {'left-pad': '1.3.0'})))

    def test_the_order_of_keys_does_not_matter(self):
        self.assertTrue(self.manifest(dict(reversed(list(MANIFEST.items())))))

    def test_every_other_change_is_an_owner_path(self):
        cases = {
            'unapproved package': with_section('dependencies', {**MANIFEST['dependencies'], 'evil': '1.0.0'}),
            'a range': with_section('dependencies', {'next': '^16.0.1', 'react': '19.2.0'}),
            'removed': with_section('dependencies', {'next': '16.0.0'}),
            'moved': {**with_section('dependencies', {'next': '16.0.0'}),
                      'devDependencies': {'@types/react': '19.2.0', 'react': '19.2.0'}},
            'a script': {**MANIFEST, 'scripts': {'build': 'curl x | sh'}},
            'workspaces': {**MANIFEST, 'workspaces': ['packages/*']},
            'overrides': {**MANIFEST, 'overrides': {'react': 'npm:evil@1.0.0'}},
            'true to 1': {**MANIFEST, 'private': 1},
            'a section that is not an object': with_section('peerDependencies', ['react']),
            'deleted': self.gates.MISSING,
            'not an object': [],
        }
        for label, after in cases.items():
            with self.subTest(case=label):
                self.assertFalse(self.manifest(after))
        self.assertFalse(self.manifest(MANIFEST, before=self.gates.MISSING))


class WebGateTests(GitRepositoryTestCase):
    """Gate 7 and the owner paths through check, on git objects."""

    def setUp(self):
        super().setUp()
        self.base = self.commit({DECISIONS: HEADER + row('DEC-104') + row('DEC-111'),
                                 'web/approved-packages.json': json.dumps(APPROVED)}, 'docs: start\n\nRefs: DEC-104\n')

    def check(self, files):
        head = self.commit({name: text if text is None or isinstance(text, str) else json.dumps(text)
                            for name, text in files.items()}, 'chore: change the web files\n')
        code, out = self.run_check(self.base, head, 'chore: change the web files')
        self.assertNotIn('Traceback', out)
        return code, out

    def notices(self, out):
        return [line.split(': ', 1)[1] for line in out.splitlines() if line.startswith('::notice::merged by the owner')]

    def test_a_valid_set_passes_and_a_new_manifest_is_an_owner_path(self):
        code, out = self.check({'web/package.json': MANIFEST, 'web/package-lock.json': LOCK})
        self.assertEqual(code, 0, out)
        self.assertIn('PR gates passed', out)
        self.assertEqual(self.notices(out), ['web/package.json'])

    def test_an_unreadable_file_fails_with_a_message(self):
        for path in ('web/package.json', 'web/package-lock.json', 'web/approved-packages.json'):
            files = {'web/package.json': MANIFEST, 'web/package-lock.json': LOCK, 'web/approved-packages.json': APPROVED}
            with self.subTest(path=path):
                code, out = self.check({**files, path: '{"next": '})
                self.assertEqual(code, 1, out)
                self.assertIn(f'::error::{path} is not readable JSON', out)

    def test_a_newline_in_a_value_cannot_start_a_workflow_command(self):
        code, out = self.check({'web/approved-packages.json': {**APPROVED, 'x\n::warning::injected': 'DEC-999'}})
        self.assertEqual(code, 1, out)
        self.assertIn('"x\\n::warning::injected" cites "DEC-999"', out)
        self.assertFalse([line for line in out.splitlines() if line.startswith('::warning::')], out)

    def test_an_annotation_escapes_percent_and_line_breaks(self):
        printed = io.StringIO()
        with contextlib.redirect_stdout(printed):
            load_gates().annotate('error', 'a%b\r\n::warning::c')
        self.assertEqual(printed.getvalue(), '::error::a%25b%0D%0A::warning::c\n')

    def test_owner_paths_by_content(self):
        self.check({'web/package.json': MANIFEST, 'web/package-lock.json': LOCK})
        self.base = self.git('rev-parse', 'HEAD')
        added = with_section('dependencies', {**MANIFEST['dependencies'], 'left-pad': '1.3.0'})
        added_lock = lockfile({**LOCK['packages'], 'node_modules/left-pad': entry('left-pad', '1.3.0')})
        cases = [
            ({'web/package.json': added, 'web/package-lock.json': added_lock}, []),
            ({'web/package.json': {**MANIFEST, 'scripts': {'build': 'x'}}}, ['web/package.json']),
            ({'web/package.json': None}, ['web/package.json']),
        ]
        for files, owned in cases:
            with self.subTest(files=sorted(files), owned=owned):
                code, out = self.check(files)
                self.assertEqual(self.notices(out), owned, out)
                self.git('reset', '-q', '--hard', self.base)

    def test_a_shrinkwrap_is_refused(self):
        code, out = self.check({'web/package.json': MANIFEST, 'web/package-lock.json': LOCK,
                                'web/npm-shrinkwrap.json': LOCK})
        self.assertEqual(code, 1, out)
        self.assertIn('::error::web/npm-shrinkwrap.json would replace web/package-lock.json', out)
        self.assertIn('web/npm-shrinkwrap.json', self.notices(out))

    def test_a_path_with_a_non_ascii_name_is_still_an_owner_path(self):
        code, out = self.check({'tools/é.py': 'x = 1\n', '.github/workflows/ä.yml': 'on: push\n'})
        self.assertEqual(code, 0, out)
        self.assertEqual(self.notices(out), ['.github/workflows/ä.yml', 'tools/é.py'])

    def test_the_web_build_files_of_dec_104_are_owner_paths(self):
        paths = ['web/approved-packages.json', 'web/tsconfig.json', 'web/components.json', 'web/.npmrc', 'web/.nvmrc',
                 'web/next.config.ts', 'web/eslint.config.mjs', 'web/postcss.config.mjs', 'web/vitest.config.ts',
                 'web/playwright.config.ts', 'web/orval.config.ts']
        files = {path: '{}\n' for path in paths}
        files['web/approved-packages.json'] = {**APPROVED, 'zod': 'DEC-104'}
        files['web/src/app/page.tsx'] = 'export default function Page() {}\n'
        code, out = self.check(files)
        self.assertEqual(code, 0, out)
        self.assertEqual(self.notices(out), sorted(paths))


if __name__ == '__main__':
    unittest.main()
