# PreToolUse hook: ask the user before Claude Code changes the specification, a file that runs code or a
# check that judges the changes.
# The specification changes only with an approved Decision (CLAUDE.md, "Rules for every change").
# Build files, hooks, Claude Code settings, MCP server configuration (.mcp.json) and CI workflows run code
# without a further prompt (at the next build, commit, session start, stop or push), so an edit to them is
# confirmed even when edits are accepted automatically.
# The checks (PR gates, schema snapshot, architecture tests and docs/ARCHITECTURE.md they read, secret scan
# configuration) are confirmed too, so that the code is not made to pass by changing what judges it.
# Generated web files (web/src/**/schema.d.ts and web/src/components/ui/, DEC-111 (7)) are confirmed when edited by
# hand; their generators (npm run gen:api, the shadcn CLI, tools/shadcn_lock.py write) do not name them and run.
# Covers file tools (Edit, Write, MultiEdit, NotebookEdit) and shell commands (Bash, PowerShell).
# In an unattended cloud session (CLAUDE_CODE_REMOTE=true) nobody can answer: the specification, the tooling
# and the checks are read-only there, and build files are left to the CI gates (tools/pr_gates.py).
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
$generatedReason = 'is written only by its generator (npm run gen:api for schema.d.ts; the shadcn CLI, then python tools/shadcn_lock.py write, for components/ui), never by hand (DEC-104 (5), DEC-111 (7)). Approve only if you asked for this change.'
$unattendedReason ='cannot change in an unattended cloud session. Stop and report it as a "Needs decision" item instead.'
$unattended = $env:CLAUDE_CODE_REMOTE -eq 'true'

# $kind: 'spec' and 'tooling' are read-only when unattended; 'build' is left to the CI gates there.
function Ask([string]$target, [string]$reason, [string]$kind) {
    if ($unattended -and $kind -eq 'build') { exit 0 }
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

$codePrefixes = @('.claude/', '.githooks/', '.github/workflows/', '.gitleaks.toml', '.mcp.json', 'tools/pr_gates.py',
    'tools/github_pr.py', 'tools/schema_snapshot.sql', 'tools/schema_compare.py', 'tools/shadcn_lock.py',
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
    if ($relative -match '^web/src/(components/ui/|(.*/)?schema\.d\.ts$)') { Ask $relative $generatedReason 'tooling' }
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
# Running the workflow tools is not a write: those invocations are removed before the command is checked. A
# shadcn_lock.py write with a folder argument stays, since it writes a lock into that folder.
$command = [string]$payload.tool_input.command
$command = $command -replace '(^|[;&|]\s*)python3?\s+(\.\.[\\/]+)?tools[\\/]+(pr_gates\.py\s+(check|coverage)|github_pr\.py\s+(create|ready|auto-merge|checks|comment)|schema_compare\.py|shadcn_lock\.py\s+(check|write(?=\s*($|[;&|\r\n]))))\b', '$1'
if ($command) {
    # tools/blueprint_apply.py writes the workbook without naming it on the command line: running it is a write to
    # the specification, which the owner does in their own terminal.
    $mentionsSpec = '(api[\\/]+db[\\/]|(^|[\s''"=])db[\\/]+zengear_|docs[\\/]+blueprint[\\/]|DATABASE\.md|product-domain-blueprint-MVP\.xlsx|(?<![\w-])blueprint_apply\b)'
    $canWrite = '(>(?!>?\s*(&|/dev/null\b|\$null\b))|\btee\b|\bsed\b[^|;&]*\s-i|\bperl\b[^|;&]*\s-i|(^|[;&|(]\s*|\b(do|then|else|xargs|sudo|exec|env)\s+)(cp|mv|rm|truncate|touch|dd|install|unzip|tar|py(?=\s))\b|\bgit\s+(checkout|restore|apply|mv|rm|reset|stash)\b|Out-File|Set-Content|Add-Content|Remove-Item|Move-Item|Copy-Item|New-Item|Rename-Item|\bpython\d*\b|\bnode\b|\bpwsh\b|\bpowershell\b)'
    $mentionsTooling = '(\.claude[\\/]|\.githooks[\\/]|\.github[\\/]+workflows|\.gitleaks\.toml|\.mcp\.json|(?<![\w.-])(pr_gates\.py|github_pr\.py|schema_snapshot\.sql|schema_compare\.py|shadcn_lock\.py)|ZenGear\.ArchitectureTests[\\/]|ARCHITECTURE\.md)'
    $mentionsGenerated = 'web[\\/]+src[\\/]+(components[\\/]+ui([\\/]|\b)|\S*schema\.d\.ts)'
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
    if ($found.spec) { Ask $found.spec $specReason 'spec' }
    if ($found.tooling) { Ask $found.tooling $codeReason 'tooling' }
    if ($found.generated) { Ask $found.generated $generatedReason 'tooling' }
    if ($found.build) { Ask $found.build $codeReason 'build' }
}

exit 0
