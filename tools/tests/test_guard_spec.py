"""Tests of .claude/hooks/guard-spec.ps1: read-only commands pass, writes to protected files are stopped.

Each case gives the decision expected in an unattended cloud session and in an attended local session:
'allow' (no output), 'deny' or 'ask'. Skipped when pwsh is missing.
Run from the repository root: python3 -m unittest discover -s tools/tests -v
"""
import json
import os
import shutil
import subprocess
import unittest
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
HOOK = Path(os.environ.get('GUARD_SPEC_SCRIPT', REPO / '.claude' / 'hooks' / 'guard-spec.ps1'))

CASES = [
    # Read-only commands: another part of the command runs pwsh or python, or 2> is not a write.
    ({'command': 'for c in pwsh dotnet; do echo $c; done; cat .claude/settings.json'}, 'allow', 'allow'),
    ({'command': 'wc -c docs/blueprint/*.md | sort -n; pwsh -NoProfile -Command "exit 0"'}, 'allow', 'allow'),
    ({'command': 'pwsh -NoProfile -Command "exit 0"; grep -n token tools/github_pr.py'}, 'allow', 'allow'),
    ({'command': 'f=/root/.claude/x.txt; grep -n -i "powershell\\|plugin" $f'}, 'allow', 'allow'),
    ({'command': 'python3 tools/feature_context.py AUT-04 --out out/feature-AUT-04.md'}, 'allow', 'allow'),
    ({'command': 'cat docs/DATABASE.md 2>/dev/null | head'}, 'allow', 'allow'),
    ({'command': 'git diff main...HEAD -- docs/blueprint/features.md 2>&1'}, 'allow', 'allow'),
    ({'command': 'python3 tools/pr_gates.py check origin/main HEAD "chore: x" claude'}, 'allow', 'allow'),
    ({'command': 'cp /tmp/a.py tools/tests/test_pr_gates.py'}, 'allow', 'allow'),
    ({'command': 'PR_GATES_SCRIPT=/tmp/gates.py python3 -m unittest tools/tests/test_pr_gates.py'}, 'allow', 'allow'),
    # Writes to the specification or the tooling.
    ({'command': 'echo x > docs/blueprint/features.md'}, 'deny', 'ask'),
    ({'command': "sed -i 's/a/b/' docs/DATABASE.md"}, 'deny', 'ask'),
    ({'command': "cd docs/blueprint && sed -i 's/a/b/' features.md"}, 'deny', 'ask'),
    ({'command': "cd docs && sed -i 's/a/b/' blueprint/features.md"}, 'deny', 'ask'),
    ({'command': "cd docs && sed -i 's/a/b/' 'blueprint/features.md'"}, 'deny', 'ask'),
    ({'command': 'cat foo | tee .claude/settings.json'}, 'deny', 'ask'),
    ({'command': 'python3 -c "open(\'.claude/settings.json\', \'w\')"'}, 'deny', 'ask'),
    ({'command': 'echo {} > .mcp.json'}, 'deny', 'ask'),
    ({'command': 'for f in a b; do cp $f api/db/; done'}, 'deny', 'ask'),
    ({'command': '(cd api && rm db/zengear_02_schema_phase1_store.sql)'}, 'deny', 'ask'),
    ({'command': 'git checkout -- .github/workflows/pipeline.yml'}, 'deny', 'ask'),
    ({'command': 'rm docs/ARCHITECTURE.md'}, 'deny', 'ask'),
    ({'command': 'cp /tmp/a.py tools/pr_gates.py'}, 'deny', 'ask'),
    ({'command': 'Set-Content -Path docs/blueprint/x.md -Value 1'}, 'deny', 'ask'),
    # blueprint_apply.py writes the workbook without naming it; reading it or running its tests is not a write.
    ({'command': 'python tools/blueprint_apply.py out/dec-105.patch.json'}, 'deny', 'ask'),
    ({'command': 'python3 tools/blueprint_apply.py p.json --workbook /tmp/w.xlsx'}, 'deny', 'ask'),
    ({'command': 'cd tools && python blueprint_apply.py p.json'}, 'deny', 'ask'),
    ({'command': 'python -m tools.blueprint_apply out/dec-105.patch.json'}, 'deny', 'ask'),
    ({'command': 'py tools/blueprint_apply.py out/dec-105.patch.json'}, 'deny', 'ask'),
    ({'command': 'py tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx docs/blueprint'}, 'deny', 'ask'),
    ({'command': 'ls tools/*.py docs/blueprint'}, 'allow', 'allow'),
    ({'command': 'cat tools/blueprint_apply.py'}, 'allow', 'allow'),
    ({'command': 'python3 -m unittest tools/tests/test_blueprint_apply.py'}, 'allow', 'allow'),
    # A build write in one part must not hide a spec write in a later part.
    ({'command': 'echo x > api/src/A.csproj; echo y > docs/blueprint/f.md'}, 'deny', 'ask'),
    # Build files: left to the CI gates when unattended, asked when attended.
    ({'command': 'echo x > api/src/ZenGear.Api/ZenGear.Api.csproj'}, 'allow', 'ask'),
    ({'command': 'dotnet add api/src/ZenGear.Api package Foo'}, 'allow', 'ask'),
    # Generated web files (DEC-111 (7)): edited by hand they are asked, their generators and reads run.
    ({'command': 'cp /tmp/b.tsx web/src/components/ui/button.tsx'}, 'deny', 'ask'),
    ({'command': 'cd web && rm src/lib/api/schema.d.ts'}, 'deny', 'ask'),
    ({'command': 'cd web/src/components/ui && sed -i s/a/b/ button.tsx'}, 'deny', 'ask'),
    ({'command': 'echo {} > web/src/components/ui/.shadcn-lock.json'}, 'deny', 'ask'),
    ({'command': 'cat web/src/lib/api/schema.d.ts | head'}, 'allow', 'allow'),
    ({'command': 'cd web && npm run gen:api'}, 'allow', 'allow'),
    ({'command': 'cd web && npm exec --no -- shadcn add button'}, 'allow', 'allow'),
    ({'command': 'python tools/shadcn_lock.py write'}, 'allow', 'allow'),
    ({'command': 'python3 tools/shadcn_lock.py check web/src/components/ui'}, 'allow', 'allow'),
    ({'command': 'npm exec --no -- shadcn add card && python tools/shadcn_lock.py write'}, 'allow', 'allow'),
    # A write with a folder writes a lock there; the script itself is guarded tooling.
    ({'command': 'python tools/shadcn_lock.py write .claude/hooks'}, 'deny', 'ask'),
    ({'command': 'cp /tmp/x.py tools/shadcn_lock.py'}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'tools' / 'shadcn_lock.py')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'web' / 'src' / 'lib' / 'api' / 'schema.d.ts')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'web' / 'src' / 'components' / 'ui' / 'button.tsx')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'web' / 'src' / 'components' / 'ui' / '.shadcn-lock.json')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'web' / 'src' / 'components' / 'app' / 'page-header.tsx')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'web' / 'src' / 'features' / 'catalog' / 'schema.ts')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'web' / 'approved-packages.json')}, 'allow', 'allow'),
    # File tools.
    ({'file_path': str(REPO / '.mcp.json')}, 'deny', 'ask'),
    ({'file_path': str(REPO / '.claude' / 'launch.json')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'tools' / 'feature_context.py')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'api' / 'src' / '..' / 'db' / 'x.sql')}, 'deny', 'ask'),
]


def decide(tool_input, unattended):
    env = {**os.environ, 'CLAUDE_PROJECT_DIR': str(REPO)}
    env.pop('CLAUDE_CODE_REMOTE', None)
    if unattended:
        env['CLAUDE_CODE_REMOTE'] = 'true'
    run = subprocess.run(['pwsh', '-NoProfile', '-File', str(HOOK)], input=json.dumps({'tool_input': tool_input}),
                         capture_output=True, text=True, env=env, cwd=REPO, timeout=60)
    if run.returncode != 0:
        return f'error {run.returncode}: {run.stderr.strip()}'
    if not run.stdout.strip():
        return 'allow'
    return json.loads(run.stdout)['hookSpecificOutput']['permissionDecision']


@unittest.skipUnless(shutil.which('pwsh'), 'pwsh is needed')
class GuardSpecTests(unittest.TestCase):
    def test_decisions(self):
        for tool_input, unattended_decision, attended_decision in CASES:
            label = tool_input.get('command') or tool_input.get('file_path')
            with self.subTest(case=label, session='unattended'):
                self.assertEqual(decide(tool_input, True), unattended_decision)
            with self.subTest(case=label, session='attended'):
                self.assertEqual(decide(tool_input, False), attended_decision)


if __name__ == '__main__':
    unittest.main()
