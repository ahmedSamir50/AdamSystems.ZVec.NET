# CI helpers (Epic E21)

| Script | Purpose |
|--------|---------|
| `deploy-native.sh` / `deploy-native.ps1` | Copy a built `zvec_c_api` into `src/Core/ZVec.NET/runtimes/{rid}/native/` |
| `build-android.sh` | NDK CMake build → `android-arm64` / `android-x64` |
| `build-ios.sh` | Xcode CMake build → `ios-*` / `maccatalyst-*` (macOS only) |
| `validate-consumer.sh` | Clean `dotnet new` app + restore local `.nupkg` + create collection smoke |
| `simulate-pack.ps1` | **Mandatory local Pack-parity gate** before remote Pack/tag: reuse Pack native artifacts → Win+Docker Linux managed (`ZVEC_REQUIRE_NATIVE=1`) → pack → win+linux consumers (rc 0) |
| `docker-linux-managed.sh` | Helper for `simulate-pack.ps1` Linux managed suite (`sdk:10.0-noble` + SDK 8/9 AppHost packs) |
| `verify-release-provenance.sh` | After a tag: assert Pack `head_sha` == tag commit, Pack `conclusion=success`, optional nuspec commit check (needs `gh` + git; no secrets) |
| `patches/*.patch` | CI-only zvec workarounds (not pushed to Alibaba): version fallback 0.5.1 (shallow/no-tags; see [alibaba/zvec#621](https://github.com/alibaba/zvec/issues/621)), Arrow MSVC/Ninja/pcg, FastPFOR MSVC ARM64 SIMDe, legacy linux-aarch64 cross / osx-x64 march (unused while optional RIDs build on native runners), iOS dual-STATIC OUTPUT_NAME, Catalyst Lz4/Arrow macabi + RocksDB `HAS_ARMV8_CRC` |

## Workflows

| Workflow | Typical triggers | Publishes to nuget.org? |
|----------|------------------|-------------------------|
| `build-managed.yml` | PRs (+ manual) | No — core + tests only (not samples) |
| `build-native.yml` / `build-native-mobile.yml` | PRs with path filters (+ manual) | No |
| `build-native-try-optional.yml` | Manual only — **linux-arm64 + osx-x64** on native runners | No (fast optional RID check) |
| `build-native-try-catalyst.yml` | Manual only — **maccatalyst-arm64** only | No (fast Catalyst check) |
| `pack.yml` | Manual `workflow_dispatch` only (+ `workflow_call`) | No (pack + smoke only) |
| `publish-nuget.yml` | tags `v*` + manual | **Yes** — nuget.org then GitHub Packages; commit must be on `release/*` |
| `validate-consumer-rerun.yml` | Manual only | No |

**Linux teardown fix branch:** after changing init/teardown, run the full local matrix before opening a PR:

| Image | Gate |
|-------|------|
| `mcr.microsoft.com/dotnet/sdk:10.0-noble` | `docker-linux-managed.sh` (managed net8 + net9, `ZVEC_REQUIRE_NATIVE=1`) — matches GHA `ubuntu-latest` |
| `mcr.microsoft.com/dotnet/sdk:8.0-noble` | `validate-consumer.sh linux-x64` via `simulate-pack.ps1` step 6 |
| Newer rolling tag (e.g. `mcr.microsoft.com/dotnet/sdk:10.0`) | Re-run managed + consumer for forward compatibility |

```powershell
# From repo root (Windows host; Docker required for linux gates)
powershell -NoProfile -File build/ci/simulate-pack.ps1 -SkipDownload
```

Linux consumer smoke uses normal `Environment.Exit(0)` after Shutdown (Windows may still use `TerminateProcess`).

**Ship:** PR CI → merge → **local `simulate-pack.ps1` green** → tag `v*` (maintainer) → Publish reuses **same-SHA** green Pack or Packs inline. Do not use remote Pack as the first discovery of managed/consumer failures.

**Local sim vs GHA**

| Gate | Local `simulate-pack.ps1` | Remote Pack |
|------|---------------------------|-------------|
| Win managed require_native (net8 then net9) | Yes | Yes |
| Linux managed require_native (Docker noble) | Yes | Yes |
| osx-arm64 managed | No (no local macOS in sim) | Yes |
| `dotnet pack` + nupkg natives | Yes | Yes |
| win + linux consumers (rc 0) | Yes | Yes |
| Optional RID natives (win-arm64, …) | No (reuse prior artifacts / soft-fail) | Soft-fail |
| Trusted Publishing / nuget.org push | No | Publish only |

**Pack order:** desktop natives → managed tests with `require_native` → pack (stamps `RepositoryCommit`) → consumers. Pack stays gated on managed success. Mobile / optional desktop RIDs are soft-fail (`continue-on-error`).

**Standalone managed** (PR): no native download; integration tests Skip if the RID binary is missing. Unit tests still gate the job.

Samples live under `samples/ZVec.NET.Samples.slnx` and are never built by these workflows.

## RID ship gate

Consumer-facing matrix (supported / not yet / never): [README.md — Native RIDs](../../README.md#native-rids-nuget-runtimes).

Missing RIDs are blocked by **building zvec’s bundled C++ third parties** (Arrow, FastPFOR/SIMDe, Lz4, host `protoc` on some mobile/cross paths), not by managed P/Invoke. Prefer **native runners** that match the RID (no cross-compile / no foreign-arch slice) before filing upstream build bugs. A RID is “shipped” when CI is hard-green for that RID **and** pack always places the binary under `src/Core/ZVec.NET/runtimes/{rid}/native/`.

| RID | Workflow matrix | Runner | Gate today |
|-----|-----------------|--------|------------|
| `win-x64`, `linux-x64` | `build-native.yml` `optional: false` | `windows-latest` / `ubuntu-latest` | Required; pack + managed `require_native` |
| `osx-arm64` | `build-native.yml` `optional: false` | Apple Silicon (`macos-latest`) | Required; pack + managed `require_native` |
| `linux-arm64` | `build-native.yml` `optional: true` | **`ubuntu-24.04-arm`** (native aarch64) | Soft-fail; not pack-required |
| `osx-x64` | `build-native.yml` `optional: true` | **`macos-15-intel`** (native x86_64) | Soft-fail; not pack-required |
| `win-arm64` | `build-native.yml` `optional: true` | `windows-latest` (MSVC amd64→arm64 cross) | Soft-fail; not pack-required |
| `android-arm64`, `android-x64` | `build-native-mobile.yml` `continue-on-error: true` | NDK CI | Soft-fail; advertised when artifact present |
| `ios-arm64`, `iossimulator-arm64`, `maccatalyst-arm64` | `build-native-mobile.yml` `continue-on-error: true` | macOS + Xcode | Soft-fail; not pack-required |

**Try optional only:** Actions → **Try optional native RIDs** → Run workflow (or `build-native.yml` with `try_optional_only=true`). Builds only `linux-arm64` + `osx-x64` so you do not wait on the full desktop matrix.

### Patch ↔ RID map (`patches/`)

| Patch / step | RID(s) |
|--------------|--------|
| `zvec-version-fallback-0.5.1.patch` | All (shallow submodule / ABI version; upstream [alibaba/zvec#621](https://github.com/alibaba/zvec/issues/621)) |
| `zvec-arrow-msvc-ninja.patch` | Windows (Arrow + Ninja/MSVC) |
| `zvec-fastpfor-msvc-arm64-simde.patch` | `win-arm64` |
| `zvec-arrow-pcg-msvc-arm64.patch` | `win-arm64` (Arrow tree) |
| Host win64 / osx `protoc` download | `win-arm64`, Android, iOS/Catalyst |
| `zvec-arrow-linux-aarch64-cross.patch` | Legacy (x86→aarch64 cross); unused while `linux-arm64` uses `ubuntu-24.04-arm` |
| `zvec-osx-x64-march.patch` | Legacy (arm64 host → x86_64 slice); unused while `osx-x64` uses `macos-15-intel` |
| `zvec-ios-static-output-name.patch` | iOS / simulator |
| `zvec-lz4-maccatalyst.patch`, `zvec-arrow-maccatalyst.patch` | `maccatalyst-arm64` (+ applied from `build-ios.sh`) |
| `zvec-rocksdb-maccatalyst-crc.patch` | `maccatalyst-arm64` — force `HAS_ARMV8_CRC` (iOS already does; Darwin+macabi skipped that path) |

**Try Catalyst only:** Actions → **Build Native (try Catalyst)** → Run workflow. Builds only `maccatalyst-arm64` (no Android / iOS matrix).

To promote an optional RID: keep the job green, set `optional: false` / drop `continue-on-error`, ensure pack always assembles that artifact, bump `PackageReleaseNotes` + README.

## Branch / tag cheat sheet

```text
development  → daily PRs
main         → stable trunk (cut releases from here)
release/1.0  → 1.0.x maintenance (hotfixes + tags)
tag v1.0.0-beta.3.1  → nuget.org + GitHub Packages (Version 1.0.0-beta.3.1+zvec.0.5.1; Publish requires same-SHA green Pack or Packs inline)
```

**GitHub Packages:** Publish dual-pushes `.nupkg` (not `.snupkg`) to `nuget.pkg.github.com/{owner}`. Primary install remains nuget.org; optional consumers need a PAT with `read:packages` — see [CONTRIBUTING.md](../../CONTRIBUTING.md).

Full policy: [CONTRIBUTING.md](../../CONTRIBUTING.md) → Branching & releases.

**Local win-x64:** prefer `src/Native/ZVec.Native/_build_and_deploy.bat` (unchanged).
