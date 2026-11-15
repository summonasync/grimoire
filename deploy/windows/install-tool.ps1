# Builds the release channel from this checkout and installs/updates the global dotnet tool plus the SPA.
# Usage: .\deploy\windows\install-tool.ps1   (from the repo root, PowerShell)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")

# PS 5.1 under "Stop" turns any native stderr line (npm notices) into a terminating error,
# while a real failure only shows up in the exit code.
function Invoke-Native([string] $what, [scriptblock] $command) {
    $ErrorActionPreference = "Continue"
    & $command
    if ($LASTEXITCODE -ne 0) { throw "$what failed with exit code $LASTEXITCODE" }
}

# Any running grimoire.exe (serve, every MCP client's server) locks the tool store, and the
# autostart launcher respawns serve within seconds - so the launcher goes first.
$launchers = @(Get-CimInstance Win32_Process -Filter "Name='wscript.exe'" | ForEach-Object {
    if ($_.CommandLine -match '"([^"]*serve-hidden\.vbs)"') {
        [pscustomobject]@{ Id = $_.ProcessId; Script = $Matches[1] }
    }
})
$launchers | ForEach-Object { Stop-Process -Id $_.Id -Force }

$running = @(Get-Process grimoire -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "Stopping $($running.Count) grimoire process(es); MCP clients need a reconnect afterwards."
    $running | Stop-Process -Force
    $running | Wait-Process -Timeout 10 -ErrorAction SilentlyContinue
}

Push-Location $root
try {
    Invoke-Native "dotnet pack" { dotnet pack src/Grimoire.Cli -c Release -o build/packages }
    if (dotnet tool list -g | Select-String -Quiet "^grimoire\s") {
        Invoke-Native "dotnet tool update" { dotnet tool update -g Grimoire --add-source build/packages --no-cache }
    } else {
        Invoke-Native "dotnet tool install" { dotnet tool install -g Grimoire --add-source build/packages --no-cache }
    }
    Push-Location web
    try { Invoke-Native "npm run build" { npm run build } } finally { Pop-Location }
    $webRoot = Join-Path $env:USERPROFILE ".grimoire\web"
    if (Test-Path $webRoot) { Remove-Item $webRoot -Recurse -Force }
    Copy-Item web/out $webRoot -Recurse
    grimoire --version
} finally {
    Pop-Location
    $launchers | Select-Object -ExpandProperty Script -Unique | ForEach-Object {
        Write-Host "Restarting serve via $_"
        Start-Process wscript.exe -ArgumentList "`"$_`""
    }
}
