<#
    Builds all six Revit versions in Release, stages them into
    installer\payload\<version>\, and packs the result.

    Produces installer\output\AtkinsMEPToolkit-Setup.zip - unzip anywhere and
    double-click Install.cmd. No administrator rights are needed.

    If Inno Setup is installed, ATKINSMEC.iss can be compiled instead for
    a single Setup.exe; see installer\README.md.

    -AutoVent builds AUTOVENT ON ITS OWN instead (the user, 2026-10-15):
    installer\output\AUTOVENT-Setup.exe, the same source built with
    -p:Edition=AutoVent - its own assembly, AUTOVENT.addin at the top of
    each add-ins folder and everything else in an AUTOVENT\ subfolder, its
    own list of the files it wrote. It can sit beside the suite.
#>

[CmdletBinding()]
param(
    [switch]$SkipBuild,
    [switch]$AutoVent
)

$ErrorActionPreference = 'Stop'

$Root     = $PSScriptRoot
$Project  = Join-Path $Root 'ATKINSMEC\ATKINSMEC.csproj'
$Payload  = Join-Path $Root $(if ($AutoVent) { 'installer\payload-autovent' } else { 'installer\payload' })
$Output   = Join-Path $Root 'installer\output'
$Versions = @('2022', '2023', '2024', '2025', '2026', '2027')

# The edition's extra build switches, and what it is called
# (assigned, not "= if ..." - that unrolls a one-item array into a
# string, and splatting a string passes it letter by letter)
$EditionArgs = @()
if ($AutoVent) { $EditionArgs = @('-p:Edition=AutoVent') }
$ProductName = if ($AutoVent) { 'AUTOVENT' } else { 'Atkins MEP Toolkit' }

# Debug symbols are not shipped
$Exclude = @('*.pdb')

Write-Host ''
Write-Host "  Building the $ProductName installer" -ForegroundColor White
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

        $log = & dotnet build $Project -c Release -p:RevitVersion=$version @EditionArgs --nologo -v minimal 2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Host ''
            Write-Host ($log | Out-String) -ForegroundColor Red
            throw "Build failed for Revit $version."
        }

        Write-Host ' ok' -ForegroundColor Green
    }

    $binary = Join-Path $Root $(if ($AutoVent) { "ATKINSMEC\bin\Release\AutoVent\$version" } else { "ATKINSMEC\bin\Release\$version" })

    if (-not (Test-Path $binary)) {
        throw "No build output at $binary. Run without -SkipBuild."
    }

    $dest = Join-Path $Payload $version

    if ($AutoVent) {
        # Everything in its own subfolder, the .addin that points into it
        # at the top - so nothing it installs shares a name with the suite
        $sub = Join-Path $dest 'AUTOVENT'

        New-Item -ItemType Directory -Path $dest -Force | Out-Null
        Copy-Item -Path $binary -Destination $sub -Recurse -Force
        Move-Item -Path (Join-Path $sub 'AUTOVENT.addin') -Destination (Join-Path $dest 'AUTOVENT.addin') -Force
    }
    else {
        Copy-Item -Path $binary -Destination $dest -Recurse -Force
    }

    foreach ($pattern in $Exclude) {
        Get-ChildItem -Path $dest -Recurse -Filter $pattern -File |
            Remove-Item -Force
    }

    $staged++
}

Write-Host ''
Write-Host "  Staged $staged version(s) into $Payload" -ForegroundColor Green

# ----------------------------------------------------------------------
# The single self-contained installer executable

$exeProject = Join-Path $Root 'installer\exe\ATKINSMEC-Setup.csproj'
$zipName    = if ($AutoVent) { 'payload-autovent.zip' } else { 'payload.zip' }
$exePayload = Join-Path $Root "installer\exe\$zipName"
$finalName  = if ($AutoVent) { 'AUTOVENT-Setup.exe' } else { 'AtkinsMEPToolkit-Setup.exe' }

Write-Host '  Packing the payload ...' -NoNewline

if (Test-Path $exePayload) { Remove-Item $exePayload -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem

[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $Payload,
    $exePayload,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

Write-Host ' ok' -ForegroundColor Green

Write-Host "  Building $finalName ..." -NoNewline

$exeLog = & dotnet build $exeProject -c Release "-p:PayloadZip=$zipName" @EditionArgs --nologo -v minimal 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host ''
    Write-Host ($exeLog | Out-String) -ForegroundColor Red
    throw 'The installer executable failed to build.'
}

Write-Host ' ok' -ForegroundColor Green

$builtExe = Join-Path $Root $(if ($AutoVent) { 'installer\exe\bin\Release\AUTOVENT-Setup.exe' } else { 'installer\exe\bin\Release\ATKINSMEC-Setup.exe' })
$finalExe = Join-Path $Output $finalName

Copy-Item $builtExe $finalExe -Force

$exeSize = [math]::Round((Get-Item $finalExe).Length / 1MB, 1)

Write-Host ''
Write-Host "  $finalExe" -ForegroundColor White
Write-Host "  $exeSize MB - one file, double-click it. No admin rights." -ForegroundColor Green

# AUTOVENT on its own is the exe alone: no script package, no Inno Setup
if ($AutoVent) {
    Write-Host ''
    return
}

# ----------------------------------------------------------------------
# The script-based package as well, for anyone who cannot run an exe

$zip = Join-Path $Output 'AtkinsMEPToolkit-Setup.zip'

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
        Write-Host "  $Output\AtkinsMEPToolkit-Setup.exe" -ForegroundColor White
    }
    else {
        Write-Host ' failed' -ForegroundColor Red
    }

    Write-Host ''
}
# Inno Setup is not needed for a single exe - AtkinsMEPToolkit-Setup.exe above
# already is one. The .iss is only there for anyone who wants a wizard
# with an Apps & features entry.
