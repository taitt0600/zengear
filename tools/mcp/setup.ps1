# Set up the MCP servers of Claude Code on this machine (DEC-100): the Docker MCP Toolkit profile zengear, which
# .mcp.json runs with `docker mcp gateway run --profile zengear`, and the servers outside the Docker catalog.
# - Postman (catalog server postman, minimal tool set) in profile zengear; the API key of the person running this is
#   read from a hidden prompt and stored with `docker mcp secret set` through stdin.
# - PostgreSQL (crystaldba/postgres-mcp, not in the catalog): .mcp.json runs the image pinned below by digest; the
#   DATABASE_URI of the development database is built from api/docker-compose.yml and stored in the user environment
#   variable ZENGEAR_MCP_DATABASE_URI. Only localhost, 127.0.0.1 or host.docker.internal are accepted.
# - Firebase is left out: the auth tools of firebase-tools mcp call identitytoolkit.googleapis.com and ignore the
#   Auth Emulator, so they would need a real Firebase project.
# Usage, from anywhere: pwsh -NoProfile -File tools/mcp/setup.ps1 [-WhatIf] [-DbHost <host>] [-FirebaseProject <id>]
#   [-ResetPostmanKey]
# Idempotent: an existing profile, server, key or variable is kept (the key is replaced with -ResetPostmanKey).
# -WhatIf prints what it would do and changes nothing. Exit codes: 0 done, 1 a prerequisite is missing, 2 refused
# input. Never prints the API key or the URI.
param(
    [switch]$WhatIf,
    [string]$DbHost = 'host.docker.internal',
    [string]$FirebaseProject = 'demo-zengear',
    [switch]$ResetPostmanKey
)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$profileId = 'zengear'
$catalog = 'mcp/docker-mcp-catalog'
$postmanSecret = 'postman.postman-api-key'
# Reviewed 2026-10-02: tag 0.3.0 (same digest as latest). Keep in step with .mcp.json.
$postgresImage = 'crystaldba/postgres-mcp:0.3.0@sha256:dbbd346860d29f1543e991f30f3284bf4ab5f096d049ecc3426528f20b1b6e6b'
$uriVariable = 'ZENGEAR_MCP_DATABASE_URI'
$allowedHosts = @('localhost', '127.0.0.1', 'host.docker.internal')

function Say([string]$text) { Write-Output "setup: $text" }
function Stop-Setup([int]$code, [string]$text) { [Console]::Error.WriteLine("setup: $text"); exit $code }

# Runs a change, or only describes it under -WhatIf.
function Change([string]$what, [scriptblock]$action) {
    if ($WhatIf) { Say "would $what"; return }
    Say $what
    & $action
    if ($LASTEXITCODE -ne 0) { Stop-Setup 1 "failed to $what (exit $LASTEXITCODE)" }
}

# Runs docker with a value on stdin, so the value never appears on a command line.
function Invoke-DockerWithInput([string[]]$arguments, [string]$value) {
    $info = [Diagnostics.ProcessStartInfo]::new('docker')
    foreach ($a in $arguments) { $info.ArgumentList.Add($a) }
    $info.RedirectStandardInput = $true
    $info.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($info)
    $process.StandardInput.Write($value)
    $process.StandardInput.Close()
    $process.WaitForExit()
    $global:LASTEXITCODE = $process.ExitCode
}

# 1. Input.
if ($DbHost -notin $allowedHosts) {
    Stop-Setup 2 "refused host '$DbHost': only $($allowedHosts -join ', ') (the development database, never staging or production)"
}
if (-not $FirebaseProject.StartsWith('demo-')) {
    Stop-Setup 2 "refused Firebase project '$FirebaseProject': only demo-* projects of the emulator"
}

# 2. Prerequisites.
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Stop-Setup 1 'docker is not installed (Docker Desktop)' }
docker info --format '{{.ServerVersion}}' *> $null
if ($LASTEXITCODE -ne 0) { Stop-Setup 1 'Docker does not answer: start Docker Desktop' }
$mcpHelp = docker mcp --help 2>&1 | Out-String
if ($LASTEXITCODE -ne 0 -or $mcpHelp -notmatch '(?m)^\s+profile\b' -or $mcpHelp -notmatch '(?m)^\s+gateway\b') {
    Stop-Setup 1 'docker mcp has no profile or gateway command: enable the MCP Toolkit in Docker Desktop and update it'
}
$compose = Join-Path $root 'api/docker-compose.yml'
if (-not (Test-Path $compose)) { Stop-Setup 1 "missing $compose" }

# 3. The Docker catalog, for catalog://$catalog/postman.
if (-not ((docker mcp catalog list 2>$null | Out-String) -match [regex]::Escape($catalog))) {
    Change "pull the catalog $catalog" { docker mcp catalog pull $catalog | Out-Null }
}

# 4. Profile zengear with the Postman server.
$profiles = docker mcp profile list --format json 2>$null | ConvertFrom-Json
if (-not ($profiles | Where-Object id -eq $profileId)) {
    Change "create the profile $profileId" { docker mcp profile create --name $profileId --id $profileId | Out-Null }
}
$hasPostman = $false
if (-not $WhatIf -or ($profiles | Where-Object id -eq $profileId)) {
    $shown = docker mcp profile show $profileId --format json 2>$null | ConvertFrom-Json
    $hasPostman = [bool]($shown.servers | Where-Object { $_.snapshot.server.name -eq 'postman' })
}
if ($hasPostman) {
    Say "profile $profileId has the server postman"
} else {
    Change "add the server postman to the profile $profileId" {
        docker mcp profile server add $profileId --server "catalog://$catalog/postman" | Out-Null
    }
}

# 5. The Postman API key, from a hidden prompt to the Toolkit's secret store.
$hasKey = (docker mcp secret ls 2>$null | Out-String) -match "(?m)^$([regex]::Escape($postmanSecret))\b"
if ($hasKey -and -not $ResetPostmanKey) {
    Say "the secret $postmanSecret is set (replace it with -ResetPostmanKey)"
} elseif ($WhatIf) {
    Say "would ask for the Postman API key and store it as the secret $postmanSecret"
} else {
    $secure = Read-Host -AsSecureString 'Postman API key (input hidden)'
    $plain = [Net.NetworkCredential]::new('', $secure).Password
    if (-not $plain) { Stop-Setup 2 'empty Postman API key' }
    Say "store the secret $postmanSecret"
    Invoke-DockerWithInput @('mcp', 'secret', 'set', $postmanSecret) $plain
    $plain = $null
    if ($LASTEXITCODE -ne 0) { Stop-Setup 1 "failed to store the secret $postmanSecret (exit $LASTEXITCODE)" }
}

# 6. PostgreSQL: the image, and the URI of the development database in the user environment.
docker image inspect $postgresImage *> $null
if ($LASTEXITCODE -eq 0) {
    Say 'the postgres-mcp image is present'
} else {
    Change 'pull the postgres-mcp image (pinned digest)' { docker pull -q $postgresImage | Out-Null }
}
$composeText = Get-Content $compose -Raw
$password = if ($composeText -match '(?m)^\s*POSTGRES_PASSWORD:\s*(\S+)') { $Matches[1] } else { $null }
$port = if ($composeText -match '"(?:127\.0\.0\.1:)?(\d+):5432"') { $Matches[1] } else { $null }
if (-not $password -or -not $port) { Stop-Setup 1 "cannot read POSTGRES_PASSWORD or the port of postgres in $compose" }
$user = if ($composeText -match '(?m)^\s*POSTGRES_USER:\s*(\S+)') { $Matches[1] } else { 'postgres' }
$uri = "postgresql://${user}:$password@${DbHost}:$port/zengear"
if ([Environment]::GetEnvironmentVariable($uriVariable, 'User') -eq $uri) {
    Say "$uriVariable is set (database zengear on ${DbHost}:$port)"
} elseif ($WhatIf) {
    Say "would set the user environment variable $uriVariable (database zengear on ${DbHost}:$port)"
} else {
    [Environment]::SetEnvironmentVariable($uriVariable, $uri, 'User')
    Say "set the user environment variable $uriVariable (database zengear on ${DbHost}:$port); restart VS Code completely so that Claude Code sees it"
}
$uri = $null
$password = $null

# 7. Firebase.
Say "Firebase MCP left out (project $FirebaseProject): its auth tools do not reach the Auth Emulator; use the Emulator UI at http://localhost:4000 or tools/emulator_token.py"

Say $(if ($WhatIf) { 'nothing changed (-WhatIf)' } else { "done: profile $profileId (postman) and postgres-mcp; check with /mcp in Claude Code" })
exit 0
