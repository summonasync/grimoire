# Removes the logon task and stops the running console.
schtasks /delete /tn "Grimoire Serve" /f | Out-Null
Get-CimInstance Win32_Process -Filter "name='grimoire.exe'" |
    Where-Object { $_.CommandLine -match '\sserve\s*$' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Write-Host "Grimoire Serve task removed"
