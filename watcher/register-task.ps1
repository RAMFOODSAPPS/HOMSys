<#
.SYNOPSIS
  Registers LegacyMasterWatcher.exe as a Windows Task Scheduler task that
  fires every 5 minutes, at logon and on a clock schedule (per this repo's
  own convention - see reference_schtasks_onlogon_no_repetition.md: a
  logon-only trigger is dormant until next logon, always pair it with a
  TimeTrigger).

  Runs LegacyMasterWatcher.exe, which reads and parses F:\'s pricing DBFs
  itself (Azure can't reach F:\), diffs against its own local snapshot, and
  POSTs only what changed to each configured sync endpoint - today that's
  just /api/masters/sync; reference data (Customers/Products) stays on the
  manual `import-reference-data` CLI command, not this watcher.

.NOTES
  Run this ON THE TARGET VM, as Administrator, after:
    1. Copying dist\LegacyMasterWatcher.exe and config.example.json to a
       stable path on that machine (this script assumes the exe sits next
       to itself in the same folder).
    2. Copying config.example.json to config.json (same folder) and editing
       it directly - no env vars, no setx, no reboot needed. This same exe
       and this same script deploy unchanged everywhere; only config.json
       differs per machine, e.g.:

         {
           "apiKey": "<the real HeadlessApiKey value>",
           "syncs": [
             { "url": "https://<host>/api/masters/sync", "path": "F:\\" }
           ]
         }

       "path" is the local root this exe reads F:\-style pricing DBFs from
       (read locally only, never sent to the server) - optional, defaults to
       "F:\". Add more entries to "syncs" if a future legacy-master data
       family gets its own sync endpoint and reader.
#>

$exePath = Join-Path $PSScriptRoot "LegacyMasterWatcher.exe"
$taskName = "HOMSys-LegacyMasterWatcher"

if (-not (Test-Path $exePath)) {
    throw "LegacyMasterWatcher.exe not found next to this script at $exePath"
}

$configPath = Join-Path $PSScriptRoot "config.json"
if (-not (Test-Path $configPath)) {
    throw "config.json not found next to this script at $configPath - copy config.example.json to config.json and fill in apiKey/syncs first."
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
Write-Host "Confirm config.json exists next to the exe and its apiKey/syncs are correct."
Write-Host "Log file: %LOCALAPPDATA%\HOMSys\legacy_master_watcher.log"
