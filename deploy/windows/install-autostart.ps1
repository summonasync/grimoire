# Registers a logon task that starts the Grimoire console hidden and starts it right away.
# The launcher stays alive to restart serve, so the task gets no time limit and ignores battery state.
# schtasks /xml instead of Register-ScheduledTask: the CIM cmdlets need elevation, schtasks does not.
# Run from PowerShell or cmd. Git Bash rewrites /create into a path - do not run schtasks from there.
$ErrorActionPreference = "Stop"
$home_ = $env:USERPROFILE
$target = Join-Path $home_ ".grimoire\serve-hidden.vbs"
$user = "$env:USERDOMAIN\$env:USERNAME"

& (Join-Path $PSScriptRoot "uninstall-autostart.ps1") -Quiet
Copy-Item (Join-Path $PSScriptRoot "serve-hidden.vbs") $target -Force

$xml = @"
<?xml version="1.0" encoding="UTF-16"?>
<Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
      <UserId>$user</UserId>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id="Author">
      <UserId>$user</UserId>
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>LeastPrivilege</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <StartWhenAvailable>true</StartWhenAvailable>
    <Enabled>true</Enabled>
  </Settings>
  <Actions Context="Author">
    <Exec>
      <Command>wscript.exe</Command>
      <Arguments>"$target"</Arguments>
    </Exec>
  </Actions>
</Task>
"@
$xmlPath = Join-Path $env:TEMP "grimoire-serve-task.xml"
$xml | Out-File $xmlPath -Encoding Unicode
schtasks /create /f /tn "Grimoire Serve" /xml $xmlPath | Out-Null
$created = $LASTEXITCODE
Remove-Item $xmlPath
if ($created -ne 0) { throw "schtasks /create failed with exit code $created" }
schtasks /run /tn "Grimoire Serve" | Out-Null

Start-Sleep -Seconds 6
try {
    $health = Invoke-RestMethod -TimeoutSec 15 "http://127.0.0.1:4770/health"
    Write-Host "Grimoire Serve task installed; console is $($health.status) on http://127.0.0.1:4770"
} catch {
    Write-Warning "Task installed but the console did not answer yet - the launcher keeps retrying; check $home_\.grimoire\serve.log"
}
