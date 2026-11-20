"""Tests of tools/mutation_shards.py (Task #296): the files StrykerJS mutates come from the mutate list of
web/stryker.config.json, they are spread over ceil(lines / 250) shards (1 to 6) heaviest first, and a glob or a path
the script cannot handle is an error.
Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import io
import json
import os
import re
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))
import mutation_shards  # noqa: E402

# The exclusions the job quality-web applied with grep before Task #296, paths relative to web/.
OLD_GREP = re.compile(r'\.test\.tsx?$|\.fixtures\.tsx?$|^src/test/|^src/app/|^src/components/ui/|^src/lib/api/generated/')
PATHS = [
    'src/features/identity/sign-in/hooks/use-sign-in.ts',
    'src/features/identity/account/components/reauth-dialog.tsx',
    'src/env-schema.ts',
    'src/a/xtestxts.ts',              # '.test.' must match a literal dot only
    'src/a/b.test.ts', 'src/a/b.test.tsx', 'src/a/b.fixtures.tsx',
    'src/test/render.tsx', 'src/app/[locale]/page.tsx', 'src/app/layout.tsx',
    'src/components/ui/button.tsx', 'src/lib/api/generated/zengear.ts',
    'src/components/data-table.tsx', 'src/styles.css', 'src/a/b.d.ts.map', 'e2e/sign-in.spec.ts',
]


def old_selection(path):
    return re.match(r'^src/.*\.tsx?$', path) is not None and not OLD_GREP.search(path)


class SelectorTests(unittest.TestCase):
    def test_the_config_selects_what_the_old_grep_selected(self):
        mutate = json.loads((REPO / 'web' / 'stryker.config.json').read_text(encoding='utf-8'))['mutate']
        mutated = mutation_shards.selector(mutate)
        for path in PATHS:
            with self.subTest(path=path):
                self.assertEqual(mutated(path), old_selection(path))

    def test_globs(self):
        cases = [('src/**/*.ts', 'src/a.ts', True), ('src/**/*.ts', 'src/a/b/c.ts', True),
                 ('src/**/*.ts', 'src/a.tsx', False), ('src/*.ts', 'src/a/b.ts', False),
                 ('src/test/**', 'src/test/a/b.ts', True), ('src/?.ts', 'src/ab.ts', False),
                 ('src/**/*.{ts,tsx}', 'src/a.tsx', True), ('src/a.ts', 'src/aXts', False),
                 # minimatch with dot: false, as Stryker reads the mutate list
                 ('src/**/*.ts', 'src/.a.ts', False), ('src/**/*.ts', 'src/.x/a.ts', False),
                 ('src/test/**', 'src/test/.x/a.ts', False), ('src/*.ts', 'src/a.b.ts', True)]
        for pattern, path, expected in cases:
            with self.subTest(pattern=pattern, path=path):
                self.assertEqual(bool(mutation_shards.glob_regex(pattern).match(path)), expected)

    def test_unsupported_globs_are_refused(self):
        for pattern in ('src/[ab].ts', 'src/!(x).ts', 'src/+(a).ts', 'src/@(a).ts', 'src/{a,{b,c}}.ts', 'src/{a.ts',
                        'src/a}.ts'):
            with self.subTest(pattern=pattern), self.assertRaises(mutation_shards.GlobError):
                mutation_shards.glob_regex(pattern)

    def test_a_positive_glob_after_a_negated_one_is_refused(self):
        # Stryker applies the list in order: 'src/app/keep.ts' would come back, which a set of patterns cannot say.
        with self.assertRaises(mutation_shards.GlobError):
            mutation_shards.selector(['src/**/*.ts', '!src/app/**', 'src/app/keep.ts'])


class PartitionTests(unittest.TestCase):
    def test_no_file_no_shard(self):
        self.assertEqual(mutation_shards.partition({}), [])

    def test_the_count_is_the_ceiling_of_the_lines_clamped(self):
        self.assertEqual(len(mutation_shards.partition({'a': 250})), 1)
        self.assertEqual(len(mutation_shards.partition({'a': 200, 'b': 51})), 2)
        self.assertEqual(len(mutation_shards.partition({f'f{i}': 140 for i in range(30)})), 6)
        self.assertEqual(len(mutation_shards.partition({'a': 900})), 1)   # never more shards than files

    def test_heaviest_first_onto_the_lightest_shard(self):
        shards = mutation_shards.partition({'a': 300, 'b': 200, 'c': 150, 'd': 100})   # 750 lines: 3 shards
        self.assertEqual(shards, [['a'], ['b'], ['c', 'd']])


class MainTests(unittest.TestCase):
    def run_main(self, files, stdin, mutate=None):
        cwd = os.getcwd()
        with tempfile.TemporaryDirectory() as folder:
            os.chdir(folder)
            try:
                Path('web').mkdir()
                config = {'mutate': mutate or ['src/**/*.{ts,tsx}', '!src/**/*.test.{ts,tsx}', '!src/app/**']}
                Path('web/stryker.config.json').write_text(json.dumps(config), encoding='utf-8')
                for path, lines in files.items():
                    Path(path).parent.mkdir(parents=True, exist_ok=True)
                    Path(path).write_text('x\n\n' * lines, encoding='utf-8')
                out = io.StringIO()
                with redirect_stdout(out):
                    code = mutation_shards.main(['--root', 'web'], stdin=io.StringIO(stdin))
            finally:
                os.chdir(cwd)
        return code, out.getvalue()

    def test_the_output_the_workflow_reads(self):
        files = {'web/src/a.ts': 300, 'web/src/b.tsx': 200, 'web/src/a.test.ts': 500, 'web/src/app/page.tsx': 500}
        code, out = self.run_main(files, '\n'.join(list(files) + ['api/src/X.cs', '']))
        self.assertEqual(code, 0, out)
        self.assertEqual(json.loads(out), {'count': 2, 'shards': [0, 1], 'files': ['src/a.ts', 'src/b.tsx']})

    def test_nothing_to_mutate(self):
        code, out = self.run_main({'web/src/a.test.ts': 10}, 'web/src/a.test.ts\nweb/README.md\n')
        self.assertEqual(code, 0, out)
        self.assertEqual(json.loads(out), {'count': 0, 'shards': [], 'files': []})

    def test_a_comma_in_a_path_is_an_error(self):
        code, out = self.run_main({'web/src/a,b.ts': 10}, 'web/src/a,b.ts\n')
        self.assertEqual(code, 2)
        self.assertIn('::error::web/src/a,b.ts', out)

    def test_an_unsupported_glob_of_the_config_is_an_error(self):
        code, out = self.run_main({}, '', mutate=['src/**/*.ts', '!src/[ab]/**'])
        self.assertEqual(code, 2)
        self.assertIn('::error::web/stryker.config.json', out)


if __name__ == '__main__':
    unittest.main()
