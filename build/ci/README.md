# CI helpers (Epic E21)

| Script | Purpose |
|--------|---------|
| `deploy-native.sh` / `deploy-native.ps1` | Copy a built `zvec_c_api` into `src/Core/ZVec.NET/runtimes/{rid}/native/` |
| `build-android.sh` | NDK CMake build → `android-arm64` / `android-x64` |
| `build-ios.sh` | Xcode CMake build → `ios-*` / `maccatalyst-*` (macOS only) |
| `validate-consumer.sh` | Clean `dotnet new` app + restore local `.nupkg` + create collection smoke |

## Workflows

| Workflow | Typical triggers | Publishes to nuget.org? |
|----------|------------------|-------------------------|
| `build-managed.yml` | `main`, `development`, `release/**`, PRs | No |
| `build-native.yml` / `build-native-mobile.yml` | same (+ path filters) | No |
| `pack.yml` | `release/**`, tags `v*`, manual | No (pack + smoke only) |
| `publish-nuget.yml` | tags `v*` only | **Yes** — commit must be on `release/*` |

## Branch / tag cheat sheet

```text
development  → daily PRs
main         → stable trunk (cut releases from here)
release/1.0  → 1.0.x maintenance (hotfixes + tags)
tag v1.0.0-alpha.1  → first nuget.org ship (Version 1.0.0-alpha.1+zvec.0.5.1 in csproj)
```

Full policy: [CONTRIBUTING.md](../../CONTRIBUTING.md) → Branching & releases.

**Local win-x64:** prefer `src/Native/ZVec.Native/_build_and_deploy.bat` (unchanged).
