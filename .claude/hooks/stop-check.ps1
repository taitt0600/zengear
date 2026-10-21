# Stop hook: before Claude Code ends a turn that changed backend code, build the solution and run the
# architecture tests (no Docker, under a minute); before it ends a turn that changed web/, run next typegen and
# tsc --noEmit there with the local binaries (DEC-104 (11)). The full test suite stays in /finish-feature and CI.
# A guardrail, not a gate: CI is the gate. Each state of the backend and of the web is checked once.
# Runs on PowerShell 7 (pwsh), on Windows and in cloud sessions, like guard-spec.ps1.

# 'Continue', not 'Stop': a native command writing to stderr under 2>&1 must not end the script,
# which reports the build errors itself.
$ErrorActionPreference = 'Continue'
# On Windows the console writes in its code page; the JSON read by Claude Code is UTF-8.
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false

$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$root = [string]$env:CLAUDE_PROJECT_DIR
if (-not $root) { $root = (Get-Location).Path }
Set-Location $root

# What the two checks found: reasons to go on with the turn (sent to Claude), and notes for the user.
$reasons = [Collections.Generic.List[string]]::new()
$notes = [Collections.Generic.List[string]]::new()

# A tool: stdout and stderr together, since build errors go to either.
function Invoke-Tool([string]$command, [string[]]$arguments) {
    $global:LASTEXITCODE = 1
    $text = & $command @arguments 2>&1 | ForEach-Object { "$_" } | Out-String
    return @{ Text = $text; Passed = ($LASTEXITCODE -eq 0) }
}

# git: stdout only, so a warning such as "LF will be replaced by CRLF" is not taken for a change.
function Read-Git([string[]]$arguments) {
    return (& git @arguments 2>$null | Out-String)
}

# Fingerprint of the changes under $paths: tracked diffs and the content of new files; '' when nothing changed.
function Get-Fingerprint([string[]]$paths) {
    $status = Read-Git (@('status', '--porcelain', '--untracked-files=all', '--') + $paths)
    if (-not $status.Trim()) { return '' }
    $text = Read-Git (@('diff', 'HEAD', '--') + $paths)
    $untracked = (Read-Git (@('ls-files', '--others', '--exclude-standard', '--') + $paths)) -split "`r?`n" | Where-Object { $_ }
    foreach ($file in $untracked) {
        if (Test-Path -LiteralPath $file -PathType Leaf) { $text += $file + (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash }
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))) -replace '-', ''
}

function Read-Stamp([string]$stamp) {
    if (Test-Path $stamp) { return (Get-Content $stamp -Raw).Trim() }
    return ''
}

function Test-Backend {
    $paths = @('api/src', 'api/tests', 'api/ZenGear.slnx', 'api/global.json', 'api/Directory.Build.props',
        'api/Directory.Packages.props', 'api/.editorconfig')
    # Nothing changed in the backend: nothing to check.
    $fingerprint = Get-Fingerprint $paths
    if (-not $fingerprint) { return }

    $stateDir = Join-Path $root 'api/obj'
    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    $stamp = Join-Path $stateDir 'stop-check.stamp'
    $last = Read-Stamp $stamp

    # This state was checked already: passed, reported as locked, or failed and reported.
    if ($last -eq "pass $fingerprint" -or $last -eq "locked $fingerprint") { return }
    if ($last -eq "fail $fingerprint") {
        if ($payload.stop_hook_active) { $notes.Add('stop-check: the build or the architecture tests still fail; see the errors above.') }
        return
    }

    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        $notes.Add('stop-check: not checked, dotnet is not on the PATH of pwsh.')
        return
    }

    # CopyRetryCount=0: a file locked by a running API fails at once instead of after ten retries per file.
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $run = Invoke-Tool 'dotnet' @('build', 'api/ZenGear.slnx', '--nologo', '-v', 'q', '-p:CopyRetryCount=0')
    if ($run.Passed) { $run = Invoke-Tool 'dotnet' @('test', 'api/tests/ZenGear.ArchitectureTests', '--no-build', '--nologo', '-v', 'q') }
    $watch.Stop()

    $errors = @($run.Text -split "`r?`n" | Where-Object { $_ -match ': error |^\s*Failed ' } | Select-Object -Unique -First 20)
    $locked = (-not $run.Passed) -and $errors.Count -gt 0 -and -not ($errors | Where-Object { $_ -notmatch 'MSB30(21|26|27)' })
    $result = if ($run.Passed) { 'pass' } elseif ($locked) { 'locked' } else { 'fail' }
    Add-Content -Path (Join-Path $stateDir 'stop-check.log') -Value ("{0} {1}s {2}" -f (Get-Date -Format s), [int]$watch.Elapsed.TotalSeconds, $result)
    Set-Content -Path $stamp -Value "$result $fingerprint"
    if ($run.Passed) { return }

    # Output files locked by a running API or by Visual Studio: nothing in the code to fix.
    if ($locked) {
        $notes.Add('stop-check: not checked, the build output is locked (the API or Visual Studio is running).')
        return
    }
    if (-not $errors) { $errors = @($run.Text -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 20) }
    $reasons.Add("dotnet build or ZenGear.ArchitectureTests fails. Fix it before finishing:`n" + ($errors -join "`n"))
}

function Test-Web {
    # Before npm ci in web/ (or before the web shell) there is nothing to typecheck with.
    $modules = Join-Path $root 'web/node_modules'
    if (-not (Test-Path -LiteralPath $modules -PathType Container)) { return }
    # What npm, tsc and Next.js write themselves is left out, so that a check does not change the state it checked.
    $paths = @('web', ':(exclude)web/node_modules', ':(exclude)web/.next', ':(exclude)web/next-env.d.ts',
        ':(exclude)web/*.tsbuildinfo')
    $fingerprint = Get-Fingerprint $paths
    if (-not $fingerprint) { return }

    $stateDir = Join-Path $modules '.cache'
    New-Item -ItemType Directory -Force -Path $stateDir | Out-Null
    $stamp = Join-Path $stateDir 'stop-check.stamp'
    $last = Read-Stamp $stamp
    if ($last -eq "pass $fingerprint") { return }
    if ($last -eq "fail $fingerprint") {
        if ($payload.stop_hook_active) { $notes.Add('stop-check: the typecheck of web/ still fails; see the errors above.') }
        return
    }

    $tsc = Join-Path $modules ('.bin/' + $(if ($IsWindows) { 'tsc.cmd' } else { 'tsc' }))
    if (-not (Test-Path -LiteralPath $tsc -PathType Leaf)) {
        $notes.Add('stop-check: web not typechecked, web/node_modules has no tsc (run npm ci in web/).')
        return
    }

    # tsc needs the route types Next.js generates (LayoutProps, PageProps), which a fresh checkout lacks.
    $next = Join-Path $modules ('.bin/' + $(if ($IsWindows) { 'next.cmd' } else { 'next' }))

    $watch = [Diagnostics.Stopwatch]::StartNew()
    Push-Location (Join-Path $root 'web')
    try {
        $typegen = if (Test-Path -LiteralPath $next -PathType Leaf) { Invoke-Tool $next @('typegen') }
        if ($typegen -and -not $typegen.Passed) { $run = $typegen }
        else { $run = Invoke-Tool $tsc @('--noEmit', '--pretty', 'false') }
    } finally { Pop-Location }
    $watch.Stop()
    $result = if ($run.Passed) { 'pass' } else { 'fail' }
    Add-Content -Path (Join-Path $stateDir 'stop-check.log') -Value ("{0} {1}s {2}" -f (Get-Date -Format s), [int]$watch.Elapsed.TotalSeconds, $result)
    Set-Content -Path $stamp -Value "$result $fingerprint"
    if ($run.Passed) { return }

    $errors = @($run.Text -split "`r?`n" | Where-Object { $_ -match ': error TS' } | Select-Object -Unique -First 20)
    if (-not $errors) { $errors = @($run.Text -split "`r?`n" | Where-Object { $_.Trim() } | Select-Object -Last 20) }
    $failed = if ($run -eq $typegen) { 'next typegen' } else { 'tsc --noEmit' }
    $reasons.Add("$failed fails in web/. Fix it before finishing:`n" + ($errors -join "`n"))
}

Test-Backend
Test-Web

$output = [ordered]@{}
if ($reasons.Count) {
    $reason = $reasons -join "`n`n"
    # The turn already went on once because of a Stop hook: let it end and show the errors to the user only.
    if ($payload.stop_hook_active) { $notes.Add("stop-check: $reason") }
    else { $output.decision = 'block'; $output.reason = $reason }
}
if ($notes.Count) { $output.systemMessage = $notes -join "`n" }
if ($output.Count) { $output | ConvertTo-Json -Depth 3 -Compress }
exit 0
