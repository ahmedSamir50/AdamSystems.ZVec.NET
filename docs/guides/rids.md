# Native RIDs

Managed TFMs are `net8.0` / `net9.0` / `net10.0`. Natives ship under `runtimes/{rid}/native/`.

**Why some RIDs are missing:** not unfinished C# P/Invoke — building Alibaba zvec’s bundled C++ third parties. Engineering detail: [build/ci/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/build/ci/README.md#rid-ship-gate).

## Pack-required in `1.0.0-beta.4`

| RID | Native file | Status |
|-----|-------------|--------|
| `win-x64` | `zvec_c_api.dll` | Desktop HARD |
| `linux-x64` | `libzvec_c_api.so` | Desktop HARD |
| `linux-arm64` | `libzvec_c_api.so` | Desktop HARD |
| `osx-arm64` | `libzvec_c_api.dylib` | Desktop HARD |
| `osx-x64` | `libzvec_c_api.dylib` | Desktop HARD |
| `android-arm64`, `android-x64` | `libzvec_c_api.so` | Mobile HARD |
| `ios-arm64`, `iossimulator-arm64` | `libzvec_c_api.dylib` | Mobile HARD |

CI remains **soft** for `maccatalyst-arm64` until a later release promotes it to pack-required HARD.

## Not yet shipped

| RID | Real reason |
|-----|-------------|
| `win-arm64` | MSVC ailego CMake skip while shared still required ([alibaba/zvec#622](https://github.com/alibaba/zvec/issues/622)) |

## Feature limits (not RID packaging)

| Item | Why |
|------|-----|
| **Blazor WebAssembly** | No native `zvec_c_api` RID |
| **HNSW-RaBitQ on ARM** | Upstream ISA (x86_64 + AVX2 only); SDK throws `PlatformNotSupportedException` |
| **DiskANN on non-Linux** | Upstream Linux-only; same SDK gate |

See [Concepts](../concepts/index.md) for index types and [Troubleshooting](troubleshooting.md) for load failures.
