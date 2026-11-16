# PreToolUse hook: ask the user before Claude Code changes the specification, a file that runs code or a
# check that judges the changes.
# The specification changes only with an approved Decision (CLAUDE.md, "Rules for every change").
# Build files, hooks, Claude Code settings, MCP server configuration (.mcp.json) and CI workflows run code
# without a further prompt (at the next build, commit, session start, stop or push), so an edit to them is
# confirmed even when edits are accepted automatically.
# The checks (PR gates, schema snapshot, architecture tests and docs/ARCHITECTURE.md they read, secret scan
# configuration) are confirmed too, so that the code is not made to pass by changing what judges it.
# The generated web client web/src/lib/api/generated/ (DEC-122 (4)) is confirmed when edited by hand; its
# generator (npm run gen:api) does not name it and runs. web/src/components/ui/ is code of the app (DEC-116 (1)), and
# so is the mutator web/src/lib/api/api.ts beside the generated folder.
# Covers file tools (Edit, Write, MultiEdit, NotebookEdit) and shell commands (Bash, PowerShell).
# Levels of DEC-149: level A (the specification: api/db/, docs/blueprint/, docs/DATABASE.md, the workbook, running
# tools/blueprint_apply.py, and running tools/export_blueprint.py into docs/blueprint) is asked locally and allowed in an unattended cloud session
# (CLAUDE_CODE_REMOTE=true), whose pull request the owner merges; level B (all of .claude/, .github/, .githooks/,
# tools/, .mcp.json, .gitleaks.toml, .cz.toml, docs/ARCHITECTURE.md, the architecture tests) is asked locally and
# denied unattended, whatever else the command does; build files are asked locally and left to the CI gates
# (tools/pr_gates.py) unattended. The rest of docs/ is not asked.
# A guardrail, not a security boundary: the CI gates and branch protection decide what reaches main.
$ErrorActionPreference = 'Stop'
# An error blocks the tool call (exit code 2) instead of letting it through unchecked.
trap {
    [Console]::Error.WriteLine("guard-spec: $_")
    exit 2
}

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

$specReason = 'is part of the specification. Approve only if an accepted Decision (DEC-xxx) requires this change.'
$codeReason = 'runs code or judges changes (build, git hook, Claude Code hook or setting, MCP server, CI workflow, PR gate, architecture test, secret scan). Approve only if you asked for this change.'
$generatedReason = 'is written only by its generator (npm run gen:api), never by hand (DEC-122 (4)). Approve only if you asked for this change.'
$unattendedReason = 'cannot change in an unattended cloud session. Stop and report it as a "Needs decision" item instead.'
$unattended = $env:CLAUDE_CODE_REMOTE -eq 'true'

# $kind: 'spec' (level A) is allowed when unattended, 'tooling' (level B) is denied there, 'build' is left to the CI
# gates there; all three are asked when attended.
function Ask([string]$target, [string]$reason, [string]$kind) {
    if ($unattended -and $kind -in @('build', 'spec')) { exit 0 }
    $decision = if ($unattended) { 'deny' } else { 'ask' }
    $why = if ($unattended) { "$target $unattendedReason" } else { "$target $reason" }
    @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = $decision
            permissionDecisionReason = $why
        }
    } | ConvertTo-Json -Depth 3 -Compress
    exit 0
}

$protectedPrefixes = @(
    'api/db/',
    'docs/blueprint/',
    'docs/DATABASE.md',
    'docs/product-domain-blueprint-MVP.xlsx'
)

$codePrefixes = @('.claude/', '.githooks/', '.github/', '.gitleaks.toml', '.mcp.json', '.cz.toml', 'tools/',
    'api/tests/ZenGear.ArchitectureTests/', 'docs/ARCHITECTURE.md')
$codeSuffixes = @('.csproj', '.props', '.targets')

# File tools: compare the path relative to the repository root, with forward slashes, after resolving
# "." and ".." so that api/src/../db/x.sql is seen as api/db/x.sql.
$path = [string]$payload.tool_input.file_path
if (-not $path) { $path = [string]$payload.tool_input.notebook_path }
if ($path) {
    $root = [string]$env:CLAUDE_PROJECT_DIR
    if (-not $root) { $root = (Get-Location).Path }
    $root = $root -replace '\\', '/'
    $relative = [IO.Path]::GetFullPath(($path -replace '\\', '/'), $root) -replace '\\', '/'
    $prefix = $root.TrimEnd('/') + '/'
    if ($relative.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
        $relative = $relative.Substring($prefix.Length)
    }
    foreach ($item in $protectedPrefixes) {
        if ($relative.StartsWith($item, [StringComparison]::OrdinalIgnoreCase)) { Ask $relative $specReason 'spec' }
    }
    foreach ($item in $codePrefixes) {
        if ($relative.StartsWith($item, [StringComparison]::OrdinalIgnoreCase)) { Ask $relative $codeReason 'tooling' }
    }
    if ($relative -match '^web/src/lib/api/generated/') { Ask $relative $generatedReason 'tooling' }
    foreach ($item in $codeSuffixes) {
        if ($relative.EndsWith($item, [StringComparison]::OrdinalIgnoreCase)) { Ask $relative $codeReason 'build' }
    }
    exit 0
}

# Shell commands: ask when a simple command names a protected file and can write to it.
# The command is split at ;, &&, ||, | and line breaks and each part is judged on its own, so that a
# read-only command is not taken for a write because another part of it runs pwsh or python, and tool names
# match only whole (test_pr_gates.py is not pr_gates.py). A cd into a
# folder carries over to the parts after it, which may name their files relative to that folder.
# Running a tool of tools/ that writes no protected file is not a write: those invocations, by name and subcommand
# (and feature_context.py only with an --out under out/ and no --root), with python, python3, py or the venv of
# out/.venv, are removed before the command is checked; their other arguments, such as a redirection, are still judged.
$command = [string]$payload.tool_input.command
# An environment prefix is stripped only for the names the tests of tools/ set, with a plain path as value; any other
# (PYTHON*, LD_*, a command substitution) stays and is judged.
$interpreter = '(?:(?:PR_GATES_SCRIPT|GUARD_SPEC_SCRIPT)=[\w./\\:-]+\s+)*(?:python3?|py|out[\\/]+\.venv[\\/]+(?:bin|Scripts)[\\/]+python(?:\.exe)?)'
$runsTool = '(pr_gates\.py\s+(check-local|check|coverage)|github_pr\.py\s+(create|ready|auto-merge|checks|comment|template)|schema_compare\.py|message_keys\.py|issue\.py\s+(render|feature|next|start|create|link|unlink)|branch_name\.py\s+(make|check)|comment_templates\.py\s+(handoff|code-review|blocked-by|unblock|log)|screen_docs\.py|token_usage\.py|structure_metrics\.py)\b'
$command = $command -replace "(^|[;&|]\s*)$interpreter\s+(\.\.[\\/]+)?tools[\\/]+$runsTool", '$1'
$command = $command -replace "(^|[;&|]\s*)$interpreter\s+(\.\.[\\/]+)?tools[\\/]+feature_context\.py\s+[A-Z]{3}-[0-9]{2}(\s+--out\s+out[\\/](?![\w./\\-]*\.\.)[\w./\\-]*)?(\s+--no-issues)?(?=\s*(\z|[;&|]))", '$1'
# An export of the workbook into out/ or /tmp/ (/check-docs, item 5) reads the specification only; any
# other target, or one with "..", stays and is judged.
$command = $command -replace "(^|[;&|]\s*)$interpreter\s+(\.\.[\\/]+)?tools[\\/]+export_blueprint\.py\s+[\w./\\-]+\.xlsx\s+(?:out|/tmp)[\\/](?![\w./\\-]*\.\.)[\w./\\-]*(?=\s*(\z|[;&|]))", '$1'
# Running the two writers of the specification is level A, not a write to tools/: the run becomes "py <name>" (still a
# write, judged by $mentionsSpec with its arguments); a command that writes to these scripts still names tools/.
$command = $command -replace "(^|[;&|]\s*)$interpreter\s+(\.\.[\\/]+)?tools[\\/]+(blueprint_apply|export_blueprint)\.py\b", '$1py $3'
$command = $command -replace "(^|[;&|]\s*)$interpreter\s+-m\s+unittest\s+(discover\s+-s\s+tools[\\/]+tests\b|tools[\\/]+tests[\\/]+[\w./\\-]+)", '$1'
$command = $command -replace '(^|[;&|]\s*)pwsh\s+-NoProfile\s+-File\s+tools[\\/]+(git_tidy|dev_reset|env_doctor|mcp[\\/]+setup)\.ps1\b', '$1'
if ($command) {
    # tools/blueprint_apply.py writes the workbook without naming it on the command line: running it is a write to the
    # specification (level A). tools/export_blueprint.py names its target, so only an export into docs/blueprint asks.
    $mentionsSpec = '(api[\\/]+db[\\/]|(^|[\s''"=])db[\\/]+zengear_|docs[\\/]+blueprint\b|DATABASE\.md|product-domain-blueprint-MVP\.xlsx|(?<![\w-])blueprint_apply\b)'
    $canWrite = '(>(?!>?\s*(&|/dev/null\b|\$null\b))|\btee\b|\bsed\b[^|;&]*\s-i|\bperl\b[^|;&]*\s-i|(^|[;&|(]\s*|\b(do|then|else|xargs|sudo|exec|env)\s+)(cp|mv|rm|ln|truncate|touch|dd|install|unzip|tar|py(?=\s))\b|\bmklink\b|\bgit\s+(checkout|restore|apply|mv|rm|reset|stash)\b|Out-File|Set-Content|Add-Content|Remove-Item|Move-Item|Copy-Item|New-Item|Rename-Item|\bpython\d*\b|\bnode\b|\bpwsh\b|\bpowershell\b)'
    # All of tools/ but the two writers of the specification above, which are level A.
    $mentionsTooling = '(\.(claude|githooks|github)(?=[\\/\s''"]|$)|\.gitleaks\.toml|\.mcp\.json|\.cz\.toml|(?<![\w.-])(?<!(?<![\w.-])out[\\/])tools[\\/]+|(?<![\w.-])(pr_gates\.py|github_pr\.py|schema_snapshot\.sql|schema_compare\.py|message_keys\.py)|ZenGear\.ArchitectureTests[\\/]|ARCHITECTURE\.md)'
    $mentionsGenerated = 'web[\\/]+src[\\/]+lib[\\/]+api[\\/]+generated\b'
    $mentionsBuild = '\.(csproj|props|targets)\b'
    # The first target of each kind; asked in the order spec, tooling, generated, build once every part is judged.
    $found = @{}
    $folder = ''
    foreach ($part in ($command -split '&&|\|\||[;|\r\n]')) {
        $part = $part.Trim()
        if (-not $part) { continue }
        if ($part -match '^[({\s]*(cd|pushd|Set-Location|sl)\s+[''"]?([^''"\s;)]+)') {
            $folder = $Matches[2].TrimEnd('/', '\')
            continue
        }
        if ($part -notmatch $canWrite) { continue }
        $seen = $part
        if ($folder) {
            $seen += ' ' + ((($part -split '\s+') | ForEach-Object { "$folder/" + $_.Trim('''', '"') }) -join ' ')
        }
        if (-not $found.spec -and $seen -match $mentionsSpec) { $found.spec = $Matches[0].Trim() }
        if (-not $found.tooling -and $seen -match $mentionsTooling) { $found.tooling = $Matches[0] }
        if (-not $found.generated -and $seen -match $mentionsGenerated) { $found.generated = $Matches[0] }
        if (-not $found.build -and $seen -match $mentionsBuild) { $found.build = $Matches[0] }
    }
    # dotnet add/remove/new/sln change project files without naming them.
    if (-not $found.build -and $command -match '\bdotnet\s+(add|remove|new|sln)\b') { $found.build = $Matches[0] }
    # Unattended, a level B target denies the whole command before a level A target can allow it.
    if ($unattended) {
        if ($found.tooling) { Ask $found.tooling $codeReason 'tooling' }
        if ($found.generated) { Ask $found.generated $generatedReason 'tooling' }
    }
    if ($found.spec) { Ask $found.spec $specReason 'spec' }
    if ($found.tooling) { Ask $found.tooling $codeReason 'tooling' }
    if ($found.generated) { Ask $found.generated $generatedReason 'tooling' }
    if ($found.build) { Ask $found.build $codeReason 'build' }
}

exit 0
