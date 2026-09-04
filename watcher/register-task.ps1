<#
.SYNOPSIS
  Registers LegacyMasterWatcher.exe as a Windows Task Scheduler task that
  fires every 5 minutes, at logon and on a clock schedule (per this repo's
  own convention — see reference_schtasks_onlogon_no_repetition.md: a
  logon-only trigger is dormant until next logon, always pair it with a
  TimeTrigger).

  Generalized runner for HOMSys's legacy-master DBF sync endpoints — today
  that's just pricing, more are expected over time (see HOMSYS_SYNC_URLS
  below). One scheduled task hits all configured sync endpoints each run.

.NOTES
  Run this ON THE TARGET VM, as Administrator, after:
    1. Copying dist\LegacyMasterWatcher.exe to a stable path on that machine
       (this script assumes it sits next to itself in the same folder).
    2. Setting HOMSYS_SYNC_URLS and HOMSYS_API_KEY as persistent env vars,
       so the task — which runs headless, no shell to inherit from — can
       read them. Machine scope (setx /M) needs admin; if you don't have
       that, User scope works fine as long as the task runs as your own
       account (the default — no -RunLevel Highest is used below):
         setx HOMSYS_SYNC_URLS "http://<homsys-host>:5200/api/pricing/sync"
         setx HOMSYS_API_KEY   "<the real PricingSync:ApiKey value>"
       Comma-separate multiple URLs as more legacy-master importers get
       their own sync endpoint, e.g.:
         "http://host:5200/api/pricing/sync,http://host:5200/api/reference/sync"
       A new shell/logon may be needed for setx values to take effect for
       services/scheduled tasks.
#>

$exePath = Join-Path $PSScriptRoot "LegacyMasterWatcher.exe"
$taskName = "HOMSys-LegacyMasterWatcher"

if (-not (Test-Path $exePath)) {
    throw "LegacyMasterWatcher.exe not found next to this script at $exePath"
}

$action = New-ScheduledTaskAction -Execute $exePath

$logonTrigger = New-ScheduledTaskTrigger -AtLogOn
$timeTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date) `
    -RepetitionInterval (New-TimeSpan -Minutes 5) `
    -RepetitionDuration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -StartWhenAvailable -MultipleInstances IgnoreNew

Register-ScheduledTask -TaskName $taskName `
    -Action $action `
    -Trigger @($logonTrigger, $timeTrigger) `
    -Settings $settings `
    -Force -ErrorAction Stop

Write-Host "Registered task '$taskName' -> $exePath, every 5 min."
Write-Host "Confirm HOMSYS_SYNC_URLS / HOMSYS_API_KEY are set for the account"
Write-Host "this task runs as (User or Machine scope, whichever you used)."
Write-Host "Log file: %LOCALAPPDATA%\HOMSys\legacy_master_watcher.log"
