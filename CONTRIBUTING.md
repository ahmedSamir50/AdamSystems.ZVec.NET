# Contributing to ZVec.NET

First off, thank you for considering contributing to ZVec.NET! Our goal is to bring the blazing fast performance of Alibaba's ZVec to the .NET ecosystem, and community support makes that possible.

## Repository Architecture

This repository contains two main components:
1. **ZVec.NET (C#):** The managed .NET SDK — DI-first (`IZvecFactory` / `IZvecCollection`), async APIs, zero-allocation vector paths.
2. **ZVec.Native (C++):** CMake wrapper that builds Alibaba's official fat C API (`zvec_c_api`) from the `external/zvec` submodule.

Native code lives at `src/Native/ZVec.Native` (header: `external/zvec/src/include/zvec/c_api.h`). Windows build steps: [`src/Native/ZVec.Native/steps.md`](src/Native/ZVec.Native/steps.md).

Design details (Factory/Builder, DI, LINQ-on-results, concurrency gates): [`ZVec.NET-Project-Plan.md`](ZVec.NET-Project-Plan.md) §§3, 7, 8.4.

## Local Development Setup

Because this project relies on a native C++ engine, you cannot simply press "Run" in Visual Studio immediately. You must initialize the C++ submodules first.

1. **Clone the repository with submodules:**
   If you already cloned the repo normally, run:
   `git submodule update --init --recursive`
   *(This downloads the upstream Alibaba C++ code into `src/Native/ZVec.Native/external/zvec`.)*

2. **C++ Compilation:**
   Ensure you have CMake and a C++ compiler (Visual Studio 2026 with C++ Desktop Development workload, or GCC/Clang on Linux/macOS) installed. On Windows, follow [`src/Native/ZVec.Native/steps.md`](src/Native/ZVec.Native/steps.md) (Ninja + Scoop tools: `ninja`, `make`, `mingw`, `perl`). Quick path from Developer PowerShell for VS 2026:

   ```powershell
   cd src\Native\ZVec.Native
   .\_configure_ninja.bat
   .\_build_ninja.bat
   ```

   The DLL is written under `src/Native/ZVec.Native/build\` (e.g. `build\external\zvec\bin\zvec_c_api.dll`).

## How to Contribute

1. **Branching:** Never work directly on `main`. Create a feature branch off the `dev` branch (e.g., `feature/add-hybrid-search`).
2. **Pull Requests:** Submit all Pull Requests against the `dev` branch.
3. **API shape:** Prefer interfaces + `AddZVec*` DI registration over new static entry points.
   - **Typed (preferred for app code):** `IZvecCollection<T>`, `ZVec.NET.Mapping` attributes, `ZVecCollectionSchemaBuilder.From<T>()`, expression filters, `AddZVecCollection<T>`. `EnsureSchema` is **additive only** (never auto-drop).
   - **Dynamic (advanced):** `IZvecCollection`, `ZVecDoc`, string field names, fluent `ZVecFilterBuilder`, `AddZVecCollection(string key, …)`.
   - Implement **complete** `type.h` enums and **all** index-param types (Hnsw, HnswRabitq, Ivf, Flat, DiskAnn, Vamana, Invert, Fts) — do not defer indexes.
4. **Coverage target:** Wrap the **Vector Database** C++ / `zvec_c_api` surface and match DB sections of [llms-full](https://zvec.org/llms-full.txt) (see plan §2.0). Do **not** implement AI Integration (embeddings, MCP, skills, model rerankers) in this package. Snapshot used for audits: `docs/llms-full.txt`.
5. **Async & concurrency:** Public surface is async-first. P/Invoke is sync — always go through collection read/write gates; never add unbounded `Task.Run` around native calls. Honor `CancellationToken` while waiting on gates.
6. **Zero allocation:** On hot paths (`Query` / `Insert`), use `ReadOnlySpan<float>` / `ReadOnlyMemory<float>` and `MemoryHandle`. Do not introduce `new float[]` copies on vector passing paths. Typed ODM mapping is a managed edge cost — keep heavy work on the existing `ZVecDoc` native path.
7. **Enums / ABI:** Match numeric values to upstream `zvec/db/type.h` and `c_api.h`. If the C header omits a define (e.g. `HNSW_RABITQ = 4`), use the `type.h` value — do not invent new numbers. Document every enum in the project plan Appendix A.
8. **LINQ:** Apply LINQ to **results** only. Expression filters on `IZvecCollection<T>` translate to native filter strings — do **not** add a custom `IQueryable` provider over the engine.
9. **Testing:** Run the `ZVec.NET.Tests` project. Unit tests cover pure managed logic (including Mapping / typed façade); native-backed integration/memory tests use the real `zvec_c_api` binary and **Skip** when it is not available. `NativeLibraryResolver.SetMockLibrary` is reserved for missing-path / failure-path tests only (no mock C++ project). Typed ODM overhead: `TypedOdmOverheadBench` in `ZVec.NET.Benchmarks`.

If you are unsure where to start, check the Issues tab for "good first 
issue" tags!