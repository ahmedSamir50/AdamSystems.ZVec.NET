# CI helpers (Epic E21)

| Script | Purpose |
|--------|---------|
| `deploy-native.sh` / `deploy-native.ps1` | Copy a built `zvec_c_api` into `src/Core/ZVec.NET/runtimes/{rid}/native/` |
| `build-android.sh` | NDK CMake build → `android-arm64` / `android-x64` |
| `build-ios.sh` | Xcode CMake build → `ios-*` / `maccatalyst-*` (macOS only) |
| `validate-consumer.sh` | Clean `dotnet new` app + restore local `.nupkg` + create collection smoke |
| `patches/*.patch` | CI-only zvec workarounds (not pushed to Alibaba): version fallback 0.5.1 (shallow/no-tags), Arrow MSVC/Ninja/pcg, FastPFOR MSVC ARM64 SIMDe, linux-aarch64 Arrow cross (+OPENSSL=OFF), osx-x64 march, iOS dual-STATIC OUTPUT_NAME, Catalyst Lz4/Arrow macabi |

## Workflows

| Workflow | Typical triggers | Publishes to nuget.org? |
|----------|------------------|-------------------------|
| `build-managed.yml` | `main`, `development`, `release/**`, PRs | No — core + tests only (not samples) |
| `build-native.yml` / `build-native-mobile.yml` | same (+ path filters) | No |
| `pack.yml` | `release/**`, tags `v*`, manual | No (pack + smoke only) |
| `publish-nuget.yml` | tags `v*` only | **Yes** — commit must be on `release/*` |

**Pack order:** desktop natives → managed tests with `require_native` (download `zvec-native-{rid}` into `runtimes/`, assert copy into test `bin/.../runtimes/`, `ZVEC_REQUIRE_NATIVE=1`, then test) → pack nupkg. Pack stays gated on managed success. Mobile / optional desktop RIDs are soft-fail (`continue-on-error`).

**Standalone managed** (push/PR): no native download; integration tests Skip if the RID binary is missing. Unit tests still gate the job.

Samples live under `samples/ZVec.NET.Samples.slnx` and are never built by these workflows.

## RID ship gate

Consumer-facing matrix (supported / not yet / never): [README.md — Native RIDs](../../README.md#native-rids-nuget-runtimes).

Missing RIDs are blocked by **cross-compiling zvec’s bundled third parties** (Arrow, FastPFOR/SIMDe, Lz4, host `protoc`), not by managed P/Invoke. A RID is “shipped” when CI is hard-green for that RID **and** pack always places the binary under `src/Core/ZVec.NET/runtimes/{rid}/native/`.

| RID | Workflow matrix | Gate today |
|-----|-----------------|------------|
| `win-x64`, `linux-x64`, `osx-arm64` | `build-native.yml` `optional: false` | Required; pack + managed `require_native` |
| `win-arm64`, `linux-arm64`, `osx-x64` | `build-native.yml` `optional: true` (`continue-on-error`) | Soft-fail; not pack-required |
| `android-arm64`, `android-x64` | `build-native-mobile.yml` `continue-on-error: true` | Soft-fail; advertised when artifact present |
| `ios-arm64`, `iossimulator-arm64`, `maccatalyst-arm64` | `build-native-mobile.yml` `continue-on-error: true` | Soft-fail; not pack-required |

### Patch ↔ RID map (`patches/`)

| Patch / step | RID(s) |
|--------------|--------|
| `zvec-version-fallback-0.5.1.patch` | All (shallow submodule / ABI version) |
| `zvec-arrow-msvc-ninja.patch` | Windows (Arrow + Ninja/MSVC) |
| `zvec-fastpfor-msvc-arm64-simde.patch` | `win-arm64` |
| `zvec-arrow-pcg-msvc-arm64.patch` | `win-arm64` (Arrow tree) |
| Host win64 / linux-x86_64 / osx `protoc` download | `win-arm64`, `linux-arm64`, Android, iOS/Catalyst |
| `zvec-arrow-linux-aarch64-cross.patch` | `linux-arm64` |
| `zvec-osx-x64-march.patch` | `osx-x64` |
| `zvec-ios-static-output-name.patch` | iOS / simulator |
| `zvec-lz4-maccatalyst.patch`, `zvec-arrow-maccatalyst.patch` | `maccatalyst-arm64` (+ applied from `build-ios.sh`) |

To promote an optional RID: keep the job green, set `optional: false` / drop `continue-on-error`, ensure pack always assembles that artifact, bump `PackageReleaseNotes` + README.

## Branch / tag cheat sheet

```text
development  → daily PRs
main         → stable trunk (cut releases from here)
release/1.0  → 1.0.x maintenance (hotfixes + tags)
tag v1.0.0-beta.1  → nuget.org beta ship (Version 1.0.0-beta.1+zvec.0.5.1 in csproj)
```


Full policy: [CONTRIBUTING.md](../../CONTRIBUTING.md) → Branching & releases.

**Local win-x64:** prefer `src/Native/ZVec.Native/_build_and_deploy.bat` (unchanged).
