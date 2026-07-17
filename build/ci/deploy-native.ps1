# Copy a built zvec_c_api shared library into src/Core/ZVec.NET/runtimes/{rid}/native/
param(
    [Parameter(Mandatory = $true)][string]$Rid,
    [Parameter(Mandatory = $true)][string]$SourcePath
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$DestDir = Join-Path $Root "src/Core/ZVec.NET/runtimes/$Rid/native"

if (-not (Test-Path $SourcePath)) {
    throw "Native library not found: $SourcePath"
}

New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
Copy-Item -Force $SourcePath $DestDir
Write-Host "Deployed $(Split-Path $SourcePath -Leaf) -> $DestDir"
Get-ChildItem $DestDir
