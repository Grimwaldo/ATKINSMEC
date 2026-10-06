<#
    Builds ATKINSMEC for every supported Revit version and deploys each
    build into that version's add-ins folder.

        .\build-all.ps1                     # all versions, Release
        .\build-all.ps1 -Versions 2024,2025 # just those
        .\build-all.ps1 -Configuration Debug

    Runtime per Revit version:
        2022, 2023, 2024 -> .NET Framework 4.8
        2025, 2026       -> .NET 8
        2027             -> .NET 10

    A version whose Revit is not installed still builds; the files are
    copied into %AppData%\Autodesk\Revit\Addins\<version> ready for when
    it is. Close Revit before building, or the copy is skipped because
    the running instance holds the old DLL.
#>

param(
    [int[]]   $Versions = @(2022, 2023, 2024, 2025, 2026, 2027),
    [string]  $Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "ATKINSMEC\ATKINSMEC.csproj"

if (-not (Test-Path $project)) {
    throw "Project not found: $project"
}

$results = @()

foreach ($version in $Versions) {

    Write-Host ""
    Write-Host "=== Revit $version ($Configuration) ===" -ForegroundColor Cyan

    $output = & dotnet build $project `
        -c $Configuration `
        -p:RevitVersion=$version `
        --nologo `
        -v minimal 2>&1

    $ok = $LASTEXITCODE -eq 0

    $output | ForEach-Object { Write-Host "   $_" }

    $addinDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version"
    $dll = Join-Path $addinDir "ATKINSMEC.dll"

    $results += [pscustomobject]@{
        Revit    = $version
        Built    = if ($ok) { "yes" } else { "FAILED" }
        Deployed = if (Test-Path $dll) {
                       (Get-Item $dll).LastWriteTime.ToString("HH:mm:ss")
                   } else { "-" }
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Cyan
$results | Format-Table -AutoSize
