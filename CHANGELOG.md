# Changelog

All notable changes to ZVec.NET are documented in this file.

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
  - Temporary; removal checklist documented in README

### Known limitations (Linux teardown workaround)

- Possible skipped final native flush; native handles leaked until process exit
- Same-process dispose then reopen of the same path is fragile — prefer a singleton for process lifetime
- Prefer one `Initialize` per process on Linux

## [1.0.0-beta.2] - 2026-07-18

Initial public beta wrapping ZVec C++ 0.5.1. Shipped natives: win-x64, linux-x64, osx-arm64, android-arm64, android-x64.
