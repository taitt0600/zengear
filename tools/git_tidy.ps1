# Tidy the local branches after pull requests merged on GitHub (DEC-120 (2)): GitHub deletes the branch of a merged
# pull request, but the clone keeps its local branch, the worktree it may have (out/worktrees/approve-*) and, without
# fetch.prune, the remote ref. A rebase-merge changes the SHAs, so git branch --merged cannot tell a merged branch:
# gh says which pull request merged and at which head.
# Usage, from anywhere: pwsh -NoProfile -File tools/git_tidy.ps1 [-Apply] [-SyncMain]
# Without -Apply it only prints what it would do, from the last fetch. -Apply runs git fetch --prune and
# git worktree prune, then deletes each local branch whose upstream is gone when gh shows a merged pull request of
# that branch, from this repository, whose head is the local head; never main, release/* or the current branch, and
# its worktree is removed first, never with --force. -SyncMain also switches to main and pulls --ff-only, only on a
# clean working tree. Without a working gh it only prunes and says so.
# Exit codes: 0 done, 1 a git or gh step failed or -SyncMain was refused (the output says which).
param(
    [switch]$Apply,
    [switch]$SyncMain
)
$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$failed = $false

function Say([string]$text) { [Console]::Out.WriteLine("git_tidy: $text") }
function Fail([string]$text) { Say $text; $script:failed = $true }

# Runs a git command that changes something, or only describes it without -Apply. True when it ran and succeeded.
function Change([string]$what, [string[]]$arguments) {
    if (-not $Apply) { Say "would $what"; return $true }
    $output = git @arguments 2>&1
    if ($LASTEXITCODE -ne 0) { Fail "failed to $what (exit $LASTEXITCODE): $(($output | Out-String).Trim())"; return $false }
    Say $what
    return $true
}

$top = git rev-parse --show-toplevel 2>$null
if ($LASTEXITCODE -ne 0) { Say 'not inside a git repository'; exit 1 }
Set-Location $top

if (-not $Apply) { Say 'dry run: nothing changes; branches are judged from the last fetch (-Apply fetches first)' }
[void](Change 'fetch with --prune' @('fetch', '--prune'))
[void](Change 'prune the worktrees whose folder is gone' @('worktree', 'prune'))

if ($SyncMain) {
    if (git status --porcelain) {
        Fail 'refused to switch to main: the working tree has changes'
    } elseif (Change 'switch to main' @('switch', 'main')) {
        [void](Change 'pull main with --ff-only' @('pull', '--ff-only'))
    }
}

$current = git symbolic-ref --quiet --short HEAD 2>$null
# A dry run of -SyncMain judges the branches as -Apply would, after its switch to main.
$leaving = $null
if ($SyncMain -and -not $Apply -and -not $failed) { $leaving = $current; $current = 'main' }
# The main worktree is the first entry; a branch checked out there cannot lose its worktree.
$mainWorktree = ((git worktree list --porcelain) | Select-Object -First 1) -replace '^worktree ', ''

# A branch is a candidate when its upstream is set but no longer resolves, which is what [gone] means; reading
# rev-parse instead of the [gone] text keeps this independent of the language of git.
$candidates = foreach ($line in git for-each-ref '--format=%(refname:short)%09%(upstream)%09%(objectname)%09%(worktreepath)' refs/heads) {
    $name, $upstream, $head, $worktree = $line -split "`t"
    if (-not $upstream) { continue }
    git rev-parse --verify --quiet $upstream *> $null
    if ($LASTEXITCODE -eq 0) { continue }
    [pscustomobject]@{ Name = $name; Head = $head; Worktree = $worktree }
}

$gh = [bool](Get-Command gh -CommandType Application -ErrorAction SilentlyContinue)
$ghChecked = $false
$ghSkipped = $false
$deleted = @()
foreach ($branch in $candidates) {
    $name = $branch.Name
    if ($name -eq 'main' -or $name -like 'release/*') { Say "kept $name`: never deleted"; continue }
    if ($name -eq $current) { Say "kept $name`: the current branch"; continue }
    if (-not $gh) { $ghSkipped = $true; break }
    $json = gh pr list --head $name --state merged --limit 100 --json number,headRefOid,isCrossRepository 2>$null
    if ($LASTEXITCODE -ne 0) {
        # The first call tells whether gh works at all (signed in, network); a later failure concerns this branch.
        if (-not $ghChecked) { $ghSkipped = $true; break }
        Fail "kept $name`: gh pr list failed (exit $LASTEXITCODE)"
        continue
    }
    $ghChecked = $true
    $pulls = @(($json | Out-String) | ConvertFrom-Json)
    $match = $pulls | Where-Object { -not $_.isCrossRepository -and $_.headRefOid -eq $branch.Head } | Select-Object -First 1
    if (-not $match) {
        $own = $pulls | Where-Object { -not $_.isCrossRepository } | Select-Object -First 1
        $reason = if (-not $pulls) { 'no merged pull request' }
            elseif (-not $own) { "pull request #$($pulls[0].number) is from a fork" }
            else { "its head differs from pull request #$($own.number)" }
        Say "kept $name`: $reason"
        continue
    }
    if ($branch.Worktree -and $name -ne $leaving) {
        if ($branch.Worktree -eq $mainWorktree) { Say "kept $name`: checked out in the main worktree"; continue }
        if (-not (Change "remove the worktree $($branch.Worktree) of $name" @('worktree', 'remove', $branch.Worktree))) {
            Fail "kept $name`: its worktree could not be removed"
            continue
        }
    }
    if (Change "delete $name (pull request #$($match.number) merged)" @('branch', '-D', $name)) { $deleted += $name }
}
if ($ghSkipped) {
    Say "gh is not available (missing, not signed in or offline): $(if ($Apply) { 'only pruned' } else { 'would only prune' }), no branch deleted"
}

$verb = if ($Apply) { 'deleted' } else { 'would delete' }
Say "$verb $($deleted.Count) branch(es)$(if ($deleted) { ': ' + ($deleted -join ', ') })"
exit ([int]$failed)
