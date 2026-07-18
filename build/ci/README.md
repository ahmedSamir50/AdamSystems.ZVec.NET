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

## Branch / tag cheat sheet

```text
development  → daily PRs
main         → stable trunk (cut releases from here)
release/1.0  → 1.0.x maintenance (hotfixes + tags)
tag v1.0.0-beta.1  → nuget.org beta ship (Version 1.0.0-beta.1+zvec.0.5.1 in csproj)
```


Full policy: [CONTRIBUTING.md](../../CONTRIBUTING.md) → Branching & releases.

**Local win-x64:** prefer `src/Native/ZVec.Native/_build_and_deploy.bat` (unchanged).
