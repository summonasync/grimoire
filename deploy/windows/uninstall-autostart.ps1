# Removes the logon task and stops the running console.
# The launcher goes first: it restarts serve when serve exits.
param([switch]$Quiet)

schtasks /query /tn "Grimoire Serve" 2>$null | Out-Null
if ($LASTEXITCODE -eq 0) { schtasks /delete /tn "Grimoire Serve" /f | Out-Null }
Get-CimInstance Win32_Process -Filter "name='wscript.exe'" |
    Where-Object { $_.CommandLine -match 'serve-hidden\.vbs' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Get-CimInstance Win32_Process -Filter "name='grimoire.exe'" |
    Where-Object { $_.CommandLine -match '\sserve\s*$' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
if (-not $Quiet) { Write-Host "Grimoire Serve task removed" }
