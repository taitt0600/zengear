# PostToolUse (Edit|Write|MultiEdit): after Claude edits a .ts or .tsx file under web/, run ESLint on that file only
# (DEC-104 (11)), with the local binary of web/node_modules and without --fix, and send its errors back to Claude.
# Warnings stay with CI; parse errors in the middle of a change are left to the TypeScript LSP, as analyzer-check.ps1
# leaves compiler errors. A max-lines error is reported without blocking, since a file may grow before it is split
# (DEC-152 (7)); CI refuses it. Without the typed rules (ZENGEAR_ESLINT_UNTYPED, read by web/eslint.config.mjs): with
# them one file took 6.3 s against 2.6 s (median of 5, 2026-11-19), and CI runs them all (DEC-152 (7)). Does nothing
# before npm ci in web/, and never blocks on its own failure or ESLint's.
$ErrorActionPreference = 'Stop'
try { $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json } catch { exit 0 }
$file = [string]$payload.tool_input.file_path
if (-not $file -or $file -notmatch '\.tsx?$' -or -not (Test-Path -LiteralPath $file -PathType Leaf)) { exit 0 }

$root = [string]$env:CLAUDE_PROJECT_DIR
if (-not $root) { $root = (Get-Location).Path }
$web = Join-Path $root 'web'
# The file relative to web/; a path that leaves it, or one on another drive (returned rooted), is not under web/.
$relative = [IO.Path]::GetRelativePath($web, (Resolve-Path -LiteralPath $file).Path)
if ([IO.Path]::IsPathRooted($relative) -or $relative -eq '..' -or $relative -match '^\.\.[\\/]') { exit 0 }
$shown = 'web/' + ($relative -replace '\\', '/')   # as Claude names it, from the repository root

$eslint = Join-Path $web ('node_modules/.bin/' + $(if ($IsWindows) { 'eslint.cmd' } else { 'eslint' }))
if (-not (Test-Path -LiteralPath $eslint -PathType Leaf)) { exit 0 }

# --quiet: errors only. --no-warn-ignored: a file the configuration ignores gives no output. ESLint exits 1 when it
# found an error, 2 when its configuration or ESLint itself failed (CI reports that); eslint.org/docs/latest/use/
# command-line-interface, "Exit Codes".
Push-Location $web
$env:ZENGEAR_ESLINT_UNTYPED = '1'
try { $output = & $eslint --quiet --no-warn-ignored --format json $relative 2>$null | Out-String }
catch { exit 0 }
finally { Pop-Location }
if ($LASTEXITCODE -ne 1) { exit 0 }
try { $results = $output | ConvertFrom-Json } catch { exit 0 }

$messages = @(foreach ($result in @($results)) {
        foreach ($message in @($result.messages)) {
            if ($message.severity -eq 2 -and -not $message.fatal) { $message }
        }
    })
$line = { param($message) "{0}:{1}:{2} {3} {4}" -f $shown, $message.line, $message.column, $message.ruleId, $message.message }
$errors = @($messages | Where-Object { $_.ruleId -ne 'max-lines' } | ForEach-Object { & $line $_ }) | Select-Object -First 20
$length = @($messages | Where-Object { $_.ruleId -eq 'max-lines' } | ForEach-Object { & $line $_ })
if ($errors) {
    [Console]::Error.WriteLine("ESLint errors in ${shown}:`n" + (@($errors) + $length -join "`n"))
    exit 2
}
if ($length) {
    # Context for Claude without blocking (Claude Code hooks, PostToolUse, additionalContext).
    $context = "ESLint: ${shown} is longer than max-lines allows; split it before the change is done (CI refuses it):`n" +
        ($length -join "`n")
    @{ hookSpecificOutput = @{ hookEventName = 'PostToolUse'; additionalContext = $context } } |
        ConvertTo-Json -Compress -Depth 3 | Write-Output
}
exit 0
