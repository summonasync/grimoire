# Registers a logon task that starts the Grimoire console hidden and starts it right away.
# Run from PowerShell or cmd. Git Bash rewrites /create into a path - do not run schtasks from there.
$ErrorActionPreference = "Stop"
$home_ = $env:USERPROFILE
$target = Join-Path $home_ ".grimoire\serve-hidden.vbs"
Copy-Item (Join-Path $PSScriptRoot "serve-hidden.vbs") $target -Force
schtasks /create /f /sc onlogon /tn "Grimoire Serve" /tr "wscript.exe `"$target`"" /rl limited | Out-Null
schtasks /run /tn "Grimoire Serve" | Out-Null
Start-Sleep -Seconds 6
try {
    $health = Invoke-RestMethod -TimeoutSec 15 "http://127.0.0.1:4770/health"
    Write-Host "Grimoire Serve task installed; console is $($health.status) on http://127.0.0.1:4770"
} catch {
    Write-Warning "Task installed but the console did not answer yet - check $home_\.grimoire\serve.log"
}
