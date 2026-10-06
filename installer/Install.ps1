<#
    ATKINSMEC installer.

    Copies the add-in into the per-user Revit add-ins folder, for every
    Revit version found. That folder lives under %AppData%, so this needs
    NO administrator rights.

    The add-ins folder is shared with other tools (RevitLookup, Add-In
    Manager), so only our own files are ever written or removed. What was
    installed is recorded in ATKINSMEC.files.txt beside them, and the
    uninstall reads that list rather than clearing the folder.
#>

[CmdletBinding()]
param(
    [switch]$Uninstall,
    [switch]$RemoveSettings,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

$Versions   = @('2022', '2023', '2024', '2025', '2026', '2027')
$Manifest   = 'ATKINSMEC.files.txt'
$SettingsDir = Join-Path $env:APPDATA 'ATKINSMEC'

function Write-Line($text, $colour = 'Gray') {
    if (-not $Quiet) { Write-Host $text -ForegroundColor $colour }
}

function Get-AddinsFolder($version) {
    return Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version"
}

# Install for a version if Revit is installed, or if the add-ins folder
# is already there (a version installed somewhere non-standard)
function Test-WantVersion($version) {
    if (Test-Path (Get-AddinsFolder $version)) { return $true }

    $exe = Join-Path $env:ProgramFiles "Autodesk\Revit $version\Revit.exe"
    if (Test-Path $exe) { return $true }

    return $false
}

function Assert-RevitClosed {
    $running = Get-Process -Name 'Revit' -ErrorAction SilentlyContinue

    if (-not $running) { return }

    $list = ($running | ForEach-Object { "  PID $($_.Id)  $($_.MainWindowTitle)" }) -join "`r`n"

    throw "Revit is running, and Windows will not let its files be replaced while it is.`r`n`r`n$list`r`n`r`nClose Revit and run this again."
}

# ----------------------------------------------------------------------

function Install-Version($version) {
    $payload = Join-Path $PSScriptRoot "payload\$version"

    if (-not (Test-Path $payload)) {
        Write-Line "  $version  - not in this package, skipped" 'DarkGray'
        return $false
    }

    if (-not (Test-WantVersion $version)) {
        Write-Line "  $version  - Revit $version not found, skipped" 'DarkGray'
        return $false
    }

    $target = Get-AddinsFolder $version

    if (-not (Test-Path $target)) {
        New-Item -ItemType Directory -Path $target -Force | Out-Null
    }

    # Anything from a previous install that this one no longer ships
    Remove-Version $version -KeepFolder

    $written = New-Object System.Collections.Generic.List[string]

    $prefix = $payload.TrimEnd('\') + '\'

    foreach ($file in Get-ChildItem -Path $payload -Recurse -File) {
        $relative = $file.FullName.Substring($prefix.Length)
        $dest     = Join-Path $target $relative
        $destDir  = Split-Path $dest -Parent

        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }

        Copy-Item -Path $file.FullName -Destination $dest -Force

        $written.Add($relative)
    }

    # The record of what belongs to us, for a clean uninstall later
    $written | Set-Content -Path (Join-Path $target $Manifest) -Encoding UTF8

    Write-Line "  $version  - installed $($written.Count) file(s)" 'Green'

    return $true
}

function Remove-Version($version, [switch]$KeepFolder) {
    $target = Get-AddinsFolder $version

    if (-not (Test-Path $target)) { return $false }

    $listFile = Join-Path $target $Manifest

    if (-not (Test-Path $listFile)) {
        if (-not $KeepFolder) {
            Write-Line "  $version  - nothing recorded as installed" 'DarkGray'
        }
        return $false
    }

    $removed = 0

    foreach ($relative in Get-Content $listFile) {
        if ([string]::IsNullOrWhiteSpace($relative)) { continue }

        $path = Join-Path $target $relative

        if (Test-Path $path) {
            Remove-Item -Path $path -Force -ErrorAction SilentlyContinue
            $removed++
        }
    }

    Remove-Item -Path $listFile -Force -ErrorAction SilentlyContinue

    # Only folders we created and that are now empty; the add-ins folder
    # itself is shared and is always left alone
    Get-ChildItem -Path $target -Recurse -Directory -ErrorAction SilentlyContinue |
        Sort-Object { $_.FullName.Length } -Descending |
        ForEach-Object {
            if (-not (Get-ChildItem -Path $_.FullName -Force -ErrorAction SilentlyContinue)) {
                Remove-Item -Path $_.FullName -Force -ErrorAction SilentlyContinue
            }
        }

    if (-not $KeepFolder) {
        Write-Line "  $version  - removed $removed file(s)" 'Yellow'
    }

    return $true
}

# ----------------------------------------------------------------------

try {
    Write-Line ''
    Write-Line '  ATKINSMEC - Revit add-ins for ATKINS' 'White'
    Write-Line '  AUTOVENT | CHW VOLUME | DUCT INSULATION | SPRINKLERS' 'DarkGray'
    Write-Line ''

    Assert-RevitClosed

    $touched = 0

    if ($Uninstall) {
        Write-Line '  Removing:' 'White'

        foreach ($version in $Versions) {
            if (Remove-Version $version) { $touched++ }
        }

        if ($RemoveSettings -and (Test-Path $SettingsDir)) {
            Remove-Item -Path $SettingsDir -Recurse -Force
            Write-Line ''
            Write-Line "  Settings removed from $SettingsDir" 'Yellow'
        }
        elseif (Test-Path $SettingsDir) {
            Write-Line ''
            Write-Line "  Settings kept in $SettingsDir" 'DarkGray'
            Write-Line '  (ACC model list and duct insulation rules)' 'DarkGray'
            Write-Line '  Run with -RemoveSettings to delete them too.' 'DarkGray'
        }

        Write-Line ''

        if ($touched -eq 0) {
            Write-Line '  Nothing was installed by this installer.' 'Yellow'
        }
        else {
            Write-Line "  Removed from $touched Revit version(s)." 'Green'
        }
    }
    else {
        Write-Line '  Installing to your own profile - no admin rights needed:' 'White'
        Write-Line "  $env:APPDATA\Autodesk\Revit\Addins\" 'DarkGray'
        Write-Line ''

        foreach ($version in $Versions) {
            if (Install-Version $version) { $touched++ }
        }

        Write-Line ''

        if ($touched -eq 0) {
            throw "No Revit installation was found, so nothing was installed.`r`n`r`nExpected Revit in $env:ProgramFiles\Autodesk\, or an existing add-ins folder under $env:APPDATA\Autodesk\Revit\Addins\."
        }

        Write-Line "  Installed for $touched Revit version(s)." 'Green'
        Write-Line '  Start Revit and look for the ATKINS tab, Mechanical panel.' 'White'
    }

    Write-Line ''

    exit 0
}
catch {
    Write-Host ''
    Write-Host '  Install failed:' -ForegroundColor Red
    Write-Host ''
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ''

    exit 1
}
