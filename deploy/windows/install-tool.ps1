# Builds the release channel from this checkout and installs/updates the global dotnet tool plus the SPA.
# Usage: .\deploy\windows\install-tool.ps1   (from the repo root, PowerShell)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Push-Location $root
try {
    dotnet pack src/Grimoire.Cli -c Release -o build/packages
    if (dotnet tool list -g | Select-String -Quiet "^grimoire\s") {
        dotnet tool update -g Grimoire --add-source build/packages --no-cache
    } else {
        dotnet tool install -g Grimoire --add-source build/packages --no-cache
    }
    Push-Location web
    try { npm run build } finally { Pop-Location }
    $webRoot = Join-Path $env:USERPROFILE ".grimoire\web"
    if (Test-Path $webRoot) { Remove-Item $webRoot -Recurse -Force }
    Copy-Item web/out $webRoot -Recurse
    grimoire --version
} finally {
    Pop-Location
}
