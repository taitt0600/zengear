# Stop hook: before Claude Code ends a turn that changed backend code, build the solution and run the
# architecture tests (no Docker, under a minute). The full test suite stays in /finish-feature and CI.
# A guardrail, not a gate: CI is the gate. Each state of the backend is checked once.
# Written for Windows PowerShell 5.1 (powershell.exe), like guard-spec.ps1.

# 'Continue', not 'Stop': in Windows PowerShell 5.1 a native command writing to stderr under
# 2>&1 would otherwise throw, and the hook would fail silently instead of reporting the errors.
$ErrorActionPreference = 'Continue'
# Windows PowerShell 5.1 writes stdout in the console code page; the JSON read by Claude Code is UTF-8.
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$root = [string]$env:CLAUDE_PROJECT_DIR
if (-not $root) { $root = (Get-Location).Path }
Set-Location $root

# dotnet: stdout and stderr together, since build errors go to either.
function Invoke-Dotnet([string[]]$arguments) {
    $global:LASTEXITCODE = 1
    $text = & dotnet @arguments 2>&1 | ForEach-Object { "$_" } | Out-String
    return @{ Text = $text; Passed = ($LASTEXITCODE -eq 0) }
}

# git: stdout only, so a warning such as "LF will be replaced by CRLF" is not taken for a change.
function Read-Git([string[]]$arguments) {
    return (& git @arguments 2>$null | Out-String)
}

function Say([hashtable]$output) {
    $output | ConvertTo-Json -Depth 3 -Compress
    exit 0
}

$paths = @('api/src', 'api/tests', 'api/ZenGear.slnx', 'api/global.json', 'api/Directory.Build.props',
    'api/Directory.Packages.props', 'api/.editorconfig')

# Nothing changed in the backend: nothing to check.
$status = Read-Git (@('status', '--porcelain', '--untracked-files=all', '--') + $paths)
if (-not $status.Trim()) { exit 0 }

# Fingerprint of the backend changes: tracked diffs and the content of new files.
$text = Read-Git (@('diff', 'HEAD', '--') + $paths)
$untracked = (Read-Git (@('ls-files', '--others', '--exclude-standard', '--') + $paths)) -split "`r?`n" | Where-Object { $_ }
foreach ($file in $untracked) {
    if (Test-Path -LiteralPath $file -PathType Leaf) { $text += $file + (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
}
$sha = [Security.Cryptography.SHA256]::Create()
$fingerprint = [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))) -replace '-', ''

$stateDir = Join-Path $root 'api/obj'
New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
$stamp = Join-Path $stateDir 'stop-check.stamp'
$last = if (Test-Path $stamp) { (Get-Content $stamp -Raw).Trim() } else { '' }

# This state was checked already: passed, reported as locked, or failed and reported.
if ($last -eq "pass $fingerprint" -or $last -eq "locked $fingerprint") { exit 0 }
if ($last -eq "fail $fingerprint") {
    if ($payload.stop_hook_active) { Say @{ systemMessage = 'stop-check: the build or the architecture tests still fail; see the errors above.' } }
    exit 0
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Say @{ systemMessage = 'stop-check: not checked, dotnet is not on the PATH of powershell.exe.' }
}

# CopyRetryCount=0: a file locked by a running API fails at once instead of after ten retries per file.
$watch = [Diagnostics.Stopwatch]::StartNew()
$run = Invoke-Dotnet @('build', 'api/ZenGear.slnx', '--nologo', '-v', 'q', '-p:CopyRetryCount=0')
if ($run.Passed) { $run = Invoke-Dotnet @('test', 'api/tests/ZenGear.ArchitectureTests', '--no-build', '--nologo', '-v', 'q') }
$watch.Stop()

$errors = @($run.Text -split "`r?`n" | Where-Object { $_ -match ': error |^\s*Failed ' } | Select-Object -Unique -First 20)
$locked = (-not $run.Passed) -and $errors.Count -gt 0 -and -not ($errors | Where-Object { $_ -notmatch 'MSB30(21|26|27)' })
$result = if ($run.Passed) { 'pass' } elseif ($locked) { 'locked' } else { 'fail' }
Add-Content -Path (Join-Path $stateDir 'stop-check.log') -Value ("{0} {1}s {2}" -f (Get-Date -Format s), [int]$watch.Elapsed.TotalSeconds, $result)

if ($run.Passed) {
    Set-Content -Path $stamp -Value "pass $fingerprint"
    exit 0
}

# Output files locked by a running API or by Visual Studio: nothing in the code to fix.
if ($locked) {
    Set-Content -Path $stamp -Value "locked $fingerprint"
    Say @{ systemMessage = 'stop-check: not checked, the build output is locked (the API or Visual Studio is running).' }
}

Set-Content -Path $stamp -Value "fail $fingerprint"
if (-not $errors) { $errors = @($run.Text -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 20) }
$reason = "dotnet build or ZenGear.ArchitectureTests fails. Fix it before finishing:`n" + ($errors -join "`n")

# The turn already went on once because of a Stop hook: let it end and show the errors to the user only.
if ($payload.stop_hook_active) { Say @{ systemMessage = "stop-check: $reason" } }

Say @{ decision = 'block'; reason = $reason }
