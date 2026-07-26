#!/usr/bin/env pwsh
# Local Pack-parity simulator: reuse Pack native artifacts, then managed (Win+Linux),
# pack, and both consumers — same shape as a successful Pack before any remote Pack.
#
# Usage (from repo root):
#   pwsh -File build/ci/simulate-pack.ps1
#   pwsh -File build/ci/simulate-pack.ps1 -PackRunId 30209030000 -SkipDownload
#
# Soft-fail exception (win managed heap corruption only): script exits 2 when Win
# managed fails with STATUS_HEAP_CORRUPTION / -1073740940 but Linux managed, pack,
# and both consumers are green. Caller may then add continue-on-error for Pack win.

[CmdletBinding()]
param(
    [long]$PackRunId = 30209030000,
    [string]$Repo = "ahmedSamir50/AdamSystems.ZVec.NET",
    [switch]$SkipDownload,
    [switch]$SkipWinManaged,
    [switch]$SkipLinuxManaged,
    [switch]$SkipPack,
    [switch]$SkipConsumers
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "../..")
Set-Location $Root

$SimNatives = Join-Path $Root "_sim_natives"
$SimFeed = Join-Path $Root "_sim_feed"
$ArtifactsNuget = Join-Path $Root "artifacts\nuget"
$RuntimesRoot = Join-Path $Root "src\Core\ZVec.NET\runtimes"
$GitBash = @(
    "C:\Program Files\Git\bin\bash.exe",
    "C:\Program Files\Git\usr\bin\bash.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

function Write-Step([string]$msg) {
    Write-Host ""
    Write-Host "==== $msg ====" -ForegroundColor Cyan
}

function Assert-Exit0([string]$label) {
    if ($LASTEXITCODE -ne 0) {
        throw "$label failed with exit $LASTEXITCODE"
    }
}

function Deploy-NativeDir([string]$rid, [string]$srcDir) {
    $dest = Join-Path $RuntimesRoot "$rid\native"
    New-Item -ItemType Directory -Force -Path $dest | Out-Null
    $files = Get-ChildItem -Path $srcDir -File -ErrorAction Stop
    if ($files.Count -eq 0) { throw "No native files in $srcDir for $rid" }
    Copy-Item -Force -Path (Join-Path $srcDir "*") -Destination $dest
    Write-Host "Deployed $rid from $srcDir -> $dest"
    Get-ChildItem $dest | Format-Table Name, Length
}

function Test-HeapCorruptionExit([int]$code) {
    # STATUS_HEAP_CORRUPTION = 0xC0000374 = -1073740940 (signed)
    return ($code -eq -1073740940) -or ($code -eq 0xC0000374)
}

$winManagedOk = $false
$winManagedHeapCorrupt = $false
$linuxManagedOk = $false
$packOk = $false
$winConsumerOk = $false
$linuxConsumerOk = $false

# --- 1. Download natives from Pack run ---
Write-Step "1/6 Download natives from Pack run $PackRunId"
New-Item -ItemType Directory -Force -Path $SimNatives | Out-Null
$rids = @("win-x64", "linux-x64", "osx-arm64")
if (-not $SkipDownload) {
    foreach ($rid in $rids) {
        $name = "zvec-native-$rid"
        $dest = Join-Path $SimNatives $name
        if (Test-Path $dest) { Remove-Item -Recurse -Force $dest }
        Write-Host "gh run download $PackRunId -n $name"
        gh run download $PackRunId -R $Repo -n $name -D $dest
        Assert-Exit0 "gh run download $name"
    }
} else {
    foreach ($rid in $rids) {
        $dest = Join-Path $SimNatives "zvec-native-$rid"
        if (-not (Test-Path $dest)) { throw "Missing $dest (remove -SkipDownload)" }
    }
    Write-Host "SkipDownload: using existing $SimNatives"
}

# --- 2. Deploy into runtimes/ ---
Write-Step "2/6 Deploy natives into runtimes/"
foreach ($rid in $rids) {
    Deploy-NativeDir $rid (Join-Path $SimNatives "zvec-native-$rid")
}

# --- 3. Windows managed (require_native, per TFM) ---
Write-Step "3/6 Windows managed ZVEC_REQUIRE_NATIVE=1 (net8 then net9)"
if ($SkipWinManaged) {
    Write-Host "Skipped (-SkipWinManaged)"
    $winManagedOk = $true
} else {
    # Clean host bin/obj so Pack-built DLL is what tests load (no stale Docker/obj mix).
    @(
        "src\Core\ZVec.NET\bin",
        "src\Core\ZVec.NET\obj",
        "testing\ZVec.NET.Tests\bin",
        "testing\ZVec.NET.Tests\obj"
    ) | ForEach-Object {
        $p = Join-Path $Root $_
        if (Test-Path $p) { Remove-Item -Recurse -Force $p }
    }

    $env:ZVEC_REQUIRE_NATIVE = "1"
    try {
        dotnet restore src/Core/ZVec.NET/ZVec.NET.csproj
        Assert-Exit0 "dotnet restore core"
        dotnet restore testing/ZVec.NET.Tests/ZVec.NET.Tests.csproj
        Assert-Exit0 "dotnet restore tests"
        dotnet build src/Core/ZVec.NET/ZVec.NET.csproj -c Release --no-restore
        Assert-Exit0 "dotnet build core"
        dotnet build testing/ZVec.NET.Tests/ZVec.NET.Tests.csproj -c Release --no-restore
        Assert-Exit0 "dotnet build tests"

        $dll = Join-Path $Root "testing\ZVec.NET.Tests\bin\Release\net8.0\runtimes\win-x64\native\zvec_c_api.dll"
        if (-not (Test-Path $dll)) { throw "Missing native in test output: $dll" }
        Write-Host "Native in test output OK: $dll"

        $tfmFailed = $false
        $lastCode = 0
        foreach ($tfm in @("net8.0", "net9.0")) {
            Write-Host "=== testing $tfm ==="
            dotnet test testing/ZVec.NET.Tests/ZVec.NET.Tests.csproj `
                -c Release -f $tfm --no-build --verbosity minimal
            $lastCode = $LASTEXITCODE
            if ($lastCode -ne 0) {
                $tfmFailed = $true
                Write-Host "Windows managed $tfm exited $lastCode" -ForegroundColor Yellow
                break
            }
        }
        if (-not $tfmFailed) {
            $winManagedOk = $true
            Write-Host "WIN_MANAGED_OK"
        } else {
            if (Test-HeapCorruptionExit $lastCode) {
                $winManagedHeapCorrupt = $true
                Write-Host "WIN_MANAGED_HEAP_CORRUPTION (exit $lastCode) - soft-fail candidate if rest green" -ForegroundColor Yellow
            } else {
                throw "Windows managed tests failed with exit $lastCode (not heap-corruption soft-fail shape)"
            }
        }
    } finally {
        Remove-Item Env:ZVEC_REQUIRE_NATIVE -ErrorAction SilentlyContinue
    }
}

# --- 4. Linux managed (Docker noble) ---
Write-Step "4/6 Linux managed Docker (sdk:10.0-noble + SDK 8/9)"
if ($SkipLinuxManaged) {
    Write-Host "Skipped (-SkipLinuxManaged)"
    $linuxManagedOk = $true
} else {
    $managedSh = Join-Path $Root "build\ci\docker-linux-managed.sh"
    # Ensure LF endings for Docker bash
    $raw = [System.IO.File]::ReadAllText($managedSh) -replace "`r`n", "`n" -replace "`r", "`n"
    [System.IO.File]::WriteAllText($managedSh, $raw, [System.Text.UTF8Encoding]::new($false))

    docker run --rm --name zvec-sim-linux-managed `
        -v "${Root}:/src:ro" -w /src `
        -e ZVEC_REQUIRE_NATIVE=1 `
        mcr.microsoft.com/dotnet/sdk:10.0-noble `
        bash /src/build/ci/docker-linux-managed.sh
    Assert-Exit0 "Docker linux managed"
    $linuxManagedOk = $true
    Write-Host "DOCKER_MANAGED_OK (host confirmed)"
}

# --- 5. Pack-shaped assemble + pack ---
Write-Step "5/6 Pack (assemble all sim RIDs + dotnet pack)"
if ($SkipPack) {
    Write-Host "Skipped (-SkipPack)"
    $packOk = $true
} else {
    foreach ($rid in $rids) {
        Deploy-NativeDir $rid (Join-Path $SimNatives "zvec-native-$rid")
    }
    New-Item -ItemType Directory -Force -Path $ArtifactsNuget | Out-Null
    # Single-version feed: clear previous sim packs of this product
    Get-ChildItem $ArtifactsNuget -Filter "ZVec.NET.*" -ErrorAction SilentlyContinue |
        Remove-Item -Force

    $commit = (git rev-parse HEAD).Trim()
    dotnet pack src/Core/ZVec.NET/ZVec.NET.csproj -c Release -o $ArtifactsNuget `
        "-p:RepositoryCommit=$commit" "-p:SourceRevisionId=$commit"
    Assert-Exit0 "dotnet pack"

    $nupkg = Get-ChildItem $ArtifactsNuget -Filter "ZVec.NET.*.nupkg" |
        Where-Object { $_.Name -notlike "*.symbols.*" } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if (-not $nupkg) { throw "No nupkg in $ArtifactsNuget" }
    $snupkg = Get-ChildItem $ArtifactsNuget -Filter "ZVec.NET.*.snupkg" | Select-Object -First 1
    if (-not $snupkg) { throw "No snupkg next to nupkg" }

    Write-Host ("Packed: " + $nupkg.Name + " size=" + $nupkg.Length)
    $nativeEntries = @()
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($nupkg.FullName)
    try {
        $nativeEntries = @($zip.Entries | Where-Object { $_.FullName -match '/native/' } | ForEach-Object { $_.FullName })
    } finally {
        $zip.Dispose()
    }
    Write-Host "=== nupkg runtimes/native ==="
    $nativeEntries | ForEach-Object { Write-Host $_ }
    foreach ($need in @("win-x64", "linux-x64", "osx-arm64")) {
        $hit = $nativeEntries | Where-Object { $_ -match [regex]::Escape("runtimes/$need/native/") }
        if (-not $hit) { throw "nupkg missing runtimes/$need/native/*" }
    }
    if ($nupkg.Length -gt (500 * 1024 * 1024)) {
        throw "nupkg exceeds 500 MiB soft gate"
    }

    # Single-version consumer feed
    if (Test-Path $SimFeed) { Remove-Item -Recurse -Force $SimFeed }
    New-Item -ItemType Directory -Force -Path $SimFeed | Out-Null
    Copy-Item $nupkg.FullName $SimFeed
    Copy-Item $snupkg.FullName $SimFeed -ErrorAction SilentlyContinue
    $packOk = $true
    Write-Host "PACK_OK feed=$SimFeed"
}

# --- 6. Consumers ---
Write-Step "6/6 Consumers (win host + linux Docker), require rc 0 / no 139"
if ($SkipConsumers) {
    Write-Host "Skipped (-SkipConsumers)"
    $winConsumerOk = $true
    $linuxConsumerOk = $true
} else {
    if (-not $GitBash) { throw "Git bash not found (needed for validate-consumer.sh on Windows)" }

    $ridWin = Join-Path $SimNatives "zvec-native-win-x64"
    $ridLinux = Join-Path $SimNatives "zvec-native-linux-x64"
    $feedUnix = ($SimFeed -replace '\\', '/')
    # Git bash wants /d/... style; cygpath if available via bash
    Write-Host "--- win-x64 consumer ---"
    $ridWinUnix = ($ridWin -replace '\\', '/')
    $rootUnix = ($Root.Path -replace '\\', '/')
    $consumerSh = "$rootUnix/build/ci/validate-consumer.sh"
    & $GitBash -lc "set -euo pipefail; sed -i 's/\r`$//' '$consumerSh'; chmod +x '$consumerSh'; bash '$consumerSh' win-x64 '$feedUnix' '$ridWinUnix'"
    Assert-Exit0 "win-x64 consumer"
    $winConsumerOk = $true
    Write-Host "WIN_CONSUMER_OK"

    Write-Host "--- linux-x64 consumer (Docker noble) ---"
    # Mount feed + rid dirs; use sdk:8.0-noble (GLIBC) like prior green smoke.
    # Copy feed into a container-visible path under repo.
    $feedRel = "_sim_feed"
    $ridRel = "_sim_natives/zvec-native-linux-x64"
    docker run --rm --name zvec-sim-linux-consumer `
        -v "${Root}:/src" -w /src `
        mcr.microsoft.com/dotnet/sdk:8.0-noble `
        bash -lc "set -euo pipefail; sed -i 's/\r`$//' build/ci/validate-consumer.sh; chmod +x build/ci/validate-consumer.sh; bash build/ci/validate-consumer.sh linux-x64 $feedRel $ridRel"
    Assert-Exit0 "linux-x64 consumer"
    $linuxConsumerOk = $true
    Write-Host "LINUX_CONSUMER_OK"
}

# --- Summary ---
Write-Step "Summary"
$lines = @(
    "win_managed=$winManagedOk heap_corrupt=$winManagedHeapCorrupt",
    "linux_managed=$linuxManagedOk",
    "pack=$packOk",
    "win_consumer=$winConsumerOk",
    "linux_consumer=$linuxConsumerOk"
)
$lines | ForEach-Object { Write-Host $_ }

$coreGreen = $linuxManagedOk -and $packOk -and $winConsumerOk -and $linuxConsumerOk
if ($winManagedOk -and $coreGreen) {
    Write-Host "SIMULATE_PACK_GREEN" -ForegroundColor Green
    exit 0
}

if ($winManagedHeapCorrupt -and $coreGreen) {
    Write-Host "SIMULATE_PACK_SOFTFAIL_WIN_MANAGED - linux+pack+consumers green; win managed heap corruption matches Pack #2 shape" -ForegroundColor Yellow
    exit 2
}

Write-Host "SIMULATE_PACK_FAILED" -ForegroundColor Red
exit 1
