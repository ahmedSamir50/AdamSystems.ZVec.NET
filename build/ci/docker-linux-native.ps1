# Host entry (Windows): build linux-x64 natives via Docker for simulate-pack SkipDownload.
# Matches GHA build-native.yml Unix recipe + LF strip for Windows core.autocrlf mounts.
#
# Usage (repo root):
#   powershell -NoProfile -File build/ci/docker-linux-native.ps1
#
# Do NOT use Git Bash to rewrite /src — MSYS mangles -w /src.

[CmdletBinding()]
param(
  [string] $RepoRoot = ""
)

$ErrorActionPreference = "Stop"
if (-not $RepoRoot) {
  $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path
}

$inner = Join-Path $RepoRoot "build/ci/docker-linux-native-inner.sh"
if (-not (Test-Path $inner)) { throw "Missing $inner" }

# Ensure inner script is LF (Windows editors may save CRLF)
$t = [IO.File]::ReadAllText($inner) -replace "`r`n", "`n" -replace "`r", "`n"
[IO.File]::WriteAllText($inner, $t)

$mount = ($RepoRoot -replace '\\', '/')
if ($mount -match '^[A-Za-z]:') {
  $mount = "/" + $mount.Substring(0, 1).ToLower() + $mount.Substring(2)
}
# Docker Desktop on Windows accepts D:/path form as well
$mountWin = ($RepoRoot -replace '\\', '/')

Write-Host "Docker linux-x64 native: mount $mountWin -> /src"
Write-Host "Inner: source on mount (git submodules), build dir /tmp (no Windows .o writes)."
Write-Host "Wipe host build-linux-x64 if present (legacy path)..."
$buildDir = Join-Path $RepoRoot "src/Native/ZVec.Native/build-linux-x64"
if (Test-Path $buildDir) {
  cmd /c "rmdir /s /q `"$buildDir`"" 2>$null
}

# Named container so we can inspect/kill; --rm still removes on exit
$cname = "zvec-linux-native-prove"
$prevEap = $ErrorActionPreference
$ErrorActionPreference = "Continue"
docker rm -f $cname 2>&1 | Out-Null
$ErrorActionPreference = $prevEap
$global:LASTEXITCODE = 0

docker run --name $cname --rm `
  -v "${mountWin}:/src" `
  -w /src `
  -e CMAKE_POLICY_VERSION_MINIMUM=3.5 `
  ubuntu:24.04 `
  bash /src/build/ci/docker-linux-native-inner.sh

if ($LASTEXITCODE -ne 0) {
  throw "docker-linux-native failed with exit $LASTEXITCODE"
}

$so = Join-Path $RepoRoot "_sim_natives/zvec-native-linux-x64/libzvec_c_api.so"
if (-not (Test-Path $so)) { throw "Missing output $so" }
$f = Get-Item $so
Write-Host "OK: $($f.FullName) size=$($f.Length) mtime=$($f.LastWriteTime)"

# Do not leave version-fallback / sed dirt in the submodule pointer
$zvec = Join-Path $RepoRoot "src/Native/ZVec.Native/external/zvec"
Write-Host "Resetting submodule to clean v0.7.0..."
git -C $zvec reset --hard v0.7.0 | Out-Null
git -C $zvec clean -ffdx | Out-Null
# Nested thirdparties get LF-stripped / patched during configure; reset those too
$prevEap2 = $ErrorActionPreference
$ErrorActionPreference = "Continue"
git -C $zvec submodule foreach --recursive "git reset --hard; git clean -ffdx" 2>&1 | Out-Null
$ErrorActionPreference = $prevEap2
Write-Host "DOCKER_LINUX_NATIVE_DONE"
