# Two-way sync of this folder with GitHub (origin, branch main).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\sync.ps1
#
# 1. Commits anything changed here ("PC sync <date>").
# 2. Fetches GitHub and rebases this PC's commits onto origin/main, so
#    work merged on GitHub (Claude's pull requests) lands in this folder.
# 3. Pushes main to GitHub.
#
# It never overwrites local work: on a conflict the rebase is aborted,
# the local commit stays, and the log says so. It does nothing while a
# merge/rebase is in progress or when the folder is not on main.
# Every run is logged to %AppData%\ATKINSMEC\ATKINSMEC_sync_log.txt.
# Run on a timer by the "ATKINSMEC GitHub sync" scheduled task
# (tools\sync-install.ps1).

param([switch]$Quiet)

$ErrorActionPreference = 'Continue'
$repo = Split-Path -Parent $PSScriptRoot
$logDir = Join-Path $env:APPDATA 'ATKINSMEC'
New-Item -ItemType Directory -Force $logDir | Out-Null
$log = Join-Path $logDir 'ATKINSMEC_sync_log.txt'

function Say($text) {
    $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')  $text"
    Add-Content -Path $log -Value $line -Encoding utf8
    if (-not $Quiet) { Write-Host $line }
}
function G { & git.exe -C $repo @args 2>&1 | ForEach-Object { "$_" } }

# Keep the log short.
if ((Test-Path $log) -and (Get-Item $log).Length -gt 512KB) {
    Get-Content $log -Tail 2000 | Set-Content $log -Encoding utf8
}

$gitDir = (G rev-parse --git-dir) | Select-Object -First 1
if (-not [System.IO.Path]::IsPathRooted($gitDir)) { $gitDir = Join-Path $repo $gitDir }
foreach ($busy in 'rebase-merge', 'rebase-apply', 'MERGE_HEAD', 'CHERRY_PICK_HEAD') {
    if (Test-Path (Join-Path $gitDir $busy)) { Say "SKIP: a $busy is in progress - finish it first."; exit 1 }
}

$branch = (G rev-parse --abbrev-ref HEAD) | Select-Object -First 1
if ($branch -ne 'main') { Say "SKIP: on branch '$branch', not main - switch to main to sync."; exit 1 }

# 1. Commit local changes.
if (G status --porcelain) {
    G add -A | Out-Null
    $msg = "PC sync $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
    $out = G commit -q -m $msg
    if ($LASTEXITCODE -ne 0) { Say "FAILED to commit: $out"; exit 1 }
    Say "Committed local changes: $msg"
}

# 2. Bring GitHub's main in.
$out = G fetch --prune origin
if ($LASTEXITCODE -ne 0) { Say "FAILED to reach GitHub (offline?): $out"; exit 1 }

$behind = [int]((G rev-list --count HEAD..origin/main) | Select-Object -First 1)
$ahead  = [int]((G rev-list --count origin/main..HEAD) | Select-Object -First 1)

if ($behind -gt 0) {
    $out = G rebase origin/main
    if ($LASTEXITCODE -ne 0) {
        G rebase --abort | Out-Null
        Say "CONFLICT: this PC and GitHub changed the same lines. Nothing was lost; run 'git pull --rebase origin main' by hand and resolve. $out"
        exit 2
    }
    Say "Brought in $behind commit(s) from GitHub."
}

# 3. Send this PC's commits up.
if ($ahead -gt 0) {
    $out = G push origin main
    if ($LASTEXITCODE -ne 0) { Say "FAILED to push: $out"; exit 1 }
    Say "Pushed $ahead commit(s) to GitHub."
}

if ($behind -eq 0 -and $ahead -eq 0) { Say "Up to date." }
exit 0
