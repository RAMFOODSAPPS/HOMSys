<#
.SYNOPSIS
  Registers "SalesOrderBridge.exe --drain" as a Task Scheduler task that runs
  every 5 minutes (plus at logon, per this repo's schtasks convention: a
  logon-only trigger is dormant until next logon, so it is always paired with
  a repeating clock trigger).

  The drain is the offline catch-up for the BMS -> HOMSys bridge: every event
  a VFP screen fires (process/lock, deallocate, invoice, OOS, delivery,
  cancel/RFC) is first written to <bms data folder>\homsys_outbox\. If the
  branch had no internet at the time, this task delivers the backlog within
  ~5 minutes of the connection coming back, even if nobody clicks anything in
  BMS. It also sends the branch heartbeat HOMSys uses for "last synced" and
  its stale-edit guard, and runs the 30-minute reconciliation sweep.

.NOTES
  Run ON ONE ALWAYS-ON PC PER BRANCH (the BMS server is ideal), as
  Administrator. More than one PC is harmless - drainers take turns via
  homsys_outbox\.drain.lock - but one is enough.

  -ExePath  defaults to SalesOrderBridge.exe next to this script (the live BMS
            program folder, where run_bridge.bat already expects it).
  -DataDir  the branch's live BMS data folder (the one holding oowkhdr.dbf).
            Omit to use "destination" from C:\fox\client\config.json.

  Log: %LOCALAPPDATA%\HOMSys\salesorder_bridge.log (of the account the task
  runs as).
#>
param(
    [string]$ExePath = (Join-Path $PSScriptRoot "SalesOrderBridge.exe"),
    [string]$DataDir = ""
)

$taskName = "HOMSys-SalesOrderBridge-Drain"

if (-not (Test-Path $ExePath)) {
    throw "SalesOrderBridge.exe not found at $ExePath"
}
if (-not (Test-Path "C:\fox\client\config.json")) {
    throw "C:\fox\client\config.json not found - the bridge reads homsys_api_url/key/branch from it."
}
if ($DataDir -and -not (Test-Path (Join-Path $DataDir "oowkhdr.dbf"))) {
    throw "oowkhdr.dbf not found under $DataDir - pass the live BMS data folder."
}

$arguments = "--drain"
if ($DataDir) { $arguments = "--drain `"$DataDir`"" }

$action = New-ScheduledTaskAction -Execute $ExePath -Argument $arguments -WorkingDirectory (Split-Path $ExePath)

$logonTrigger = New-ScheduledTaskTrigger -AtLogOn
$timeTrigger = New-ScheduledTaskTrigger -Once -At (Get-Date) `
    -RepetitionInterval (New-TimeSpan -Minutes 5) `
    -RepetitionDuration (New-TimeSpan -Days 3650)

$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -StartWhenAvailable -MultipleInstances IgnoreNew `
    -ExecutionTimeLimit (New-TimeSpan -Minutes 30)

Register-ScheduledTask -TaskName $taskName `
    -Action $action `
    -Trigger @($logonTrigger, $timeTrigger) `
    -Settings $settings `
    -Force -ErrorAction Stop

Write-Host "Registered task '$taskName' -> $ExePath $arguments, every 5 min."
Write-Host "Log file: %LOCALAPPDATA%\HOMSys\salesorder_bridge.log"
