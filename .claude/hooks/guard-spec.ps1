# PreToolUse hook: ask the user before Claude Code changes the specification.
# The specification changes only with an approved Decision (CLAUDE.md, "Rules for every change").
# Covers file tools (Edit, Write, MultiEdit, NotebookEdit) and shell commands (Bash, PowerShell).
# A guardrail, not a security boundary: every change is still reviewed in the diff before commit.
$ErrorActionPreference = 'Stop'

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json

function Ask([string]$target) {
    @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'ask'
            permissionDecisionReason = "$target is part of the specification. Approve only if an accepted Decision (DEC-xxx) requires this change."
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
        if ($relative.StartsWith($item, [StringComparison]::OrdinalIgnoreCase)) { Ask $relative }
    }
    exit 0
}

# Shell commands: ask when the command names a specification file and can write to it.
$command = [string]$payload.tool_input.command
if ($command) {
    $mentionsSpec = '(api[\\/]+db[\\/]|(^|[\s''"=])db[\\/]+zengear_|docs[\\/]+blueprint[\\/]|DATABASE\.md|product-domain-blueprint-MVP\.xlsx)'
    $canWrite = '(>|\btee\b|\bsed\b[^|;&]*\s-i|\bperl\b[^|;&]*\s-i|(^|[;&|(]\s*)(cp|mv|rm|truncate|touch|dd|install|unzip|tar)\b|\bgit\s+(checkout|restore|apply|mv|rm|reset|stash)\b|Out-File|Set-Content|Add-Content|Remove-Item|Move-Item|Copy-Item|New-Item|Rename-Item|\bpython\d*\b|\bnode\b|\bpwsh\b|\bpowershell\b)'
    if ($command -match $mentionsSpec) {
        $target = $Matches[0]
        if ($command -match $canWrite) { Ask $target }
    }
}

exit 0
