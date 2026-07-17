# CI helpers (Epic E21)

| Script | Purpose |
|--------|---------|
| `deploy-native.sh` / `deploy-native.ps1` | Copy a built `zvec_c_api` into `src/Core/ZVec.NET/runtimes/{rid}/native/` |
| `build-android.sh` | NDK CMake build → `android-arm64` / `android-x64` |
| `build-ios.sh` | Xcode CMake build → `ios-*` / `maccatalyst-*` (macOS only) |
| `validate-consumer.sh` | Clean `dotnet new` app + restore local `.nupkg` + create collection smoke |

Workflows (repo root `.github/workflows/`):

- `build-native.yml` — desktop RIDs
- `build-native-mobile.yml` — Android + Apple mobile
- `build-managed.yml` — `dotnet test`
- `pack.yml` — assemble artifacts + pack + consumer validate
- `publish-nuget.yml` — tag `v*` → nuget.org (Trusted Publishing)

**Local win-x64:** prefer `src/Native/ZVec.Native/_build_and_deploy.bat` (unchanged).
