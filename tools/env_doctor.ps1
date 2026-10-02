# Read-only check of the local environment: Docker, the SDK, the services of api/docker-compose.yml, the
# database set-up of api/CLAUDE.md ("Local database"), the user-secrets and the running API.
# Usage, from anywhere: pwsh -NoProfile -File tools/env_doctor.ps1 [-Json]
# Prints one row per check (PASS, WARN, FAIL or SKIP) with the fix to try; exit code 1 when a check fails.
# Changes nothing. Never prints a secret: user-secrets are reported by key name only, their values are
# dropped as soon as they are read.
param([switch]$Json)
$ErrorActionPreference = 'Continue'
$root = Split-Path -Parent $PSScriptRoot
$results = [System.Collections.Generic.List[object]]::new()

function Add-Result([string]$name, [string]$result, [string]$hint) {
    $results.Add([pscustomobject]@{ Check = $name; Result = $result; Hint = $(if ($result -eq 'PASS') { '' } else { $hint }) })
}

# A check: a test that returns $true or $false (an exception counts as $false), the level of a failure and the fix.
function Check([string]$name, [scriptblock]$test, [string]$hint, [string]$level = 'FAIL') {
    $ok = $false
    try { $ok = [bool](& $test | Select-Object -Last 1) } catch { $ok = $false }
    Add-Result $name $(if ($ok) { 'PASS' } else { $level }) $hint
}

function Test-Port([int]$port) {
    $client = [Net.Sockets.TcpClient]::new()
    try { return $client.ConnectAsync('127.0.0.1', $port).Wait(500) } catch { return $false } finally { $client.Dispose() }
}

# The running container that publishes a port: its name and compose project, or $null.
function Get-PortContainer([int]$port) {
    $line = docker ps --filter "publish=$port" --format '{{.Names}}|{{.Label "com.docker.compose.project"}}' 2>$null |
        Select-Object -First 1
    if (-not $line) { return $null }
    $name, $project = $line -split '\|', 2
    return [pscustomobject]@{ Name = $name; Project = $project }
}

# One value from the database zengear, through psql inside the PostgreSQL container (local trust as postgres).
function Invoke-Sql([string]$container, [string]$database, [string]$sql) {
    $value = docker exec $container psql -U postgres -d $database -Atc $sql 2>$null
    if ($LASTEXITCODE -ne 0) { throw "psql failed on $database" }
    return ($value | Select-Object -First 1)
}

$startServices = 'Visual Studio: startup project docker-compose, profile Infrastructure, Ctrl+F5 - or `docker compose up -d postgres redis firebase` in api/.'

# 1. Tools.
docker info --format '{{.ServerVersion}}' *> $null
$dockerUp = $LASTEXITCODE -eq 0
Add-Result 'Docker answers' $(if ($dockerUp) { 'PASS' } else { 'FAIL' }) 'Start Docker Desktop, then run this check again.'

Check 'SDK of api/global.json (rollForward latestFeature)' {
    $wanted = (Get-Content (Join-Path $root 'api/global.json') -Raw | ConvertFrom-Json).sdk.version.Split('.')
    $have = (dotnet --version).Split('-')[0].Split('.')
    # Same major and minor, and a feature band (first digit of the patch) at least the wanted one.
    $have[0] -eq $wanted[0] -and $have[1] -eq $wanted[1] -and [int]$have[2][0].ToString() -ge [int]$wanted[2][0].ToString()
} 'Install the .NET SDK named in api/global.json.'

# 2. Services of api/docker-compose.yml.
foreach ($port in 5432, 6379, 9099, 4000) {
    $p = $port
    Check "Port $p answers" { Test-Port $p } $startServices
}

$emulator = if ($dockerUp) { Get-PortContainer 9099 } else { $null }
if ($emulator) {
    # The acceptance compose runs in CI only (DEC-100); left up on this machine, its emulator takes 9099 and 4000.
    Check "Auth emulator is not the acceptance one ($($emulator.Project))" { $emulator.Project -ne 'zengear-acceptance' } `
        'The acceptance compose is for CI only and holds 9099/4000: `docker compose -p zengear-acceptance down -v`, then start the dev services.' 'WARN'
}

# 3. Database set-up, in the order of api/CLAUDE.md ("Local database").
$postgres = if ($dockerUp) { Get-PortContainer 5432 } else { $null }
if (-not $postgres) {
    Add-Result 'Database checks' 'SKIP' 'No running container publishes port 5432 (a PostgreSQL outside Docker is not checked).'
} else {
    $pg = $postgres.Name
    $hasDatabase = $false
    Check 'Database zengear exists' {
        $script:hasDatabase = (Invoke-Sql $pg 'postgres' "SELECT count(*) FROM pg_database WHERE datname = 'zengear'") -eq '1'
        $script:hasDatabase
    } 'Run api/db/zengear_01_database.sql on the database postgres (it creates zengear with the Vietnamese ICU collation).'
    if ($hasDatabase) {
        $migrations = @(Get-ChildItem (Join-Path $root 'api/src/ZenGear.Data/Migrations') -Filter '*_*.cs' |
            Where-Object { $_.Name -notlike '*.Designer.cs' }).Count
        Check "Migrations applied ($migrations in the code)" {
            [int](Invoke-Sql $pg 'zengear' 'SELECT count(*) FROM "__EFMigrationsHistory"') -eq $migrations
        } 'From api/: dotnet ef database update -p src/ZenGear.Data -s src/ZenGear.Api'
        Check 'Database roles of api/db/zengear_08_roles.sql' {
            (Invoke-Sql $pg 'zengear' "SELECT count(*) FROM pg_roles WHERE rolname IN ('zengear_app', 'zengear_jobs')") -eq '2'
        } 'Run api/db/zengear_08_roles.sql on zengear, after the migrations.'
        Check 'Seed loaded (roles)' {
            [int](Invoke-Sql $pg 'zengear' 'SELECT count(*) FROM roles') -gt 0
        } 'Run api/seed/0*.sql in file order on zengear (api/seed/README.md).'
    }
}

# 4. User-secrets of ZenGear.Api: key names only.
$keys = @(dotnet user-secrets list --project (Join-Path $root 'api/src/ZenGear.Api') 2>$null |
    Where-Object { $_ -match ' = ' } | ForEach-Object { ($_ -split ' = ', 2)[0].Trim() })
foreach ($key in 'ConnectionStrings:ZenGear', 'ConnectionStrings:ZenGearMigrations', 'ConnectionStrings:Redis') {
    $k = $key
    # Without Redis every request reads PostgreSQL: slower, still correct (R-SEC-07).
    $level = if ($k -eq 'ConnectionStrings:Redis') { 'WARN' } else { 'FAIL' }
    Check "Secret $k is set" { $keys -contains $k } `
        "dotnet user-secrets set `"$k`" `"<value>`" --project api/src/ZenGear.Api (api/CLAUDE.md, Local database)." $level
}

# 5. The API started from Visual Studio or `dotnet run` (profile http).
Check 'API answers http://localhost:5256/health' {
    (Invoke-WebRequest 'http://localhost:5256/health' -TimeoutSec 3 -SkipHttpErrorCheck).StatusCode -eq 200
} 'Start ZenGear.Api with the profile http and read its console output.' 'WARN'

# Out-String: plain text on stdout, whether or not it is a terminal.
if ($Json) { ConvertTo-Json -InputObject @($results) } else { $results | Format-Table -AutoSize -Wrap | Out-String -Width 160 | Write-Output }
exit ([int]($results.Result -contains 'FAIL'))
