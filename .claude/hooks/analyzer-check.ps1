# PostToolUse (Edit|Write|MultiEdit): after Claude edits a .cs file under api/, build only the project that holds
# it (analyzers on, warnings as errors as in Directory.Build.props) and send the analyzer errors back to Claude.
# csharp-ls reports compiler diagnostics only (measured 2026-10-02, DEC-100): analyzer rules surface here, or at
# the build of stop-check.ps1. Never blocks on its own failure.
$ErrorActionPreference = 'Stop'
try { $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json } catch { exit 0 }
$file = $payload.tool_input.file_path
if (-not $file -or $file -notmatch '(^|[\\/])api[\\/].+\.cs$' -or -not (Test-Path $file)) { exit 0 }

# The nearest project file above the edited file.
$dir = Split-Path (Resolve-Path $file) -Parent
while ($dir -and -not (Get-ChildItem $dir -Filter *.csproj -File)) { $dir = Split-Path $dir -Parent }
if (-not $dir) { exit 0 }
$project = (Get-ChildItem $dir -Filter *.csproj -File | Select-Object -First 1).FullName

$output = & dotnet build $project --no-restore --no-dependencies -v q -nologo 2>&1
if ($LASTEXITCODE -eq 0) { exit 0 }
# Files locked by a running API or Visual Studio, or a restore needed: stop-check reports them.
if ($output -match 'MSB3027|MSB3021|NETSDK1004|being used by another process') { exit 0 }
# Compiler errors only: the LSP shows them, and they are normal in the middle of a change.
$analyzer = $output | Select-String -Pattern ': error (S|CA)\d+' | ForEach-Object Line | Select-Object -Unique -First 20
if (-not $analyzer) { exit 0 }
[Console]::Error.WriteLine("Analyzer errors in $(Split-Path $project -Leaf):`n" + ($analyzer -join "`n"))
exit 2
