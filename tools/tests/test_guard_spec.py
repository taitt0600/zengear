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
    ({'command': 'python3 tools/pr_gates.py check origin/main HEAD out/event.json'}, 'allow', 'allow'),
    ({'command': 'PR_GATES_SCRIPT=/tmp/gates.py python3 -m unittest tools/tests/test_pr_gates.py'}, 'allow', 'allow'),
    ({'command': 'python -m unittest discover -s tools/tests -v'}, 'allow', 'allow'),
    # Running the tools of the workflow is not a write (DEC-148, DEC-149): no prompt inside a one-command run.
    ({'command': 'python tools/issue.py render task --field source=x > out/body.md'}, 'allow', 'allow'),
    ({'command': 'out/.venv/bin/python tools/issue.py render task --field source=x'}, 'allow', 'allow'),
    ({'command': 'python tools/issue.py start 12'}, 'allow', 'allow'),
    ({'command': 'python3 tools/issue.py link 12 7'}, 'allow', 'allow'),
    ({'command': 'python tools/branch_name.py make --number 12 --type Feature --feature STF-09'}, 'allow', 'allow'),
    ({'command': 'python tools/comment_templates.py code-review --sha abc --verdict pass > out/review.md'}, 'allow', 'allow'),
    ({'command': 'python tools/github_pr.py template --closes 3 > out/pr-body.md'}, 'allow', 'allow'),
    ({'command': 'pwsh -NoProfile -File tools/git_tidy.ps1 -Apply'}, 'allow', 'allow'),
    ({'command': 'echo x > out/tools/a.txt'}, 'allow', 'allow'),
    ({'command': 'cd api && cp x ../tools/a.py'}, 'deny', 'ask'),
    ({'command': 'cp x ./tools/issue.py'}, 'deny', 'ask'),
    ({'command': 'X=$(cp a .claude/settings.json) python tools/issue.py render task'}, 'deny', 'ask'),
    ({'command': 'X=$(cp${IFS}a${IFS}.claude/settings.json) python tools/issue.py render task'}, 'deny', 'ask'),
    ({'command': 'cmd //c mklink docs\\x.md ..\\.claude\\settings.json'}, 'deny', 'ask'),
    ({'command': 'PYTHONPATH=tools/evil python tools/issue.py render task'}, 'deny', 'ask'),
    ({'command': 'LD_PRELOAD=tools/x.so python tools/issue.py render task'}, 'deny', 'ask'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx out/$(cp${IFS}a${IFS}.claude/x)'}, 'deny', 'ask'),
    ({'command': 'python tools/feature_context.py STF-09 --out out/$(cp${IFS}a${IFS}tools/x)'}, 'deny', 'ask'),
    ({'command': 'ln -s ../.claude/settings.json docs/x.md'}, 'deny', 'ask'),
    ({'command': 'cp x tools/export_blueprint.py'}, 'deny', 'ask'),
    ({'command': 'cp x tools/blueprint_apply.py'}, 'deny', 'ask'),
    ({'command': 'echo x > $CLAUDE_PROJECT_DIR/tools/requirements.txt'}, 'deny', 'ask'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx /tmp/export'}, 'allow', 'allow'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx ./docs/blueprint'}, 'allow', 'ask'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx /tmp/x > tools/a.py'}, 'deny', 'ask'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx out/export'}, 'allow', 'allow'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx .claude'}, 'deny', 'ask'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx tools/x'}, 'deny', 'ask'),
    ({'command': 'python tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx out/../.claude'}, 'deny', 'ask'),
    # ... but their other arguments are judged.
    ({'command': 'python tools/issue.py render task > .claude/x.md'}, 'deny', 'ask'),
    ({'command': 'python tools/feature_context.py STF-09 --out tools/pr_gates.py'}, 'deny', 'ask'),
    ({'command': 'python tools/feature_context.py STF-09 --root .claude --out out/x.md'}, 'deny', 'ask'),
    # Level A (the specification): allowed unattended, asked locally (DEC-149 (1)).
    ({'command': 'echo x > docs/blueprint/features.md'}, 'allow', 'ask'),
    ({'command': "sed -i 's/a/b/' docs/DATABASE.md"}, 'allow', 'ask'),
    ({'command': "cd docs/blueprint && sed -i 's/a/b/' features.md"}, 'allow', 'ask'),
    ({'command': "cd docs && sed -i 's/a/b/' blueprint/features.md"}, 'allow', 'ask'),
    ({'command': "cd docs && sed -i 's/a/b/' 'blueprint/features.md'"}, 'allow', 'ask'),
    ({'command': 'for f in a b; do cp $f api/db/; done'}, 'allow', 'ask'),
    ({'command': '(cd api && rm db/zengear_02_schema_phase1_store.sql)'}, 'allow', 'ask'),
    ({'command': 'Set-Content -Path docs/blueprint/x.md -Value 1'}, 'allow', 'ask'),
    # Level B (what runs code or judges changes): denied unattended, asked locally (DEC-149 (2)).
    ({'command': 'cat foo | tee .claude/settings.json'}, 'deny', 'ask'),
    ({'command': 'python3 -c "open(\'.claude/settings.json\', \'w\')"'}, 'deny', 'ask'),
    ({'command': 'echo {} > .mcp.json'}, 'deny', 'ask'),
    ({'command': 'git checkout -- .github/workflows/pipeline.yml'}, 'deny', 'ask'),
    ({'command': 'cp x .github/ISSUE_TEMPLATE/task.yml'}, 'deny', 'ask'),
    ({'command': 'cp x .cz.toml'}, 'deny', 'ask'),
    ({'command': 'rm docs/ARCHITECTURE.md'}, 'deny', 'ask'),
    ({'command': 'cp /tmp/a.py tools/pr_gates.py'}, 'deny', 'ask'),
    ({'command': 'cp /tmp/a.py tools/tests/test_pr_gates.py'}, 'deny', 'ask'),
    # A level B target in any part denies the command unattended, before a level A target could allow it.
    ({'command': 'cp a docs/blueprint/x && cp b .claude/settings.json'}, 'deny', 'ask'),
    # blueprint_apply.py writes the workbook without naming it (level A); its other arguments are still judged.
    ({'command': 'python tools/blueprint_apply.py out/dec-105.patch.json'}, 'allow', 'ask'),
    ({'command': 'out/.venv/bin/python tools/blueprint_apply.py out/dec-105.patch.json'}, 'allow', 'ask'),
    ({'command': 'python3 tools/blueprint_apply.py p.json --workbook /tmp/w.xlsx'}, 'allow', 'ask'),
    ({'command': 'python tools/blueprint_apply.py p.json --out .claude/x'}, 'deny', 'ask'),
    ({'command': 'python tools/blueprint_apply.py p.json --workbook tools/x.xlsx'}, 'deny', 'ask'),
    ({'command': 'cd tools && python blueprint_apply.py p.json'}, 'deny', 'ask'),
    ({'command': 'python -m tools.blueprint_apply out/dec-105.patch.json'}, 'allow', 'ask'),
    ({'command': 'py tools/blueprint_apply.py out/dec-105.patch.json'}, 'allow', 'ask'),
    ({'command': 'py tools/export_blueprint.py docs/product-domain-blueprint-MVP.xlsx docs/blueprint'}, 'allow', 'ask'),
    ({'command': 'ls tools/*.py docs/blueprint'}, 'allow', 'allow'),
    ({'command': 'cat tools/blueprint_apply.py'}, 'allow', 'allow'),
    ({'command': 'python3 -m unittest tools/tests/test_blueprint_apply.py'}, 'allow', 'allow'),
    # A build write in one part must not hide a spec write in a later part.
    ({'command': 'echo x > api/src/A.csproj; echo y > docs/blueprint/f.md'}, 'allow', 'ask'),
    # Build files: left to the CI gates when unattended, asked when attended.
    ({'command': 'echo x > api/src/ZenGear.Api/ZenGear.Api.csproj'}, 'allow', 'ask'),
    ({'command': 'dotnet add api/src/ZenGear.Api package Foo'}, 'allow', 'ask'),
    # The generated client folder (DEC-122 (4)): edited by hand it is asked, its generator and reads run;
    # web/src/components/ui/ (DEC-116 (1)) and the mutator web/src/lib/api/api.ts (DEC-122 (4)) are code of the app.
    ({'command': 'cp /tmp/b.tsx web/src/components/ui/button.tsx'}, 'allow', 'allow'),
    ({'command': 'cd web && rm -r src/lib/api/generated'}, 'deny', 'ask'),
    ({'command': 'cd web/src/components/ui && sed -i s/a/b/ button.tsx'}, 'allow', 'allow'),
    ({'command': 'cd web/src && sed -i s/a/b/ components/ui/sheet.tsx lib/api/generated/me/me.ts'}, 'deny', 'ask'),
    ({'command': 'git checkout -- web/src/lib/api/generated/model/brandDto.ts'}, 'deny', 'ask'),
    ({'command': 'cat web/src/lib/api/generated/me/me.ts | head'}, 'allow', 'allow'),
    ({'command': 'cd web/src/lib/api && sed -i s/a/b/ api.ts client.ts'}, 'allow', 'allow'),
    ({'command': 'Remove-Item -Recurse web\\src\\lib\\api\\generated'}, 'deny', 'ask'),
    ({'command': 'Set-Location web; Remove-Item src\\lib\\api\\generated\\me\\me.ts'}, 'deny', 'ask'),
    ({'command': 'cd web && npm run gen:api'}, 'allow', 'allow'),
    ({'command': 'cd web && npm run gen:api > ../out/logs/step2-web-gen-api.log 2>&1'}, 'allow', 'allow'),
    ({'command': 'cd web && npm exec --no -- shadcn add button'}, 'allow', 'allow'),
    # The message-key check of web-test judges changes too; running it is not a write.
    ({'command': 'python tools/message_keys.py'}, 'allow', 'allow'),
    ({'command': 'python3 tools/message_keys.py web/messages'}, 'allow', 'allow'),
    ({'command': 'cp /tmp/x.py tools/message_keys.py'}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'tools' / 'message_keys.py')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'tools' / 'tests' / 'test_message_keys.py')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'web' / 'src' / 'lib' / 'api' / 'generated' / 'me' / 'me.ts')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'web' / 'src' / 'lib' / 'api' / 'generated' / 'model' / 'brandDto.ts')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'web' / 'src' / 'lib' / 'api' / 'api.ts')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'web' / 'src' / 'components' / 'ui' / 'button.tsx')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'web' / 'src' / 'components' / 'app' / 'page-header.tsx')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'web' / 'src' / 'features' / 'catalog' / 'schema.ts')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'web' / 'approved-packages.json')}, 'allow', 'allow'),
    # File tools.
    ({'file_path': str(REPO / '.mcp.json')}, 'deny', 'ask'),
    ({'file_path': str(REPO / '.claude' / 'launch.json')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'tools' / 'feature_context.py')}, 'deny', 'ask'),
    ({'file_path': str(REPO / '.github' / 'pull_request_template.md')}, 'deny', 'ask'),
    ({'file_path': str(REPO / '.cz.toml')}, 'deny', 'ask'),
    ({'file_path': str(REPO / 'docs' / 'AUTOMATION.md')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'CLAUDE.md')}, 'allow', 'allow'),
    ({'file_path': str(REPO / 'docs' / 'blueprint' / 'features.md')}, 'allow', 'ask'),
    ({'file_path': str(REPO / 'api' / 'src' / '..' / 'db' / 'x.sql')}, 'allow', 'ask'),
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
