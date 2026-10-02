"""Tests of tools/feature_context.py: a small fake docs/ tree, then every feature of the real specification.

Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import contextlib
import io
import re
import sys
import tempfile
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(REPO / 'tools'))

import feature_context  # noqa: E402  (imported after the path is set)

HEADERS = {
    'features.md': '| Feature ID | Feature | Rules | Entities | Depends On |',
    'business-rules.md': '| Rule ID | Rule | Source | Tables |',
    'use-cases.md': '| UC ID | Use Case | Feature ID |',
    'decisions.md': '| Decision ID | Topic | Status |',
}


def write_docs(root, rows, technical='', database='', glossary=''):
    blueprint = root / 'docs' / 'blueprint'
    blueprint.mkdir(parents=True)
    for name, header in HEADERS.items():
        separator = '|' + '---|' * (header.count('|') - 1)
        lines = [f'# {name}', '', header, separator, *rows.get(name, [])]
        (blueprint / name).write_text('\n'.join(lines) + '\n', encoding='utf-8')
    (blueprint / 'technical-design.md').write_text(technical, encoding='utf-8')
    (root / 'docs' / 'DATABASE.md').write_text(database, encoding='utf-8')
    (root / 'docs' / 'GLOSSARY.md').write_text(glossary, encoding='utf-8')


class FakeSpecTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        write_docs(self.root, {
            'features.md': ['| POS-01 | Sell at counter | R-INV-01, R-TX-14 | `ProductSku` | — |'],
            'business-rules.md': ['| R-INV-01 | Stock only through the ledger | DEC-010 | inventory_ledger, product_skus |',
                                  '| R-INV-02 | Unrelated rule | DEC-099 | other |'],
            'use-cases.md': ['| UC-010 | Checkout at POS | POS-01, POS-02 |', '| UC-011 | Other | POS-010 |'],
            'decisions.md': ['| DEC-010 | Ledger | Accepted |', '| DEC-011 | Locks | Accepted |',
                             '| DEC-099 | Unrelated | Accepted |'],
        }, technical='## 8\n- R-TX-14: sums under SELECT ... FOR UPDATE (DEC-011)\n- R-TX-140: another rule\n',
            database='### Inventory\n#### inventory_ledger\ncols A\n#### unrelated\ncols B\n'
                     '### Catalog\n#### product_skus\ncols C\n### Other section\ntext D\n',
            glossary='# Glossary\n\n## Entities\n\n| Thuật ngữ | Entity | Table | Area |\n|---|---|---|---|\n'
                     '| Biến thể | `ProductSku` | `product_skus` | Catalog |\n| Khác | `Other` | `other` | Other |\n\n'
                     '## Other\n\n| A | B |\n|---|---|\n| x | y |\n')

    def tearDown(self):
        self.tmp.cleanup()

    def test_collects_only_the_slice_of_the_feature(self):
        out = feature_context.build('POS-01', self.root)
        self.assertIn('### R-INV-01', out)
        self.assertNotIn('R-INV-02', out)
        self.assertIn('R-TX-14: sums under', out)
        self.assertNotIn('R-TX-140', out)  # R-TX-14 must not match R-TX-140
        self.assertIn('### UC-010', out)
        self.assertNotIn('UC-011', out)  # POS-010 is not POS-01
        self.assertIn('### DEC-010', out)
        self.assertIn('### DEC-011', out)  # cited by an implementation rule
        self.assertNotIn('DEC-099', out)
        self.assertIn('cols A', out)
        self.assertIn('cols C', out)
        self.assertNotIn('cols B', out)
        self.assertNotIn('text D', out)  # a section ends at the next heading of level 4 or less
        self.assertIn('| Biến thể | `ProductSku` | `product_skus` | Catalog |', out)
        self.assertNotIn('`Other`', out)

    def test_reports_a_rule_found_nowhere(self):
        (self.root / 'docs' / 'blueprint' / 'technical-design.md').write_text('', encoding='utf-8')
        out = feature_context.build('POS-01', self.root)
        self.assertIn('**Rules not found in business-rules.md nor technical-design.md**: R-TX-14', out)

    def test_is_deterministic(self):
        self.assertEqual(feature_context.build('POS-01', self.root), feature_context.build('POS-01', self.root))

    def test_rejects_a_malformed_feature_id(self):
        for bad in ('pos-01', 'POS-1', '../etc', '', 'POS-01; rm -rf /'):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                feature_context.build(bad, self.root)

    def test_unknown_feature_is_an_error(self):
        with self.assertRaises(LookupError):
            feature_context.build('ZZZ-99', self.root)

    def test_escaped_pipe_stays_inside_its_cell(self):
        self.assertEqual(feature_context.split_row(r'| A | x \| y | B |'), ['A', r'x \| y', 'B'])

    def test_out_writes_only_under_out(self):
        with contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(feature_context.main(['POS-01', '--root', str(self.root), '--out', 'out/f.md']), 0)
        self.assertTrue((self.root / 'out' / 'f.md').read_text(encoding='utf-8').startswith('# Feature dossier POS-01'))
        for bad in ('docs/blueprint/features.md', 'out/../docs/x.md', 'out', '/tmp/f.md'):
            with self.subTest(bad=bad), contextlib.redirect_stderr(io.StringIO()) as err:
                self.assertEqual(feature_context.main(['POS-01', '--root', str(self.root), '--out', bad]), 1)
                self.assertIn('--out must be a file under', err.getvalue())
        self.assertNotIn('Feature dossier', (self.root / 'docs' / 'blueprint' / 'features.md').read_text(encoding='utf-8'))


class RealSpecTests(unittest.TestCase):
    """Every feature of docs/blueprint/features.md gives a dossier that holds each rule it lists."""

    def test_every_feature_has_a_complete_dossier(self):
        features = feature_context.read_table(REPO / 'docs' / 'blueprint' / 'features.md')
        self.assertGreater(len(features), 0)
        for row in features:
            feature_id = row['Feature ID']
            with self.subTest(feature=feature_id):
                out = feature_context.build(feature_id, REPO)
                self.assertNotIn('Rules not found', out)
                for rule_id in set(re.findall(r'\bR-[A-Z]+-\d+\b', row.get('Rules', ''))):
                    self.assertRegex(out, rf'\b{re.escape(rule_id)}\b')


if __name__ == '__main__':
    unittest.main()
