# SessionStart hook: prepare a cloud session for the unattended workflow (CLAUDE.md, Workflow).
# - Starts the Docker daemon when it does not answer: the ready check of /implement-feature asks for it, and the
#   integration tests (Testcontainers) and the acceptance environment of /finish-feature run on it.
# - Installs gitleaks 8.24.3, the version of .github/workflows/secrets.yml, when it is missing, so that
#   .githooks/pre-commit scans every commit; the archive must match its pinned SHA-256 before it is unpacked.
# Does nothing locally: Docker Desktop and gitleaks are the developer's own there.
# Never fails the session start: a problem is printed, and the ready check of /implement-feature stops a feature
# whose session still has no Docker.
$ErrorActionPreference = 'Continue'
if ($env:CLAUDE_CODE_REMOTE -ne 'true') { exit 0 }

$gitleaksVersion = '8.24.3'
# From gitleaks_8.24.3_checksums.txt of the GitHub release.
$gitleaksSha256 = '9991e0b2903da4c8f6122b5c3186448b927a5da4deef1fe45271c3793f4ee29c'
$report = @()

function Test-Docker {
    docker info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

# 1. Docker.
if (Test-Docker) {
    $report += 'Docker: running'
} elseif (-not (Get-Command dockerd -ErrorAction SilentlyContinue)) {
    $report += 'Docker: dockerd is not installed, the Docker tests cannot run in this session'
} else {
    # setsid -f: the daemon leaves the hook's process group and outlives the hook. The shell points its output at
    # the log file itself: a pwsh redirection would hold a pipe open and wait for the daemon forever.
    & bash -c 'setsid -f dockerd > /tmp/dockerd.log 2>&1 < /dev/null'
    $deadline = (Get-Date).AddSeconds(30)
    while (-not (Test-Docker) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 500 }
    $report += if (Test-Docker) { 'Docker: started' } else { 'Docker: did not answer within 30 s (see /tmp/dockerd.log)' }
}

# 2. gitleaks.
if (Get-Command gitleaks -ErrorAction SilentlyContinue) {
    $report += 'gitleaks: installed'
} else {
    $archive = Join-Path ([IO.Path]::GetTempPath()) "gitleaks_$gitleaksVersion.tar.gz"
    $url = "https://github.com/gitleaks/gitleaks/releases/download/v$gitleaksVersion/gitleaks_${gitleaksVersion}_linux_x64.tar.gz"
    & curl -sSL --max-time 60 -o $archive $url
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $archive)) {
        $report += 'gitleaks: download failed, commits are scanned in CI only'
    } elseif ((Get-FileHash -Algorithm SHA256 $archive).Hash -ne $gitleaksSha256) {
        $report += 'gitleaks: checksum mismatch, not installed, commits are scanned in CI only'
    } else {
        & tar -xzf $archive -C /usr/local/bin gitleaks
        $report += if ($LASTEXITCODE -eq 0) { "gitleaks: installed $gitleaksVersion" } else { 'gitleaks: unpacking failed' }
    }
    Remove-Item $archive -ErrorAction SilentlyContinue
}

Write-Output ('session-start: ' + ($report -join '; '))
exit 0
