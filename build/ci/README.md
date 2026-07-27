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
| `patches/*.patch` | CI-only zvec workarounds (not pushed to Alibaba): version fallback 0.5.1 ([#621](https://github.com/alibaba/zvec/issues/621)), Arrow MSVC/Ninja/pcg, FastPFOR MSVC ARM64 SIMDe, iOS dual-STATIC OUTPUT_NAME, Catalyst Lz4/Arrow macabi + RocksDB `HAS_ARMV8_CRC` |

## Workflows

| Workflow | Typical triggers | Publishes to nuget.org? |
|----------|------------------|-------------------------|
| `build-managed.yml` | PRs (+ manual) | No — core + tests only (not samples) |
| `build-native.yml` / `build-native-mobile.yml` | PRs with path filters (+ manual) | No |
| `build-native-try-optional.yml` | Manual only — **win-arm64** soft RID | No |
| `build-native-try-catalyst.yml` | Manual only — **maccatalyst-arm64** (hard on try path) | No |
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
| Soft RID natives (`win-arm64`, `maccatalyst-arm64`) | No (best-effort) | Soft-fail; ship if present |
| Trusted Publishing / nuget.org push | No | Publish only |

**Pack order:** desktop natives → managed (`require_native`) → mobile natives → pack (asserts HARD RIDs; stamps `RepositoryCommit`) → consumers. Soft RIDs must not block Pack.

**Standalone managed** (PR): no native download; integration tests Skip if the RID binary is missing. Unit tests still gate the job.

Samples live under `samples/ZVec.NET.Samples.slnx` and are never built by these workflows.

## RID ship gate

Consumer-facing matrix: [README.md — Native RIDs](../../README.md#native-rids-nuget-runtimes).

| RID | Workflow matrix | Runner | Gate (beta.3.2) |
|-----|-----------------|--------|-----------------|
| `win-x64`, `linux-x64` | `build-native.yml` HARD | `windows-latest` / `ubuntu-latest` | Pack-required |
| `osx-arm64` | HARD | Apple Silicon (`macos-latest`) | Pack-required |
| `linux-arm64` | HARD | `ubuntu-24.04-arm` | Pack-required |
| `osx-x64` | HARD | `macos-15-intel` | Pack-required |
| `android-arm64`, `android-x64` | mobile HARD | NDK | Pack-required |
| `ios-arm64`, `iossimulator-arm64` | mobile HARD | macOS + Xcode | Pack-required |
| `maccatalyst-arm64` | mobile SOFT | macOS + Xcode | Best-effort in nupkg; HARD next release |
| `win-arm64` | desktop SOFT | MSVC amd64→arm64 | Not pack-required (#622) |

**Try optional only:** `win-arm64`. **Try Catalyst only:** `maccatalyst-arm64` (hard-fail on that try path).

### Patch ↔ RID map (`patches/`)

| Patch / step | RID(s) |
|--------------|--------|
| `zvec-version-fallback-0.5.1.patch` | All ([#621](https://github.com/alibaba/zvec/issues/621)) |
| `zvec-arrow-msvc-ninja.patch` | Windows |
| `zvec-fastpfor-msvc-arm64-simde.patch` | `win-arm64` |
| `zvec-arrow-pcg-msvc-arm64.patch` | `win-arm64` |
| Host win64 / osx `protoc` download | `win-arm64`, Android, iOS/Catalyst |
| `zvec-ios-static-output-name.patch` | iOS / simulator |
| `zvec-lz4-maccatalyst.patch`, `zvec-arrow-maccatalyst.patch`, `zvec-rocksdb-maccatalyst-crc.patch` | `maccatalyst-arm64` |

## Branch / tag cheat sheet

```text
development  → daily PRs
main         → stable trunk (cut releases from here)
release/1.0  → 1.0.x maintenance (hotfixes + tags)
tag v1.0.0-beta.3.2  → nuget.org + GitHub Packages (Version 1.0.0-beta.3.2+zvec.0.5.1; Publish requires same-SHA green Pack or Packs inline)
```

**GitHub Packages:** Publish dual-pushes `.nupkg` (not `.snupkg`) to `nuget.pkg.github.com/{owner}`. Primary install remains nuget.org; optional consumers need a PAT with `read:packages` — see [CONTRIBUTING.md](../../CONTRIBUTING.md).

Full policy: [CONTRIBUTING.md](../../CONTRIBUTING.md) → Branching & releases.

**Local win-x64:** prefer `src/Native/ZVec.Native/_build_and_deploy.bat` (unchanged).
