# ZVec.NET.Samples.Maui (.NET 10) — flagship edge RAG

Cross-platform proof for ZVec natives: **Windows, Android, iOS, Mac Catalyst**.

## Prerequisites

1. .NET 10 MAUI workload: `dotnet workload install maui`
2. Native `zvec_c_api` for the target RID under `src/Core/ZVec.NET/runtimes/{rid}/native/`  
   - Built by GitHub Actions (`build-native.yml` / `build-native-mobile.yml`) or local scripts in `build/ci/`
   - Wired via [`ZVec.Native.targets`](ZVec.Native.targets) (Debug **and** Release)

## Run

### Windows

```bash
dotnet build samples/ZVec.NET.Samples.Maui -f net10.0-windows10.0.19041.0
dotnet build samples/ZVec.NET.Samples.Maui -t:Run -f net10.0-windows10.0.19041.0
```

Requires `runtimes/win-x64/native/zvec_c_api.dll`.

### Android (device or emulator)

```bash
# Produce native (once):
#   ANDROID_NDK_HOME=... ./build/ci/build-android.sh arm64-v8a
#   ANDROID_NDK_HOME=... ./build/ci/build-android.sh x86_64   # emulator

dotnet build samples/ZVec.NET.Samples.Maui -f net10.0-android -c Debug
dotnet build samples/ZVec.NET.Samples.Maui -t:Run -f net10.0-android -c Debug
# Release:
dotnet build samples/ZVec.NET.Samples.Maui -t:Run -f net10.0-android -c Release
```

Requires `libzvec_c_api.so` in `runtimes/android-arm64` and/or `android-x64`.

### iOS / Mac Catalyst (macOS + Xcode)

```bash
./build/ci/build-ios.sh ios-arm64
./build/ci/build-ios.sh iossimulator-arm64
./build/ci/build-ios.sh maccatalyst-arm64

dotnet build samples/ZVec.NET.Samples.Maui -f net10.0-ios
dotnet build samples/ZVec.NET.Samples.Maui -f net10.0-maccatalyst
```

Physical iPhone runs need Apple signing; use the iOS device testing issue template for community reports.

## App behavior

- Collection path: `FileSystem.AppDataDirectory` + mmap
- Ingest paste/text or built-in T0 fixtures
- Ask → retrieve + Gemma 4 E2B + citations (EmbeddingGemma + chat concurrent in LM Studio)
- Status bar probes both LM Studio models and dataset download progress
