# PreToolUse hook: ask the user before Claude Code changes the specification or a file that runs code.
# The specification changes only with an approved Decision (CLAUDE.md, "Rules for every change").
# Build files, hooks, Claude Code settings and CI workflows run code without a further prompt (at the next
# build, commit, stop or push), so an edit to them is confirmed even when edits are accepted automatically.
# Covers file tools (Edit, Write, MultiEdit, NotebookEdit) and shell commands (Bash, PowerShell).
# A guardrail, not a security boundary: every change is still reviewed in the diff before commit.
$ErrorActionPreference = 'Stop'

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

$specReason = 'is part of the specification. Approve only if an accepted Decision (DEC-xxx) requires this change.'
$codeReason = 'runs code (build, git hook, Claude Code hook or setting, CI workflow). Approve only if you asked for this change.'

function Ask([string]$target, [string]$reason) {
    @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'ask'
            permissionDecisionReason = "$target $reason"
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

$codePrefixes = @('.claude/', '.githooks/', '.github/workflows/')
$codeSuffixes = @('.csproj', '.props', '.targets')

# File tools: compare the path relative to the repository root, with forward slashes.
$path = [string]$payload.tool_input.file_path
if (-not $path) { $path = [string]$payload.tool_input.notebook_path }
if ($path) {
    $relative = $path -replace '\\', '/'
    $root = [string]$env:CLAUDE_PROJECT_DIR
    if ($root) {
        $prefix = ($root -replace '\\', '/').TrimEnd('/') + '/'
        if ($relative.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)) {
            $relative = $relative.Substring($prefix.Length)
        }
    }
    $relative = $relative -replace '^\./', ''
    foreach ($item in $protectedPrefixes) {
        if ($relative.StartsWith($item, [StringComparison]::OrdinalIgnoreCase)) { Ask $relative $specReason }
    }
    foreach ($item in $codePrefixes) {
        if ($relative.StartsWith($item, [StringComparison]::OrdinalIgnoreCase)) { Ask $relative $codeReason }
    }
    foreach ($item in $codeSuffixes) {
        if ($relative.EndsWith($item, [StringComparison]::OrdinalIgnoreCase)) { Ask $relative $codeReason }
    }
    exit 0
}

# Shell commands: ask when the command names a protected file and can write to it.
$command = [string]$payload.tool_input.command
if ($command) {
    $mentionsSpec = '(api[\\/]+db[\\/]|(^|[\s''"=])db[\\/]+zengear_|docs[\\/]+blueprint[\\/]|DATABASE\.md|product-domain-blueprint-MVP\.xlsx)'
    $canWrite = '(>|\btee\b|\bsed\b[^|;&]*\s-i|\bperl\b[^|;&]*\s-i|(^|[;&|(]\s*)(cp|mv|rm|truncate|touch|dd|install|unzip|tar)\b|\bgit\s+(checkout|restore|apply|mv|rm|reset|stash)\b|Out-File|Set-Content|Add-Content|Remove-Item|Move-Item|Copy-Item|New-Item|Rename-Item|\bpython\d*\b|\bnode\b|\bpwsh\b|\bpowershell\b)'
    $mentionsCode = '(\.claude[\\/]|\.githooks[\\/]|\.github[\\/]+workflows|\.(csproj|props|targets)\b)'
    if ($command -match $mentionsSpec) {
        $target = $Matches[0]
        if ($command -match $canWrite) { Ask $target $specReason }
    }
    if ($command -match $mentionsCode) {
        $target = $Matches[0]
        if ($command -match $canWrite) { Ask $target $codeReason }
    }
    # dotnet add/remove/new/sln change project files without naming them.
    if ($command -match '\bdotnet\s+(add|remove|new|sln)\b') { Ask $Matches[0] $codeReason }
}

exit 0
