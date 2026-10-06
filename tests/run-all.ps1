# Builds and runs every headless test harness under tests\ and says which
# passed. No Revit is needed for any of them.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests\run-all.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests\run-all.ps1 officetest esptest
#
# The previews (ventpreview, esppreview, nwcpreview) render windows to
# PNGs and are not run here - run one by hand and LOOK at what it draws.
# icontest reads the BUILT 2025 DLL, so build the add-in first.

# ValueFromRemainingArguments: under -File, a plain [string[]] takes only
# the FIRST name given and silently drops the rest
param([Parameter(ValueFromRemainingArguments = $true)][string[]] $Only)

$here = Split-Path -Parent $MyInvocation.MyCommand.Path

$tests = @(
    'chwjointtest', 'chwsizetest', 'ducttest', 'equiptest', 'esptest',
    'icontest', 'levelrowtest', 'nwcsettingstest',
    'officetest', 'runtest', 'sheettest', 'synctest',
    'toggletest', 'ventfcutest', 'ventleveltest', 'ventsettingstest'
)

# RECOVERED AT AN OLDER VERSION AND OUT OF STEP - see tests\README.md.
# Each MIRRORS add-in logic by hand rather than compiling it in, and the
# copy that could be recovered predates later fixes, so a failure here
# says nothing about the add-in. Run by name to look at one.
$stale = @('roomtest', 'fittingtest', 'geomtest')

if ($Only) { $tests = @($tests) + $stale | Where-Object { $Only -contains $_ } }

$results = @()

foreach ($name in $tests) {
    $dir = Join-Path $here $name

    Write-Host "=== $name" -ForegroundColor Cyan

    $build = & dotnet build $dir -c Release -nologo -v q 2>&1
    if ($LASTEXITCODE -ne 0) {
        $build | Select-String -Pattern ' error ' | Select-Object -First 5 | ForEach-Object { Write-Host $_.Line }
        $results += [pscustomobject]@{ Test = $name; Result = 'DOES NOT BUILD'; Last = '' }
        continue
    }

    $out = & dotnet run --project $dir -c Release --no-build 2>&1
    $code = $LASTEXITCODE

    $failed = @($out | Where-Object { $_ -match '^\s*(FAIL|FAILED)\b' })
    $failed | Select-Object -First 10 | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }

    $last = ($out | Where-Object { "$_".Trim() -ne '' } | Select-Object -Last 1)

    $results += [pscustomobject]@{
        Test   = $name
        Result = if ($code -eq 0) { 'pass' } else { "FAILED (exit $code)" }
        Last   = "$last".Trim()
    }
}

Write-Host ''
$results | Format-Table -AutoSize -Wrap

if (-not $Only) {
    Write-Host ("Not run - recovered at an older version, out of step with the add-in: " +
                ($stale -join ', ') + ". See tests\README.md.") -ForegroundColor DarkYellow
}

if ($results | Where-Object { $_.Result -ne 'pass' }) { exit 1 }
