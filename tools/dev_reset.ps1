# Brings the development environment back to the state right after the seed (DEC-100), so a Postman run always
# starts the same way: database zengear recreated in the set-up order of api/CLAUDE.md ("Local database"), the
# accounts of the Firebase Auth Emulator deleted, the Admin account created again with bootstrap-admin (DEC-096),
# the dev-only fixtures of postman/fixtures applied, Redis cleared.
# Usage, from anywhere: pwsh -NoProfile -File tools/dev_reset.ps1 [-WhatIf]
# Exit code 0 done, 1 a prerequisite missing (the message says what to fix), 2 refused: PostgreSQL, Redis or the
# emulator is not the local one of api/docker-compose.yml, or the database is not zengear.
# Never prints a secret: user-secrets are parsed for host, port and database only, and the password reset link
# that bootstrap-admin prints is dropped. The accounts are emulator accounts with test values only.
[CmdletBinding(SupportsShouldProcess)]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$api = Join-Path $root 'api/src/ZenGear.Api'

$Database = 'zengear'
$ProjectId = 'demo-zengear'                       # appsettings.Development.json, api/docker-compose.yml
$Ports = @{ Postgres = 5432; Redis = 6379; Emulator = 9099 }   # published by api/docker-compose.yml
$LocalHosts = 'localhost', '127.0.0.1'
$AdminEmail = 'owner@zengear.test'
$AdminName = 'Chủ cửa hàng'
$TestPassword = 'dev-reset-only'                  # emulator accounts only; the Postman collection signs in without it

function Say([string]$text) { [Console]::Out.WriteLine($text) }
function Stop-With([int]$code, [string]$text) {
    [Console]::Out.WriteLine($(if ($code -eq 2) { "REFUSED: $text" } else { "MISSING: $text" }))
    exit $code
}

# --- Endpoints, from the same places the API reads them --------------------------------------------------------

# User-secrets of ZenGear.Api as a key → value map; the values stay in memory and are never printed.
$secrets = @{}
dotnet user-secrets list --project $api 2>$null | Where-Object { $_ -match ' = ' } | ForEach-Object {
    $key, $value = $_ -split ' = ', 2
    $secrets[$key.Trim()] = $value
}
if ($LASTEXITCODE -ne 0 -and $secrets.Count -eq 0) {
    Stop-With 1 'dotnet user-secrets could not be read: install the .NET SDK named in api/global.json.'
}

# Host, port and database of an Npgsql connection string.
function Get-PgTarget([string]$connection) {
    $parts = @{}
    foreach ($pair in $connection -split ';') {
        $k, $v = $pair -split '=', 2
        if ($v) { $parts[$k.Trim().ToLowerInvariant()] = $v.Trim() }
    }
    $hostName = @($parts['host'], $parts['server']) | Where-Object { $_ } | Select-Object -First 1
    $port = if ($parts['port']) { [int]$parts['port'] } else { 5432 }
    $db = @($parts['database'], $parts['initial catalog']) | Where-Object { $_ } | Select-Object -First 1
    [pscustomobject]@{ Host = $hostName; Port = $port; Database = $db }
}

# host:port, as in FIREBASE_AUTH_EMULATOR_HOST or a StackExchange.Redis configuration string.
function Get-HostPort([string]$value, [int]$defaultPort) {
    $first = ($value -split ',')[0].Trim()
    $hostName, $port = $first -split ':', 2
    [pscustomobject]@{ Host = $hostName; Port = $(if ($port) { [int]$port } else { $defaultPort }) }
}

foreach ($key in 'ConnectionStrings:ZenGearMigrations', 'ConnectionStrings:ZenGear') {
    if (-not $secrets.ContainsKey($key)) {
        Stop-With 1 "user-secret $key is not set: dotnet user-secrets set `"$key`" `"<value>`" --project api/src/ZenGear.Api (api/CLAUDE.md, Local database)."
    }
}
if (-not $secrets.ContainsKey('Firebase:DefaultStaffPassword')) {
    Stop-With 1 'user-secret Firebase:DefaultStaffPassword is not set: without it POST /api/v1/admin/staff fails (api/CLAUDE.md, Local database).'
}

$launch = Get-Content (Join-Path $api 'Properties/launchSettings.json') -Raw | ConvertFrom-Json
$emulatorValues = @($launch.profiles.http.environmentVariables.FIREBASE_AUTH_EMULATOR_HOST, $env:FIREBASE_AUTH_EMULATOR_HOST) |
    Where-Object { $_ }
$apiUrls = @($launch.profiles.PSObject.Properties.Value | ForEach-Object { $_.applicationUrl } | Where-Object { $_ } |
    ForEach-Object { $_ -split ';' } | Sort-Object -Unique)

# --- Refusals (exit 2): checked first, -WhatIf included --------------------------------------------------------

foreach ($key in 'ConnectionStrings:ZenGearMigrations', 'ConnectionStrings:ZenGear') {
    $pg = Get-PgTarget $secrets[$key]
    if ($pg.Host -notin $LocalHosts -or $pg.Port -ne $Ports.Postgres) {
        Stop-With 2 "$key does not point to the PostgreSQL of api/docker-compose.yml (localhost:$($Ports.Postgres))."
    }
    if ($pg.Database -ne $Database) {
        Stop-With 2 "$key names another database than $Database; this command only recreates $Database."
    }
}
$redis = if ($secrets.ContainsKey('ConnectionStrings:Redis')) { Get-HostPort $secrets['ConnectionStrings:Redis'] 6379 } else { $null }
if ($redis -and ($redis.Host -notin $LocalHosts -or $redis.Port -ne $Ports.Redis)) {
    Stop-With 2 "ConnectionStrings:Redis does not point to the Redis of api/docker-compose.yml (127.0.0.1:$($Ports.Redis))."
}
foreach ($value in $emulatorValues) {
    $emulator = Get-HostPort $value $Ports.Emulator
    if ($emulator.Host -notin $LocalHosts -or $emulator.Port -ne $Ports.Emulator) {
        Stop-With 2 "FIREBASE_AUTH_EMULATOR_HOST ($value) is not the emulator of api/docker-compose.yml (localhost:$($Ports.Emulator))."
    }
}
$secrets = $null   # nothing below needs a value
$emulatorUrl = "http://127.0.0.1:$($Ports.Emulator)"

# --- Prerequisites (exit 1) ------------------------------------------------------------------------------------

function Test-Port([int]$port) {
    $client = [Net.Sockets.TcpClient]::new()
    try { return $client.ConnectAsync('127.0.0.1', $port).Wait(500) } catch { return $false } finally { $client.Dispose() }
}

# The running container that publishes a port, or $null.
function Get-PortContainer([int]$port) {
    docker ps --filter "publish=$port" --format '{{.Names}}' 2>$null | Select-Object -First 1
}

$startServices = 'start the services: Visual Studio, startup project docker-compose, profile Infrastructure, Ctrl+F5 - or `docker compose up -d postgres redis firebase` in api/.'
$postgres = $null
$redisContainer = $null
if ($WhatIfPreference) {
    Say 'What if: the prerequisites (Docker, the containers, the emulator, the API stopped) are checked only by a real run.'
} else {
    docker info --format '{{.ServerVersion}}' *> $null
    if ($LASTEXITCODE -ne 0) { Stop-With 1 'Docker does not answer: start Docker Desktop.' }
    $postgres = Get-PortContainer $Ports.Postgres
    if (-not $postgres) { Stop-With 1 "no container publishes port $($Ports.Postgres): $startServices" }
    $redisContainer = Get-PortContainer $Ports.Redis
    if (-not $redisContainer) { Stop-With 1 "no container publishes port $($Ports.Redis): $startServices" }
    if (-not (Test-Port $Ports.Emulator)) { Stop-With 1 "the Firebase Auth Emulator does not answer on $($Ports.Emulator): $startServices" }
    if (-not (Get-Command python -ErrorAction SilentlyContinue)) { Stop-With 1 'python is not on PATH (tools/emulator_token.py creates the accounts).' }
    foreach ($url in $apiUrls) {
        $port = ([uri]$url).Port
        if (Test-Port $port) {
            Stop-With 1 "the API (or another program) answers on ${url}: stop it first; the API synchronizes the permissions when it starts, so it must start after the reset."
        }
    }
}

# --- Steps -----------------------------------------------------------------------------------------------------

# psql inside the PostgreSQL container (local trust as postgres), reading a file on stdin, in one transaction
# unless -NoTransaction (DROP DATABASE cannot run in one). Returns the output, with $LASTEXITCODE set.
function Invoke-PsqlFile([string]$db, [string]$file, [string[]]$variables = @(), [switch]$NoTransaction) {
    $options = @('-X', '-q', '-v', 'ON_ERROR_STOP=1', '-U', 'postgres', '-d', $db) + $(if ($NoTransaction) { @() } else { @('-1') })
    $vars = $variables | ForEach-Object { '-v'; $_ }
    Get-Content $file -Raw -Encoding utf8 |
        docker exec -i -e PGOPTIONS='-c client_min_messages=warning' $postgres psql @options @vars -f - 2>&1
}

# A script that needs a table or function of the specification not migrated yet is skipped as a whole, as in
# api/acceptance/init/init.sh; any other error stops the reset.
$spec = (Get-ChildItem (Join-Path $root 'api/db') -File | Where-Object Name -match '^zengear_0[2-5]_.*\.sql$' |
    Get-Content -Raw) -join "`n"
function Invoke-SetupScript([string]$file, [string[]]$variables = @()) {
    $name = Split-Path $file -Leaf
    $output = Invoke-PsqlFile $Database $file $variables
    if ($LASTEXITCODE -eq 0) { Say "  applied $name"; return }
    $text = $output -join "`n"
    if ($text -match 'ERROR: +(?:relation|function) "?([a-z_]+)"?.* does not exist') {
        $missing = $Matches[1]
        if ($spec -match "(?i)CREATE (TABLE|(OR REPLACE )?FUNCTION) $missing\b") {
            Say "  skipped ${name}: $missing is not migrated yet"
            return
        }
    }
    Say $text
    Stop-With 1 "$name failed (output above)."
}

# Creates an emulator account the way tools/emulator_token.py does and returns its uid; the ID token it prints
# on stdout is discarded.
function New-EmulatorAccount([string]$email) {
    $env:FIREBASE_AUTH_EMULATOR_HOST = "127.0.0.1:$($Ports.Emulator)"
    $err = @(python (Join-Path $root 'tools/emulator_token.py') $email $TestPassword --create 2>&1 |
        Where-Object { $_ -is [System.Management.Automation.ErrorRecord] } | ForEach-Object { "$_" })
    if ($LASTEXITCODE -ne 0) { Stop-With 1 "the emulator account $email could not be created: $($err -join ' ')" }
    ($err | Where-Object { "$_" -match '^uid: (.+)$' } | Select-Object -First 1) -replace '^uid: ', ''
}

Say "Reset of the development environment ($Database, Redis, Firebase Auth Emulator of $ProjectId)"

if ($PSCmdlet.ShouldProcess("database $Database", 'drop and create with api/db/zengear_01_database.sql')) {
    $output = Invoke-PsqlFile 'postgres' (Join-Path $root 'api/db/zengear_01_database.sql') -NoTransaction
    if ($LASTEXITCODE -ne 0) { Say ($output -join "`n"); Stop-With 1 'zengear_01_database.sql failed (output above).' }
    Say "  created $Database"
}

if ($PSCmdlet.ShouldProcess("database $Database", 'apply the migrations (dotnet ef database update)')) {
    Push-Location (Join-Path $root 'api')
    try {
        $output = dotnet ef database update -p src/ZenGear.Data -s src/ZenGear.Api 2>&1
        if ($LASTEXITCODE -ne 0) {
            Say ($output | Select-Object -Last 20 | Out-String)
            Stop-With 1 'the migrations failed (output above); `dotnet tool restore` in api/ if dotnet-ef is missing.'
        }
    } finally { Pop-Location }
    Say '  migrations applied'
}

$setup = @(Join-Path $root 'api/db/zengear_08_roles.sql') + @(Get-ChildItem (Join-Path $root 'api/seed') -Filter '0*.sql' | Sort-Object Name | ForEach-Object { $_.FullName })
if ($PSCmdlet.ShouldProcess("database $Database", "run $(($setup | ForEach-Object { Split-Path $_ -Leaf }) -join ', ')")) {
    foreach ($file in $setup) { Invoke-SetupScript $file }
}

if ($PSCmdlet.ShouldProcess("Firebase Auth Emulator, project $ProjectId", 'delete every account')) {
    Invoke-RestMethod -Method Delete -Uri "$emulatorUrl/emulator/v1/projects/$ProjectId/accounts" -NoProxy | Out-Null
    Say '  emulator accounts deleted'
}

if ($PSCmdlet.ShouldProcess($AdminEmail, 'create the emulator account and run bootstrap-admin (DEC-096)')) {
    New-EmulatorAccount $AdminEmail | Out-Null
    $output = dotnet run --project $api --launch-profile http -- bootstrap-admin --email $AdminEmail --name $AdminName 2>&1
    $code = $LASTEXITCODE
    # The password reset link is a credential: never shown, whatever the outcome.
    $safe = $output | ForEach-Object { "$_" } | Where-Object { $_ -notmatch '://' -and $_ -notmatch 'oobCode' }
    if ($code -ne 0) { Say ($safe | Select-Object -Last 20 | Out-String); Stop-With 1 'bootstrap-admin failed (output above).' }
    Say "  $(($safe | Where-Object { $_ -match '^Admin created' } | Select-Object -First 1))"
}

# Dev-only fixtures: postman/fixtures/accounts.txt lists "<psql variable> <email>" accounts created in the
# emulator, then every postman/fixtures/*.sql runs with those variables set to the uids.
# Keep in step with the step "Admin account and fixtures" of the job acceptance in .github/workflows/pipeline.yml,
# which does the same for CI (this script refuses the CI ports).
$fixtures = Join-Path $root 'postman/fixtures'
$accounts = if (Test-Path (Join-Path $fixtures 'accounts.txt')) {
    Get-Content (Join-Path $fixtures 'accounts.txt') | Where-Object { $_ -match '^\s*[a-z_]+\s+\S+@\S+\s*$' } |
        ForEach-Object { $name, $email = $_.Trim() -split '\s+'; [pscustomobject]@{ Name = $name; Email = $email } }
} else { @() }
$fixtureFiles = @(if (Test-Path $fixtures) { Get-ChildItem $fixtures -Filter '*.sql' | Sort-Object Name | ForEach-Object { $_.FullName } })
if ($fixtureFiles.Count -gt 0 -and
    $PSCmdlet.ShouldProcess('postman/fixtures', "create $(@($accounts).Count) emulator accounts, run $(($fixtureFiles | ForEach-Object { Split-Path $_ -Leaf }) -join ', ')")) {
    $variables = @(foreach ($account in $accounts) { "$($account.Name)=$(New-EmulatorAccount $account.Email)" })
    foreach ($file in $fixtureFiles) { Invoke-SetupScript $file $variables }
}

if ($PSCmdlet.ShouldProcess("Redis on port $($Ports.Redis)", 'FLUSHALL')) {
    $output = docker exec $redisContainer redis-cli FLUSHALL 2>&1
    if ($LASTEXITCODE -ne 0) { Say ($output -join "`n"); Stop-With 1 'redis-cli FLUSHALL failed (output above).' }
    Say '  Redis cleared'
}

Say 'Done. Start the API: dotnet run --project api/src/ZenGear.Api --launch-profile http'
exit 0
