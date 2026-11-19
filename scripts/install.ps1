param(
  [Parameter(Mandatory = $true)][string]$Archive,
  [string]$BinDir = (Join-Path $env:LOCALAPPDATA 'grimoire'),
  [string]$WebRoot = (Join-Path $env:USERPROFILE '.grimoire\web')
)
$ErrorActionPreference = 'Stop'

$tmp = Join-Path ([System.IO.Path]::GetTempPath()) ([System.Guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $tmp | Out-Null
try {
  if ($Archive -match '^https?://') {
    Write-Output "downloading $Archive"
    $local = Join-Path $tmp 'archive.zip'
    Invoke-WebRequest -Uri $Archive -OutFile $local -UseBasicParsing
    $Archive = $local
  }
  if (-not (Test-Path $Archive)) { throw "archive not found: $Archive" }

  $x = Join-Path $tmp 'x'
  if ($Archive -like '*.zip') {
    Expand-Archive -Path $Archive -DestinationPath $x
  } else {
    tar -xzf $Archive -C $tmp
    $x = $tmp
  }
  $inner = Get-ChildItem -Path $x -Directory -Recurse -Depth 2 |
    Where-Object { $_.Name -like 'grimoire-*' } | Select-Object -First 1
  if (-not $inner) { throw 'archive carries no grimoire-<version>-<rid> tree' }
  $stamp = Join-Path $inner.FullName 'web\grimoire-web.json'
  if (-not (Test-Path $stamp)) { throw 'archive carries no SPA stamp' }

  $binSrc = Join-Path $inner.FullName 'grimoire.exe'
  if (-not (Test-Path $binSrc)) { $binSrc = Join-Path $inner.FullName 'grimoire' }
  if (-not (Test-Path $binSrc)) { throw 'archive carries no binary' }
  $binName = 'grimoire.exe'

  $newSha = (Get-FileHash $binSrc -Algorithm SHA256).Hash
  $target = Join-Path $BinDir $binName
  $oldSha = if (Test-Path $target) { (Get-FileHash $target -Algorithm SHA256).Hash } else { '' }
  $oldStamp = Join-Path $WebRoot 'grimoire-web.json'
  if ($newSha -eq $oldSha -and (Test-Path $oldStamp) -and
      ((Get-Content $stamp -Raw) -eq (Get-Content $oldStamp -Raw))) {
    Write-Output 'grimoire unchanged: binary and web stamp already match the archive'
    return
  }

  New-Item -ItemType Directory -Path $BinDir -Force | Out-Null
  Copy-Item $binSrc $target -Force
  if (Test-Path $WebRoot) { Remove-Item $WebRoot -Recurse -Force }
  New-Item -ItemType Directory -Path $WebRoot -Force | Out-Null
  Copy-Item (Join-Path $inner.FullName 'web\*') $WebRoot -Recurse -Force

  if (($env:PATH -split ';') -notcontains $BinDir) {
    Write-Output "note: $BinDir is not on PATH; add it to your user PATH variable"
  }
  $version = (Get-Content $stamp -Raw | ConvertFrom-Json).productVersion
  Write-Output "installed $version -> $target, web -> $WebRoot"
} finally {
  Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
}
