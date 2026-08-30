# Changelog

All notable changes to ZVec.NET are documented in this file.

## [Unreleased]

### Planned

- Promote `maccatalyst-arm64` from soft CI (included in Pack [30311588652](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/actions/runs/30311588652)) to pack-required HARD
- Bind `zvec_collection_flush` as `Flush` / `FlushAsync` next to `Optimize`
- Bind DiskANN I/O-backend diagnostics (`zvec_get_io_backend_type` / `_name` / `_description`) on `IZvecFactory`
- Restrict `V070ApiIntegrationTests` IVF-RaBitQ skip to **Linux x64** (Linux ARM64 currently can run then hit the RaBitQ Arm gate)
- Optional CI minutes: Pack-only native workflows **only if** `workflow_dispatch` (or `try_*` inputs) stay usable, and `docs.yml` still deploys on `v*` so mike `latest` follows the tag

### Changed

- Managed tests (CI + `simulate-pack` / Docker) run on **net8.0** only; package still ships `net8.0` / `net9.0` / `net10.0`

## [1.0.0-beta.6] - 2026-08-27

Native pin: **zvec 0.7.0** (was 0.6.0).

### Added

- **IVF-RaBitQ**: `ZVecIvfRabitqIndexParam`, `ZVecIvfRabitqQueryParams`, ODM mapping for `[ZVecVector(Index = ZVecIndexType.IvfRabitq)]`
- **DocIterator**: `IZvecCollectionQueries.Iterate` / `IZvecCollection<T>.Iterate` returning disposable `ZVecDocIterator` (snapshot semantics)
- **FTS ngram**: `ZVecFtsTokenizer.Ngram`, `ZVecFtsExtraParams` keys `ngram_min`, `ngram_max`, `token_chars`
- **Vamana two-pass build**: `ZVecVamanaIndexParam.TwoPassBuild`

### Changed

- `ZVecNativeAbi` minimum → **0.7.0**
- DiskANN platform gate: Linux (any arch) + macOS ARM64 (Windows still blocked)
- CI/version: `-DOVERRIDE_GIT_DESCRIBE=v0.7.0` replaces the 0.6.0 version-fallback patch

### Upgrade

- **Required from ≤`1.0.0-beta.5.x`.** ABI floor is now `0.7.0`; packages with `+zvec.0.6.0` natives are incompatible with this managed assembly and vice versa.

### Known limitations (unchanged)

- HNSW-RaBitQ index **create** still throws `NotSupportedException` (C API gap)
- `QueryGroupBy` / `QueryGroupByAsync` execute still throws (no `zvec_collection_group_by_query` export)

## [1.0.0-beta.5] - 2026-08-11

Native pin unchanged: **zvec 0.6.0**.

### Added

- **Native AOT / IL Trimming compatible**: all typed ODM public APIs (`IZvecCollection<T>`, `ZVecCollection<T>`, `ZVecMapper`, `ZVecTypeModel`, `ZVecCollectionSchemaBuilder.From<T>()`, `ZVecExpressionFilter.Translate<T>()`, `AddZVecCollection<T>()`) annotated with `[DynamicallyAccessedMembers]`
- `<IsAotCompatible>true</IsAotCompatible>` and `<EnableTrimAnalyzer>true</EnableTrimAnalyzer>` in csproj for build-time verification

### Impact

- **Zero breaking changes**: annotations are additive metadata
- **Typed ODM constraint**: `IZvecCollection<T>` / `ZVecCollection<T>` now require `where T : class, new()` (already required at runtime for `Fetch` / `Query` deserialization)
- **Consumer POCOs do not need annotations** — the SDK preserves required members
- Consumers can now `dotnet publish -c Release /p:PublishAot=true` with **zero IL2070/IL2091 warnings**

## [1.0.0-beta.4] - 2026-07-29

Native pin: **zvec 0.6.0** (was 0.5.1).

### Migration

- **Upgrade required from ≤`1.0.0-beta.3.x`.** ABI floor is now `0.6.0`; packages with `+zvec.0.5.1` natives are incompatible with this managed assembly and vice versa.
- Prefer `dotnet add package ZVec.NET --version 1.0.0-beta.4` (or latest beta.4+).

### Added

- `EnableRotate` on Flat/HNSW/IVF/Vamana/DiskANN index params (INT8/INT4 random rotation)
- `ZVecFlatQueryParams`, `ZVecVamanaQueryParams`, `ZVecDiskAnnQueryParams`
- Extended `ZVecHnswQueryParams` / `ZVecIvfQueryParams` (`Radius`, `IsLinear`, `IsUsingRefiner`, IVF `ScaleFactor`)
- Multi-query sub-queries honor per-query `QueryParams`
- Internal `NativeGroupByQueryBuilder` for `zvec_group_by_vector_query_*` parity (execution still blocked)
- Honest `NotSupportedException` for HNSW-RaBitQ index create via C API (upstream `zvec_index_params_create` has no `HNSW_RABITQ` case)

### Changed

- Native submodule pin → official `v0.6.0`
- `ZVecNativeAbi` minimum → `0.6.0`
- DiskANN messaging: Linux-only; libaio optional (upstream dlopen + pread fallback)
- FTS index builder uses `ZVecNativeStrings` for tokenizer/filter literals
- CI version-fallback patch retargeted to `0.6.0`

### Fixed / inherited from upstream 0.6.0

- FTS, collection DDL, DiskANN I/O, IVF, and related native fixes ship with the rebuilt `zvec_c_api`
- Group-by execution blocked: Python reaches C++ `Collection::GroupByQuery` via pybind; official `c_api.h` has builders only (no `zvec_collection_group_by_query`)

### Known limitations

- `QueryGroupBy` / `QueryGroupByAsync` remain `[Obsolete]` and throw `NotSupportedException`
- HNSW-RaBitQ index create via official C API is unsupported until upstream adds a `HNSW_RABITQ` create/set path (managed SDK throws)

## [1.0.0-beta.3.2] - 2026-07-28

Native pin unchanged: **zvec 0.5.1**.

### Fixed

- Linux teardown SIGSEGV (exit 139): stop double-freeing log config after `zvec_config_data_set_log_config` transfers ownership ([alibaba/zvec#619](https://github.com/alibaba/zvec/issues/619))
- Restore native collection close/shutdown on Linux (`ZVecNativeTeardownPolicy.Auto` no longer suppresses teardown)
- Linux consumer smoke uses normal `Environment.Exit(0)` after Shutdown (Windows still uses `TerminateProcess` where needed)
- Apple host `protoc` path pollution in `build-ios.sh` (stdout → exit 127)
- Mac Catalyst RocksDB ARM CRC link via CI patch (`HAS_ARMV8_CRC` for Darwin+macabi)

### Changed

- Pack-required natives: add `linux-arm64`, `osx-x64`, `android-arm64`, `android-x64`, `ios-arm64`, `iossimulator-arm64` (HARD CI + Pack assert)
- Soft CI only: `win-arm64` ([alibaba/zvec#622](https://github.com/alibaba/zvec/issues/622)); `maccatalyst-arm64` (included in Pack [30311588652](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/actions/runs/30311588652); soft until next HARD promote)
- Desktop optional RIDs build on native runners (`ubuntu-24.04-arm`, `macos-15-intel`)
- Pack requires desktop + hard-mobile workflow success; asserts HARD RID folders

## [1.0.0-beta.3.1] - 2026-07-26

Native pin unchanged: **zvec 0.5.1**.

### Changed

- Root `LICENSE` is the standard Apache-2.0 text (GitHub license detection)
- Publish NuGet refuses Pack artifacts unless Pack `head_sha` equals the tag commit and Pack concluded **success** (no cross-SHA publish)
- Tag push alone can Pack via `workflow_call` when no same-SHA green Pack exists
- Packed nupkg stamps `RepositoryCommit` from the pack commit for provenance
- Maintainer docs: mandatory local `simulate-pack.ps1` before remote Pack/tag

### Fixed

- Consumer smoke HardExit after Shutdown on Windows (`TerminateProcess`) as well as Linux (`_exit`) so Pack consumer does not AV on `Environment.Exit`

## [1.0.0-beta.3] - 2026-07-26

Native pin unchanged: **zvec 0.5.1**.

### Added

- `IZvecFactory.OpenOrCreate` / `OpenOrCreateAsync` — restart-safe open-or-create (upstream has no native `open_or_create`)
- `ZVecCollectionOpenMode` (`CreateOnly`, `OpenOnly`, `OpenOrCreate`) for DI; **default `OpenOrCreate`**
- `ZVecNativeTeardownPolicy` (`Auto`, `AlwaysCall`, `Suppress`) on `ZVecOptions`
- Root `NOTICE` for Apache-2.0 / upstream attribution

### Changed

- DI no longer requires flipping `Create = false` on second run; obsolete `bool Create` shim maps to CreateOnly / OpenOnly
- Samples use SDK `OpenOrCreate` instead of a samples-only helper implementation
- License: **Apache-2.0** (was incorrectly documented as MIT; upstream zvec is Apache-2.0)
- Consumer smoke calls Dispose/Shutdown under Linux Auto teardown mitigation; Linux smoke then `_exit(0)` to avoid glibc atexit SIGSEGV after suppress

### Fixed

- Linux host stop crashing with exit **139** (SIGSEGV on `zvec_collection_close` / `zvec_shutdown`): Auto policy skips those native calls on Linux so the process exits 0
  - Tracked upstream: https://github.com/alibaba/zvec/issues/619
  - Temporary; removal checklist documented in README (superseded by **1.0.0-beta.3.2** ownership fix)

### Known limitations (Linux teardown workaround)

- Possible skipped final native flush; native handles leaked until process exit
- Same-process dispose then reopen of the same path is fragile — prefer a singleton for process lifetime
- Prefer one `Initialize` per process on Linux

## [1.0.0-beta.2] - 2026-07-18

Initial public beta wrapping ZVec C++ 0.5.1. Shipped natives: win-x64, linux-x64, osx-arm64, android-arm64, android-x64.
