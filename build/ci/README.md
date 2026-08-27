# CI helpers (Epic E21)

| Script | Purpose |
|--------|---------|
| `deploy-native.sh` / `deploy-native.ps1` | Copy a built `zvec_c_api` into `src/Core/ZVec.NET/runtimes/{rid}/native/` |
| `build-android.sh` | NDK CMake build → `android-arm64` / `android-x64` |
| `build-ios.sh` | Xcode CMake build → `ios-*` / `maccatalyst-*` (macOS only) |
| `validate-consumer.sh` | Clean `dotnet new` app + restore local `.nupkg` + create collection smoke |
| `simulate-pack.ps1` | **Mandatory local Pack-parity gate** before remote Pack/tag: reuse Pack native artifacts → Win+Docker Linux managed (`ZVEC_REQUIRE_NATIVE=1`) → pack → win+linux consumers (rc 0) |
| `docker-linux-managed.sh` | Helper for `simulate-pack.ps1` Linux managed suite (`sdk:10.0-noble` + SDK 8/9 AppHost packs; tests **net8.0** only) |
| `docker-linux-native.ps1` / `docker-linux-native.sh` | Local **linux-x64** `zvec_c_api` via `ubuntu:24.04` (GHA-equivalent cmake). On Windows use the `.ps1` (explicit `D:/…:/src` mount). Inner script strips `\r` from scripts/makefiles under `external/zvec` because host `core.autocrlf=true` otherwise breaks snowball (`perl\r`); GHA Linux checkouts are already LF and do not need that. Build dir is container `/tmp` (object writes off the Windows mount); source stays mounted so nested git submodules still work for zvec’s thirdparty patches. |
| `verify-release-provenance.sh` | After a tag: assert Pack `head_sha` == tag commit, Pack `conclusion=success`, optional nuspec commit check (needs `gh` + git; no secrets) |
| `patches/*.patch` | CI-only zvec workarounds (not pushed to Alibaba): Arrow MSVC/Ninja/pcg, FastPFOR MSVC ARM64 SIMDe, iOS dual-STATIC OUTPUT_NAME, Catalyst Lz4/Arrow macabi + RocksDB `HAS_ARMV8_CRC`. Version is forced via `-DOVERRIDE_GIT_DESCRIBE=v0.7.0` in wrapper CMake (replaces the old 0.6.0 version-fallback patch). Apply with [`apply-native-patches.ps1`](apply-native-patches.ps1); never commit into the submodule. |
| `apply-native-patches.ps1` | Local mirror of `build-native.yml` patch steps (Arrow/FastPFOR/pcg on Windows only). |

## Workflows

| Workflow | Typical triggers | Publishes to nuget.org? |
|----------|------------------|-------------------------|
| `build-managed.yml` | PRs (+ manual) | No — core + tests only (not samples) |
| `build-native.yml` / `build-native-mobile.yml` | PRs with path filters (+ manual; optional RIDs via dispatch inputs) | No |
| `pack.yml` | Manual `workflow_dispatch` only (+ `workflow_call`) | No (pack + smoke only) |
| `publish-nuget.yml` | tags `v*` + manual | **Yes** — nuget.org then GitHub Packages; commit must be on `release/*` |
| `docs.yml` | push to `main` / `release/*` (+ manual) | No |

**Linux teardown fix branch:** after changing init/teardown, run the full local matrix before opening a PR:

| Image | Gate |
|-------|------|
| `mcr.microsoft.com/dotnet/sdk:10.0-noble` | `docker-linux-managed.sh` (managed **net8.0**, `ZVEC_REQUIRE_NATIVE=1`) — matches GHA `ubuntu-latest` |
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
| Win managed require_native (**net8.0**) | Yes | Yes |
| Linux managed require_native (Docker noble) | Yes | Yes |
| osx-arm64 managed | No (no local macOS in sim) | Yes |
| `dotnet pack` + nupkg natives | Yes | Yes |
| win + linux consumers (rc 0) | Yes | Yes |
| Soft RID natives (`win-arm64`, `maccatalyst-arm64`) | No | Soft-fail; Catalyst included in Pack 30311588652 |
| Trusted Publishing / nuget.org push | No | Publish only |

**Pack order:** desktop natives → managed (`require_native`) → mobile natives → pack (asserts HARD RIDs; stamps `RepositoryCommit`) → consumers. Soft RIDs must not block Pack. **Managed tests = net8.0 only**; package still ships net8/net9/net10.

**Standalone managed** (PR): no native download; integration tests Skip if the RID binary is missing. Unit tests still gate the job.

Samples live under `samples/ZVec.NET.Samples.slnx` and are never built by these workflows.

## RID ship gate

Consumer-facing matrix: [README.md — Native RIDs](../../README.md#native-rids-nuget-runtimes).

| RID | Workflow matrix | Runner | Gate (beta.4) |
|-----|-----------------|--------|-----------------|
| `win-x64`, `linux-x64` | `build-native.yml` HARD | `windows-latest` / `ubuntu-latest` | Pack-required |
| `osx-arm64` | HARD | Apple Silicon (`macos-latest`) | Pack-required |
| `linux-arm64` | HARD | `ubuntu-24.04-arm` | Pack-required |
| `osx-x64` | HARD | `macos-15-intel` | Pack-required |
| `android-arm64`, `android-x64` | mobile HARD | NDK | Pack-required |
| `ios-arm64`, `iossimulator-arm64` | mobile HARD | macOS + Xcode | Pack-required |
| `maccatalyst-arm64` | mobile SOFT | macOS + Xcode | Included in Pack 30311588652; CI soft until HARD next release |
| `win-arm64` | desktop SOFT | MSVC amd64→arm64 | Not pack-required (#622) |

**Try optional only:** `win-arm64`. **Try Catalyst only:** `maccatalyst-arm64` (hard-fail on that try path).

### Patch ↔ RID map (`patches/`)

| Patch / step | RID(s) |
|--------------|--------|
| `-DOVERRIDE_GIT_DESCRIBE=v0.7.0` (wrapper CMake / GHA configure) | All |
| `zvec-arrow-msvc-ninja.patch` | Windows |
| `zvec-fastpfor-msvc-arm64-simde.patch` | `win-arm64` |
| `zvec-arrow-pcg-msvc-arm64.patch` | `win-arm64` |
| Host win64 / osx `protoc` download | `win-arm64`, Android, iOS/Catalyst |
| `zvec-ios-static-output-name.patch` | iOS / simulator (obsolete on zvec ≥0.7.0 — upstream fixed STATIC naming) |
| `zvec-lz4-maccatalyst.patch`, `zvec-arrow-maccatalyst.patch`, `zvec-rocksdb-maccatalyst-crc.patch` | `maccatalyst-arm64` only (`build-ios.sh`) |

## Branch / tag cheat sheet

```text
development  → daily PRs
main         → stable trunk (cut releases from here)
release/1.0  → 1.0.x maintenance (hotfixes + tags)
tag v1.0.0-beta.4  → nuget.org + GitHub Packages (Version 1.0.0-beta.4+zvec.0.6.0; Publish requires same-SHA green Pack or Packs inline)
```

**GitHub Packages:** Publish dual-pushes `.nupkg` (not `.snupkg`) to `nuget.pkg.github.com/{owner}`. Primary install remains nuget.org; optional consumers need a PAT with `read:packages` — see [CONTRIBUTING.md](../../CONTRIBUTING.md).

Full policy: [CONTRIBUTING.md](../../CONTRIBUTING.md) → Branching & releases.

**Local win-x64:** prefer `src/Native/ZVec.Native/_build_and_deploy.bat` (unchanged).
