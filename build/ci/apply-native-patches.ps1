# Apply CI-only native workarounds onto the Alibaba zvec checkout (and nested
# thirdparty git trees). Patches live under build/ci/patches/ — never commit
# the resulting dirt into external/zvec or its nested repos.
#
# Mirrors .github/workflows/build-native.yml (last-green development recipe +
# version-fallback 0.6.0 only). Do not add antlr/gflags/glog unless GHA fails.
#
# Usage (repo root):
#   powershell -NoProfile -File build/ci/apply-native-patches.ps1
#   powershell -NoProfile -File build/ci/apply-native-patches.ps1 -Platform Windows
#
# Idempotent: skips a patch when `git apply --check` fails (already applied).

[CmdletBinding()]
param(
  [ValidateSet("Windows", "Unix", "All")]
  [string] $Platform = $(if ($env:OS -match "Windows") { "Windows" } else { "Unix" })
)

$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "../..")
$zvec = Join-Path $root "src/Native/ZVec.Native/external/zvec"
$patches = Join-Path $root "build/ci/patches"

function Apply-Patch {
  param(
    [Parameter(Mandatory)] [string] $Repo,
    [Parameter(Mandatory)] [string] $PatchFile,
    [string] $Label = $PatchFile
  )
  if (-not (Test-Path $Repo)) { throw "Repo missing: $Repo" }
  $p = Join-Path $patches $PatchFile
  if (-not (Test-Path $p)) { throw "Patch missing: $p" }
  $prev = $ErrorActionPreference
  $ErrorActionPreference = "Continue"
  git -C $Repo apply --check $p 2>$null | Out-Null
  $check = $LASTEXITCODE
  $ErrorActionPreference = $prev
  if ($check -ne 0) {
    Write-Host "SKIP (already applied or N/A): $Label"
    return
  }
  git -C $Repo apply $p
  if ($LASTEXITCODE -ne 0) { throw "git apply failed: $Label" }
  Write-Host "APPLIED: $Label"
}

Write-Host "Applying native CI patches (Platform=$Platform) under $zvec"
Write-Host "Remember: wipe with submodule reset before committing the zvec pointer."

# All RIDs (same as GHA "Patch zvec version fallback")
Apply-Patch -Repo $zvec -PatchFile "zvec-version-fallback-0.6.0.patch"

# Windows only (same as GHA "Patch zvec CI workarounds (Windows)")
if ($Platform -eq "Windows" -or $Platform -eq "All") {
  Apply-Patch -Repo $zvec -PatchFile "zvec-arrow-msvc-ninja.patch"
  Apply-Patch -Repo $zvec -PatchFile "zvec-fastpfor-msvc-arm64-simde.patch"
  $arrow = Join-Path $zvec "thirdparty/arrow/apache-arrow-21.0.0"
  Apply-Patch -Repo $arrow -PatchFile "zvec-arrow-pcg-msvc-arm64.patch"
}

Write-Host "Done. Do not commit nested thirdparty dirt; only the parent submodule SHA for the clean tag."
