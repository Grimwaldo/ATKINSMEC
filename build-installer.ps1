<#
    Builds all six Revit versions in Release, stages them into
    installer\payload\<version>\, and packs the result.

    Produces installer\output\ATKINSMEC-Setup.zip - unzip anywhere and
    double-click Install.cmd. No administrator rights are needed.

    If Inno Setup is installed, ATKINSMEC.iss can be compiled instead for
    a single Setup.exe; see installer\README.md.
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$Root     = $PSScriptRoot
$Project  = Join-Path $Root 'ATKINSMEC\ATKINSMEC.csproj'
$Payload  = Join-Path $Root 'installer\payload'
$Output   = Join-Path $Root 'installer\output'
$Versions = @('2022', '2023', '2024', '2025', '2026', '2027')

# Debug symbols are not shipped
$Exclude = @('*.pdb')

Write-Host ''
Write-Host '  Building ATKINSMEC installer' -ForegroundColor White
Write-Host ''

if (Get-Process -Name 'Revit' -ErrorAction SilentlyContinue) {
    Write-Host '  Revit is running. The build itself is fine, but its' -ForegroundColor Yellow
    Write-Host '  deploy step will silently skip the locked versions.' -ForegroundColor Yellow
    Write-Host '  The installer payload is copied from bin\Release, so' -ForegroundColor Yellow
    Write-Host '  the package is still correct.' -ForegroundColor Yellow
    Write-Host ''
}

if (Test-Path $Payload) { Remove-Item $Payload -Recurse -Force }
New-Item -ItemType Directory -Path $Payload -Force | Out-Null

if (-not (Test-Path $Output)) {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}

$staged = 0

foreach ($version in $Versions) {
    if (-not $SkipBuild) {
        Write-Host "  Building $version ..." -NoNewline

        $log = & dotnet build $Project -c Release -p:RevitVersion=$version --nologo -v minimal 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Host ''
            Write-Host ($log | Out-String) -ForegroundColor Red
            throw "Build failed for Revit $version."
        }

        Write-Host ' ok' -ForegroundColor Green
    }

    $binary = Join-Path $Root "ATKINSMEC\bin\Release\$version"

    if (-not (Test-Path $binary)) {
        throw "No build output at $binary. Run without -SkipBuild."
    }

    $dest = Join-Path $Payload $version

    Copy-Item -Path $binary -Destination $dest -Recurse -Force

    foreach ($pattern in $Exclude) {
        Get-ChildItem -Path $dest -Recurse -Filter $pattern -File |
            Remove-Item -Force
    }

    $staged++
}

Write-Host ''
Write-Host "  Staged $staged version(s) into installer\payload" -ForegroundColor Green

# ----------------------------------------------------------------------
# The single self-contained installer executable

$exeProject = Join-Path $Root 'installer\exe\ATKINSMEC-Setup.csproj'
$exePayload = Join-Path $Root 'installer\exe\payload.zip'

Write-Host '  Packing the payload ...' -NoNewline

if (Test-Path $exePayload) { Remove-Item $exePayload -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem

[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $Payload,
    $exePayload,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

Write-Host ' ok' -ForegroundColor Green

Write-Host '  Building ATKINSMEC-Setup.exe ...' -NoNewline

$exeLog = & dotnet build $exeProject -c Release --nologo -v minimal 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host ($exeLog | Out-String) -ForegroundColor Red
    throw 'The installer executable failed to build.'
}

Write-Host ' ok' -ForegroundColor Green

$builtExe = Join-Path $Root 'installer\exe\bin\Release\ATKINSMEC-Setup.exe'
$finalExe = Join-Path $Output 'ATKINSMEC-Setup.exe'

Copy-Item $builtExe $finalExe -Force

$exeSize = [math]::Round((Get-Item $finalExe).Length / 1MB, 1)

Write-Host ''
Write-Host "  $finalExe" -ForegroundColor White
Write-Host "  $exeSize MB - one file, double-click it. No admin rights." -ForegroundColor Green

# ----------------------------------------------------------------------
# The script-based package as well, for anyone who cannot run an exe

$zip = Join-Path $Output 'ATKINSMEC-Setup.zip'

if (Test-Path $zip) { Remove-Item $zip -Force }

$staging = Join-Path $env:TEMP ('atkinsmec-setup-' + [guid]::NewGuid().ToString('N'))

New-Item -ItemType Directory -Path $staging -Force | Out-Null

try {
    Copy-Item (Join-Path $Root 'installer\Install.ps1')   $staging
    Copy-Item (Join-Path $Root 'installer\Install.cmd')   $staging
    Copy-Item (Join-Path $Root 'installer\Uninstall.cmd') $staging
    Copy-Item (Join-Path $Root 'installer\README.md')     $staging
    Copy-Item $Payload (Join-Path $staging 'payload') -Recurse

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    [System.IO.Compression.ZipFile]::CreateFromDirectory($staging, $zip)
}
finally {
    Remove-Item $staging -Recurse -Force -ErrorAction SilentlyContinue
}

$size = [math]::Round((Get-Item $zip).Length / 1MB, 1)

Write-Host ''
Write-Host "  $zip" -ForegroundColor White
Write-Host "  $size MB - unzip and run Install.cmd, no admin rights needed." -ForegroundColor Green
Write-Host ''

# A single Setup.exe instead, when Inno Setup is available
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    Write-Host '  Inno Setup found, building Setup.exe ...' -NoNewline

    & $iscc (Join-Path $Root 'installer\ATKINSMEC.iss') /Q

    if ($LASTEXITCODE -eq 0) {
        Write-Host ' ok' -ForegroundColor Green
        Write-Host "  $Output\ATKINSMEC-Setup.exe" -ForegroundColor White
    }
    else {
        Write-Host ' failed' -ForegroundColor Red
    }

    Write-Host ''
}
# Inno Setup is not needed for a single exe - ATKINSMEC-Setup.exe above
# already is one. The .iss is only there for anyone who wants a wizard
# with an Apps & features entry.
