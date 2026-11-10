# Registers (or with -Remove, deletes) the "ATKINSMEC GitHub sync"
# scheduled task: tools\sync.ps1 every 15 minutes while you are logged
# on, with no window (conhost --headless).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\sync-install.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\sync-install.ps1 -Remove

param([switch]$Remove, [int]$Minutes = 15)

$name = 'ATKINSMEC GitHub sync'
if ($Remove) {
    Unregister-ScheduledTask -TaskName $name -Confirm:$false
    Write-Host "Removed '$name'."
    return
}

$script = Join-Path $PSScriptRoot 'sync.ps1'
$action = New-ScheduledTaskAction -Execute 'conhost.exe' `
    -Argument "--headless powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$script`" -Quiet"
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) `
    -RepetitionInterval (New-TimeSpan -Minutes $Minutes)
$logon = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet -StartWhenAvailable -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 5)
Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger, $logon `
    -Settings $settings -Force | Out-Null
Write-Host "Registered '$name' (every $Minutes min and at logon)."
