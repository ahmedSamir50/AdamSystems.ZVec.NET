# ZVec.NET — Production-Grade Project Plan

> **ZVec** is Alibaba's open-source, in-process vector database — the "SQLite of Vector DBs" — built on the battle-tested Proxima search engine. Written in C++, it delivers sub-millisecond HNSW search, hybrid scalar+vector filtering, full-text search, WAL durability, and memory-mapped I/O with zero network overhead.
>
> **ZVec.NET** is Adam Systems' .NET NuGet package that wraps ZVec's C++ core via the official `zvec_c_api` and exposes an idiomatic C# API (sync + async) with zero-allocation vector pipelines. Public identity follows the Newtonsoft-style pattern: **company (AdamSystems) + product (ZVec) + framework (.NET)**.

---

## Table of Contents

1. [Project Vision & Goals](#1-project-vision--goals)
2. [ZVec API Surface Catalog](#2-zvec-api-surface-catalog)
3. [Architecture Overview](#3-architecture-overview)
4. [Detailed Design](#4-detailed-design)
5. [Native C-API Bridge Specification](#5-native-c-api-bridge-specification)
6. [P/Invoke & Marshalling Layer](#6-pinvoke--marshalling-layer)
7. [Public SDK API Design](#7-public-sdk-api-design)
8. [Memory & Performance Strategy](#8-memory--performance-strategy)
9. [Cross-Platform NuGet Packaging](#9-cross-platform-nuget-packaging) *(incl. §9.3 Versioning & §9.4 Announcing to Upstream)*
10. [Testing Strategy](#10-testing-strategy)
11. [Benchmark Strategy](#11-benchmark-strategy)
12. [Work Breakdown Structure (WBS)](#12-work-breakdown-structure-wbs)
13. [CI/CD Pipeline](#13-cicd-pipeline)
14. [Risk Register](#14-risk-register)
15. [Timeline & Milestones](#15-timeline--milestones)
16. [Appendix A — ZVec Enum Reference](#appendix-a--zvec-enum-reference)
17. [Appendix B — ZVec Method Signature Reference](#appendix-b--zvec-method-signature-reference)

---

## 1. Project Vision & Goals

### 1.1 Vision

Deliver the **definitive .NET SDK** for ZVec — the same raw performance as the C++ core, with the ergonomics .NET developers expect. The SDK should feel like it was written by Microsoft, not by a C++ engineer who learned C# last week.

### 1.2 Goals

| # | Goal | Success Metric |
|---|------|---------------|
| G1 | **Idiomatic C# API** | Sync + async entry points; .NET naming guidelines; `ValueTask` for async; `IAsyncEnumerable` for streaming where useful |
| G2 | **Zero-allocation vector pipeline** | `ReadOnlySpan<float>` / `ReadOnlyMemory<float>` on vector hot paths; no `float[]` copies. Scalar `Fields` may box (documented) |
| G3 | **Sub-millisecond overhead** | Sync P/Invoke marshalling overhead < 50 µs on a 768-dim vector query (verified by BenchmarkDotNet); async path uses bounded offload |
| G4 | **Cross-platform single NuGet** | One `.nupkg` (`ZVec.NET`) with native binaries for win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64 |
| G5 | **Edge-ready / MAUI-compatible** | Memory-mapped I/O exposed; resource governance knobs exposed; no server dependency |
| G6 | **Full in-process DB / C++ wrap** | 100% of the **Vector Database** API that `zvec_c_api` / C++ exposes, with managed shapes aligned to Python/Node **DB** docs in [llms-full](https://zvec.org/llms-full.txt) (collections, schema, CRUD, query modes, indexes, schema evolution, config). **Not** 100% of AI Integration packages (embeddings, MCP, skills, model rerankers) |
| G7 | **SafeHandle guarantees** | Every native pointer wrapped in `SafeHandle`; consumers **must** `Dispose` / `await using`; finalizer is a safety net only |
| G8 | **Comprehensive test & benchmark suite** | ≥90% line coverage; BenchmarkDotNet comparison vs. raw P/Invoke baseline |

### 1.3 Non-Goals (v1)

- Embedding generation (dense/sparse) — AI Integration; left to user or separate package
- MCP server integration — AI Integration; not part of `zvec_c_api`
- Skills / agent tooling packages — AI Integration
- Model-based / API rerankers (e.g. Qwen, local cross-encoder) — AI Integration; **in-DB** fusion rerankers (`Rrf` / `Weighted` via multi-query C API) **are** in scope
- Custom `IQueryable` / LINQ provider over the engine (LINQ applies to **query results** only)
- Blazor WebAssembly hosting (no native `zvec_c_api` RID for WASM in v1; Blazor Server is supported)

> **G6 excludes AI Integration.** Coverage is the C++ DB wrap + Vector Database docs parity.
>
> **All DB index types are in scope for v1** (HNSW, Flat, IVF, HNSW-RaBitQ, DiskANN, Vamana, Invert, FTS). Platform caveats (RaBitQ = x86_64/AVX2; DiskANN = Linux + libaio per docs) are runtime warnings, not scope cuts.

### 1.4 Constraints & Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Public identity | Namespace, assembly, PackageId = **`ZVec.NET`** |  ZVec (wrapped product) + .NET (framework). Consumers import only this name |
| Inner project path | `src/Core/...` for layout only | Folder/project may use `Core` internally; **never** ship a public `*.Core` namespace or PackageId |
| .NET Target | `net8.0` – `net10.0` (pack `lib/net8.0` when identical) | `net8.0` is LTS; net9/10 fall back to net8.0 asset if no TFM-specific code |
| C++ Standard | C++17 | Matches ZVec upstream |
| License | MIT via `<PackageLicenseExpression>` | Correct NuGet metadata property (not `<License>`) |
| Strong-naming | Yes, open signing key (`.snk` committed to repo) | Enterprise / strong-named consumer compatibility. The `.snk` is an **identity key** (not a security secret) — like SQLitePCLRaw's approach. Generated once via `sn -k ZVec.NET.snk` and placed in `build/`. It lets strong-named consumers reference our assembly |
| Test Framework | xUnit + FluentAssertions | User decision |
| Mock Strategy | C++ mock C-API DLL (primary); managed `SetDllImportResolver` mock optional | Unit tests without full native rebuild; integration uses real binaries |
| NuGet Layout | Single `.nupkg` with `runtimes/{rid}/native/` | Runtime resolves RID natives; no custom `build/*.props` for RID packing |
| P/Invoke Style | `[LibraryImport]` (source generator) | Compile-time marshalling; faster than `[DllImport]` |
| Public API style | DI-first (`IZvecFactory` / `IZvecCollection`) + selective builders | Hostable in ASP.NET Core, MAUI, Blazor Server |
| Factory / Builder | Factory for open/create; builders for schema + filters | Immutable configs vs native resource lifecycle |
| C++ type surface (v1) | Full wrap — every `type.h` enum + every index-param class | Match C++ library; align with [llms-full.txt](https://zvec.org/llms-full.txt) DB sections |
| Docs audit source | Fresh snapshot [`docs/llms-full.txt`](docs/llms-full.txt) | Re-audit when upstream docs change |
| LINQ | On results only | Engine predicates stay FilterBuilder + query objects |
| Public API shape | **Sync + async** entry points | Sync = lowest latency after RW lock; async = bounded offload for ASP.NET |
| Concurrency gates | Interlocked for lifecycle state; native C++ owns operation-level thread safety | No managed-side RW lock — `Interlocked.CompareExchange`/`Exchange` for factory/collection state transitions; native `std::atomic` for its own global config. The planned `AsyncReaderWriterLock` was canceled (redundant with native guarantees, correctness concerns) |
| Native version gate | Prefer `zvec_check_version` / int major/minor/patch; string version is diagnostics only | Fail fast on ABI mismatch; never free static version string |
| Close vs Destroy | `Dispose`/`ReleaseHandle` → `close` only; `Destroy` → `destroy` then `close` | Close releases handle (data on disk); Destroy permanently deletes collection |
| Factory shutdown | `IZvecFactory` : `IAsyncDisposable` + `Shutdown`/`ShutdownAsync` → `zvec_shutdown` | DI singleton dispose tears down process-wide native state |
| First-init-wins | First successful `Initialize` wins; later calls no-op | Log warning if subsequent `ZVecOptions` differ (incl. `MemoryLimitMb`) |
| Enum / ABI source of truth | Upstream `zvec/db/type.h` + `c_api.h` only | Prefer `type.h` when C macros lag; **never** invent parallel headers or numeric values |
| Bool marshalling | `[MarshalAs(UnmanagedType.U1)]` for C99 `bool` | Not `UnmanagedType.Bool` (4-byte Win32 BOOL) |

### 1.5 General Coding Rules & Guidelines

For all current and future development tasks, the following strict rules apply:
1. **Developer-Friendly Documentation**: Check created classes and enums against `c_api.h` and `docs/llms-full.txt`. All C# code must include explicitly written XML documentation (`///`) reflecting the original C++ docs so it is highly accessible to developers.
2. **Centralized Defaults**: Use a public `Defaults` class (or similar structural mechanism) that groups many static classes/defaults by related functionality instead of scattering magic values. 
3. **No Magic Strings**: All strings must be defined as constants, `nameof()`, or strongly typed enums. 
4. **No Magic Default Values**: All default values must be sourced from the centralized `Defaults` class or clearly defined constants. 

---

## 2. ZVec API Surface Catalog

This catalog covers the **Vector Database** surface that maps to our submodule (`zvec_c_api` / C++). It is aligned to the DB sections of [llms-full.txt](https://zvec.org/llms-full.txt) / [llms.txt](https://zvec.org/llms.txt). **AI Integration** (Embedding, MCP, Skills, model rerankers) is out of scope for this package — see §1.3.

Every **DB** item below that has a C API (or `type.h`) counterpart **must** have a corresponding C# wrapper. Features documented for Python/Node but **missing from `c_api.h`** are listed as **binding gaps** (use `type.h` numeric values where possible; do not invent P/Invoke).

> **Full audit completed:** Cross-checked `c_api.h` (4,144 lines) × `type.h` (146 lines). Only 2 binding gaps remain (`HNSW_RABITQ=4`, `RABITQ=4`), both resolved via `type.h` values. All other DB features have matching C API functions.

### 2.0 Coverage matrix (DB docs × C++ × plan)

Audit basis: remote `https://zvec.org/llms-full.txt` saved as [`docs/llms-full.txt`](docs/llms-full.txt) (2026-07-14, ~715 KB), cross-checked against `c_api.h` + `type.h` and this plan §7.

| DB feature (llms-full / llms.txt) | `c_api.h` / `type.h` | Plan §7 / catalog | Status |
|----------------------------------|----------------------|-------------------|--------|
| Global config / init | `zvec_initialize`, config/log APIs, `zvec_get_version*` | `IZvecFactory.Initialize` / `InitializeAsync`, first-init-wins, `ZVecOptions`, version gate | Covered |
| Global shutdown | `zvec_shutdown` | `Shutdown` / `ShutdownAsync`, factory `IAsyncDisposable` | Covered |
| Create and open | `zvec_collection_create_and_open` | `CreateAndOpen` / `CreateAndOpenAsync` | Covered |
| Open / close / destroy | `zvec_collection_open` / `close` / `destroy` | `Open` / `OpenAsync`; `Dispose`→close only; `Destroy`→destroy+close | Covered |
| Inspect (schema/stats/options/path) | `zvec_collection_get_schema` / `stats` / `options` | Collection properties | Covered |
| Optimize | `zvec_collection_optimize` | `Optimize` / `OptimizeAsync` | Covered |
| Schema + field/vector indexes | schema + `zvec_index_params_*` | SchemaBuilder + IndexParams | Covered |
| Schema evolution (add/alter/drop column, create/drop index) | `zvec_collection_add_column`, schema alter/drop, index add/drop | DDL methods on `IZvecCollection` (sync + async) | Covered |
| Insert / Upsert / Update / Delete / DeleteByFilter | matching `zvec_collection_*` | Sync + `*Async` CRUD | Covered |
| Fetch | `zvec_collection_fetch` | `Fetch` / `FetchAsync` (single → `ZVecDoc?`; batch → dictionary) | Covered |
| Query single / multi / filter / hybrid | `zvec_collection_query`, multi-query APIs | `Query` / `QueryAsync` | Covered |
| Full-text query | FTS query params + collection query | `ZVecFtsQuery` | Covered |
| Group query | `zvec_group_by_vector_query_*` | `QueryGroupBy` / `QueryGroupByAsync` / `ZVecGroupByQuery` | Covered |
| In-DB RRF / Weighted fusion | `zvec_multi_query_set_rerank_rrf` / `_weighted` | `ZVecRrfReranker` / `ZVecWeightedReranker` | Covered |
| Indexes: HNSW, Flat, IVF, Invert, FTS | index type macros + params APIs | IndexParams + Appendix A | Covered |
| Indexes: DiskANN, Vamana | types + query/index params in C API | `ZVecDiskAnnIndexParam` / `ZVecVamanaIndexParam` | Covered (DiskANN: Linux-only per docs) |
| Index: HNSW-RaBitQ | **`type.h` = 4**; **no `#define` in `c_api.h`** | `ZVecHnswRabitqIndexParam` | **Binding gap** — pass value `4` from `type.h` until upstream adds macro |
| Quantize `RABITQ` | **`type.h` = 4**; C quantize macros stop at INT4 | `ZVecQuantizeType.Rabitq` | **Binding gap** — same as above |
| All `DataType` ABI values | `ZVEC_DATA_TYPE_*` | Appendix A | Covered |
| Embedding models / MCP / Skills / model rerankers | Not in `zvec_c_api` | §1.3 non-goals | **Out of scope (AI)** |

### 2.1 Top-Level Module Functions

| Python | Node.js | Description |
|--------|---------|-------------|
| `zvec.init(log_type, log_level, query_threads)` | `ZVecInitialize({ logType, logLevel, queryThreads })` | Global configuration; call once at startup |
| `zvec.create_and_open(path, schema, option?)` | `ZVecCreateAndOpen(path, schema, options?)` | Create a new collection and open it |
| `zvec.open(path, option?)` | `ZVecOpen(path, options?)` | Open an existing collection |

### 2.2 Enumerations

Managed wrappers must expose the **full** C++ set from `type.h` (see Appendix A). Public docs ([llms-full.txt](https://zvec.org/llms-full.txt)) list a product-facing subset; ABI-only members remain required for a complete wrap.

| Enum | Values (docs + ABI) |
|------|--------|
| `DataType` | Docs: `STRING`, `BOOL`, `INT32`, `INT64`, `UINT32`, `UINT64`, `FLOAT`, `DOUBLE`, `ARRAY_*`, `VECTOR_FP16`/`FP32`/`INT8`, `SPARSE_VECTOR_FP16`/`FP32`. ABI also: `BINARY`, `VECTOR_BINARY32`/`64`, `VECTOR_FP64`, `VECTOR_INT4`/`INT16`, `ARRAY_BINARY`, `UNDEFINED` |
| `MetricType` | `UNDEFINED`, `L2`, `IP`, `COSINE`, `MIPSL2` |
| `LogType` | `CONSOLE`, `FILE` |
| `LogLevel` | `DEBUG`, `INFO`, `WARN`, `ERROR`, `FATAL` |
| `IndexType` | `UNDEFINED`, `HNSW`, `IVF`, `FLAT`, `HNSW_RABITQ`, `DISKANN`, `VAMANA`, `INVERT`, `FTS` |
| `QuantizeType` | `UNDEFINED`, `FP16`, `INT8`, `INT4`, `RABITQ` |

### 2.3 Schema Definition Types

| Type | Properties |
|------|-----------|
| `CollectionSchema` | `name: string`, `fields: list[FieldSchema]`, `vectors: list[VectorSchema]`, `max_doc_count_per_segment: int` |
| `FieldSchema` | `name: string`, `data_type: DataType`, `nullable: bool`, `index_param: InvertIndexParam?` |
| `VectorSchema` | `name: string`, `data_type: DataType`, `dimension: int`, `index_param: HnswIndexParam? or HnswRabitqIndexParam? or IvfIndexParam? or FlatIndexParam? or DiskAnnIndexParam? or VamanaIndexParam? or FtsIndexParam?` |
| `InvertIndexParam` | `enable_range_optimization: bool`, `enable_extended_wildcard: bool` |
| `HnswIndexParam` | `metric_type: MetricType`, `m: int`, `ef_construction: int`, `quantize_type: QuantizeType` |
| `HnswRabitqIndexParam` | `metric_type: MetricType`, `total_bits: int`, `num_clusters: int`, `m: int`, `ef_construction: int`, `sample_count: int` |
| `IvfIndexParam` | `metric_type: MetricType`, `centroids_num: int`, `nlist: int`, `nprobe: int`, `quantize_type: QuantizeType` |
| `FlatIndexParam` | `metric_type: MetricType`, `quantize_type: QuantizeType` |
| `DiskAnnIndexParam` | `metric_type: MetricType`, `max_degree: int`, `list_size: int`, `pq_chunk_num: int`, `quantize_type: QuantizeType` |
| `VamanaIndexParam` | `metric_type: MetricType`, `max_degree: int`, `search_list_size: int`, `alpha: float`, `saturate_graph: bool`, `use_contiguous_memory: bool`, `use_id_map: bool`, `quantize_type: QuantizeType` |
| `FtsIndexParam` | Python: `tokenizer_name`/`filters`/`extra_params` strings → .NET: `ZVecFtsTokenizer` / `IReadOnlyList<ZVecFtsTokenFilter>` / `ZVecFtsExtraParams?` |
| `CollectionOption` | `read_only: bool`, `enable_mmap: bool` |

### 2.4 Document Type

| Type | Properties |
|------|-----------|
| `Doc` | `id: string`, `vectors: dict[string, float[] or dict[int,float]]`, `fields: dict[string, object]`, `score: float` (read-only, query result) |

### 2.5 Collection Methods

| Method | Signature (Python) | Returns |
|--------|-------------------|---------|
| `insert` | `collection.insert(doc or list[doc])` | `Status` or `list[Status]` |
| `upsert` | `collection.upsert(doc or list[doc])` | `Status` or `list[Status]` |
| `update` | `collection.update(doc or list[doc])` | `Status` or `list[Status]` |
| `delete` | `collection.delete(ids: str or list[str])` | `Status` or `list[Status]` |
| `delete_by_filter` | `collection.delete_by_filter(filter: str)` | `Status` |
| `fetch` | `collection.fetch(ids: str or list[str])` | `dict[str, Doc]` |
| `query` | `collection.query(queries, topk, filter?, reranker?)` | `list[Doc]` |
| `optimize` | `collection.optimize()` | `void` |
| `destroy` | `collection.destroy()` | `void` |
| `add_column` | `collection.add_column(field_schema, expression)` | `void` |
| `drop_column` | `collection.drop_column(field_name)` | `void` |
| `alter_column` | `collection.alter_column(old_name?, new_name?, field_schema?)` | `void` |
| `create_index` | `collection.create_index(field_name, index_param)` | `void` |
| `drop_index` | `collection.drop_index(field_name)` | `void` |

### 2.6 Collection Properties

| Property | Type | Description |
|----------|------|-------------|
| `schema` | `CollectionSchema` | Current collection schema |
| `stats` | `CollectionStats` | `{ doc_count, index_completeness }` |
| `option` | `CollectionOption` | Runtime options (enable_mmap, read_only) |
| `path` | `string` | Filesystem path |

### 2.7 Query Types

Query modes documented in [llms-full.txt](https://zvec.org/llms-full.txt) — all must be wrapped: **single vector**, **multi vector**, **filter**, **hybrid** (dense + sparse), **FTS**, **group**.

| Type | Properties |
|------|-----------|
| `Query` | `field_name: string`, `vector?: float[] or dict[int,float]`, `id?: string`, `fts?: Fts`, `params?: dict` |
| `Fts` | `match_string?: string`, `query_string?: string`, `default_operator?: string` |
| `GroupBy` / grouped query | Group field + per-group topk / limit (see docs “Group” query); C# `ZVecGroupByQuery` |

### 2.8 Reranker Types

| Type | Properties |
|------|-----------|
| `WeightedReRanker` | `topn: int`, `metric: MetricType`, `weights: dict[str,float]` |
| `RrfReRanker` | `topn: int`, `rank_constant: float` |

### 2.9 Filter Syntax

ZVec uses string-based filter expressions:

| Operator | Example |
|----------|---------|
| `=`, `!=` | `category = "fiction"` |
| `>`, `<`, `>=`, `<=` | `publish_year > 1936` |
| `IN` | `category IN ("fiction", "romance")` |
| `LIKE` | `title LIKE "Wireless%"` |
| `CONTAIN_ANY` | `tags CONTAIN_ANY ["sport", "music"]` |
| `CONTAIN_ALL` | `permissions CONTAIN_ALL ["read", "write"]` |
| `AND`, `OR`, `NOT` | `publish_year > 1936 AND category = "fiction"` |

---

## 3. Architecture Overview

```
┌──────────────────────────────────────────────────────────────┐
│         Consumer (ASP.NET Core / MAUI / Blazor Server)       │
│              DI: AddZVec / AddZVecCollection                  │
│         using ZVec.NET;                           │
└───────────────────────┬──────────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────────┐
│              ZVec.NET Public SDK                  │
│                                                               │
│  ┌──────────────┐  ┌────────────────┐  ┌─────────────────┐  │
│  │ IZvecFactory │  │ IZvecCollection│  │ SchemaBuilder + │  │
│  │ (open/create)│  │ sync + async   │  │ FilterBuilder   │  │
│  └──────┬───────┘  └───────┬────────┘  └────────┬────────┘  │
│         │                  │                     │            │
│  ┌──────┴──────────────────┴─────────────────────┴────────┐  │
│  │  DTO Layer (ZVecDoc, ZVecStatus, schemas, index params) │  │
│  └──────────────────────────┬─────────────────────────────┘  │
│                              │                                │
│  ┌──────────────────────────┴─────────────────────────────┐  │
│  │  Interlocked lifecycle gating + process-wide native cap    │  │
│  └──────────────────────────┬─────────────────────────────┘  │
│                              │                                │
│  ┌──────────────────────────┴─────────────────────────────┐  │
│  │          SafeHandle Layer (SafeZvecHandle, etc.)         │  │
│  └──────────────────────────┬─────────────────────────────┘  │
│                              │                                │
│  ┌──────────────────────────┴─────────────────────────────┐  │
│  │        P/Invoke Layer ([LibraryImport] source-gen)       │  │
│  │              NativeMethods.cs                            │  │
│  └──────────────────────────┬─────────────────────────────┘  │
└─────────────────────────────┼────────────────────────────────┘
                              │  P/Invoke (flat C ABI)
                              ▼
┌──────────────────────────────────────────────────────────────┐
│              zvec_c_api (official Alibaba C bindings)        │
│                                                               │
│  zvec_initialize / zvec_get_version / zvec_check_version     │
│  zvec_collection_create_and_open / open / close / destroy    │
│  zvec_collection_insert / query / fetch / delete / optimize  │
│  zvec_get_last_error / zvec_get_last_error_details           │
└──────────────────────────┬───────────────────────────────────┘
                           │  C++ function calls
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                   ZVec C++ Core (Proxima Engine)              │
└──────────────────────────────────────────────────────────────┘
```

### 3.1 Layer Responsibilities

| Layer | Responsibility | Key Design Principle |
|-------|---------------|---------------------|
| **Public SDK** | Idiomatic sync + async C# API + DI; hides unsafe code | Code against `IZvecFactory` / `IZvecCollection`; never expose `IntPtr`; namespace `ZVec.NET` |
| **Builders** | `ZVecCollectionSchemaBuilder` + fluent `ZVecFilterBuilder` | Immutable outputs; no native calls |
| **DTO Layer** | Strongly-typed data transfer objects | `init` properties; `ReadOnlyMemory<float>` for vectors |
| **Concurrency gates** | `Interlocked` lifecycle gating + process-wide native cap | Factory/collection state transitions via `Interlocked.CompareExchange`/`Exchange`; no managed-side RW lock (canceled — native C++ handles operation-level safety) |
| **SafeHandle Layer** | Wraps native pointers; guarantees cleanup | Prefer explicit `Dispose`; finalizer is last resort (see §8.2) |
| **P/Invoke Layer** | `[LibraryImport]` from upstream `c_api.h` | Blittable spans where applicable; C99 `bool` as `U1` |
| **C-API Bridge** | Upstream official `zvec_c_api` (Alibaba C bindings) | C-linkage; `zvec_error_code_t` + last-error APIs — **do not invent headers** |
| **ZVec Core** | Upstream C++ library (git submodule) | Consumed via CMake `add_subdirectory`; not forked for API surface |

---

## 4. Detailed Design

### 4.1 Project Structure

```
ZVec.NET/                    # repo / product root
├── src/
│   ├── Native/
│   │   └── ZVec.Native/                 # CMake wrapper → upstream zvec_c_api
│   │       ├── CMakeLists.txt           # Forces BUILD_C_BINDINGS=ON; add_subdirectory(external/zvec)
│   │       ├── steps.md                 # Windows operator build guide
│   │       └── external/zvec/           # Git submodule (alibaba/zvec)
│   │           ├── src/include/zvec/c_api.h   # Official C API (P/Invoke source of truth)
│   │           └── src/binding/c/             # Builds fat zvec_c_api shared library
│   │
│   ├── Core/                            # INTERNAL layout only — not a public package name
│   │   └── ZVec.NET/        # Published assembly / PackageId: ZVec.NET
│   │       ├── ZVec.NET.csproj
│   │       ├── Abstractions/
│   │       │   ├── IZvecFactory.cs         # Open/create collections (sync + async)
│   │       │   └── IZvecCollection.cs      # Sync + async CRUD + query + DDL
│   │       ├── DependencyInjection/
│   │       │   ├── ZVecServiceCollectionExtensions.cs  # AddZVec / AddZVecCollection
│   │       │   ├── ZVecOptions.cs          # Global init (incl. MemoryLimitMb)
│   │       │   └── ZVecCollectionRegistrationOptions.cs
│   │       ├── Builders/
│   │       │   └── ZVecCollectionSchemaBuilder.cs
│   │       ├── Interop/
│   │       │   ├── NativeMethods.cs        # [LibraryImport] from c_api.h
│   │       │   ├── SafeZvecHandle.cs
│   │       │   ├── SafeZvecQueryHandle.cs
│   │       │   ├── SafeZvecSchemaHandle.cs
│   │       │   └── NativeLibraryResolver.cs
│   │       ├── Internal/
│   │       ├── Models/
│   │       │   ├── ZVecDoc.cs
│   │       │   ├── ZVecStatus.cs
│   │       │   ├── ZVecCollectionSchema.cs
│   │       │   ├── ZVecFieldSchema.cs
│   │       │   ├── ZVecVectorSchema.cs
│   │       │   ├── ZVecCollectionOptions.cs
│   │       │   ├── ZVecCollectionStats.cs
│   │       │   └── Enums/                 # Values MUST match type.h / c_api.h (Appendix A)
│   │       ├── IndexParams/
│   │       ├── Query/
│   │       │   ├── ZVecQuery.cs
│   │       │   ├── ZVecQueryParams.cs      # Typed index-specific query params
│   │       │   ├── ZVecFtsQuery.cs
│   │       │   ├── ZVecGroupByQuery.cs
│   │       │   ├── ZVecFilterBuilder.cs
│   │       │   └── ZVecReranker.cs
│   │       ├── ZVecFactory.cs
│   │       └── ZVecCollection.cs
│   │
│   └── Mock/
│       └── ZVec.Native.Mock/          # Mock native library for testing (C++ primary; outside main Core code)
│           ├── CMakeLists.txt
│           └── src/
│               └── zvec_c_api_mock.cpp # In-memory mock matching upstream C API surface
│
├── testing/
│   ├── ZVec.NET.Tests/
│   │   ├── ZVec.NET.Tests.csproj
│   │   ├── Unit/
│   │   ├── Integration/
│   │   └── Memory/
│   │
│   └── ZVec.NET.Benchmarks/
│       └── ZVec.NET.Benchmarks.csproj
│
├── build/
│   ├── ZVec.NET.snk   # Strong-name identity key (not a secret)
│   └── ci/
│       ├── build-native.yml
│       ├── build-managed.yml
│       └── publish-nuget.yml
│
├── ZVec.NET.slnx      # Solution at repo root (VS .slnx)
├── Directory.Build.props         # Auto-imported by MSBuild (must be at root / ancestor of projects)
├── Directory.Packages.props      # Central Package Management (must be at root)
└── README.md
```

> **Consumer rule:** all imports are `using ZVec.NET;` (plus `.DependencyInjection` / `.Query` as needed). The `Core/` folder is repo layout only — it must not appear in PackageId, assembly name, or public namespaces.

### 4.2 Multi-Targeting Strategy

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Multi-target for local builds; NuGet may ship a single lib/net8.0 if no TFM #ifs -->
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <RootNamespace>ZVec.NET</RootNamespace>
    <AssemblyName>ZVec.NET</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <!-- SignAssembly + AssemblyOriginatorKeyFile live in root Directory.Build.props
         (key file: build/ZVec.NET.snk) -->
  </PropertyGroup>
</Project>
```

---

## 5. Native C-API Bridge Specification

> **Implementation rule:** ZVec.NET consumes Alibaba’s official C API at
> `src/Native/ZVec.Native/external/zvec/src/include/zvec/c_api.h` (built by
> `src/binding/c` as `zvec_c_api`). **Do not invent a parallel custom header,**
> stub `.cpp`, enum table, or error-code table. P/Invoke signatures, enums, and
> error codes must be derived from `c_api.h` + `type.h`. Numeric values live in
> **Appendix A** only.

### 5.1 Design Principles

1. **Flat C linkage only** — no C++ name mangling, no exceptions across boundary
2. **Error code return pattern** — upstream returns `zvec_error_code_t`; details via `zvec_get_last_error` / `zvec_get_last_error_details` (managed layer maps to exceptions / status DTOs). There is **no** `ZvecStatus { int code; char message[512]; }` struct in upstream.
3. **Opaque handles** — C++ objects exposed as opaque pointers (`zvec_collection_t*`, etc.)
4. **No heap allocations in the managed vector hot path** — native allocations stay in ZVec core; C# uses spans / pooling for vectors
5. **Version gate** — call `zvec_check_version` / `zvec_get_version*` during `Initialize` and fail with `ZVecAbiMismatchException` on mismatch
6. **Lifecycle names** — `Dispose` / finalizer → `zvec_collection_close` only. `Destroy` → `zvec_collection_destroy` then `zvec_collection_close`. Do not invent `zvec_destroy` / `zvec_destroy_collection`.

### 5.2 Source of Truth (no parallel header)

| Artifact | Path / API |
|----------|------------|
| C API header | `external/zvec/src/include/zvec/c_api.h` |
| Enum / type ABI | `external/zvec/src/include/zvec/db/type.h` (+ macros in `c_api.h`) |
| Error codes | `zvec_error_code_t` — see Appendix A (`ZVecErrorCode`) |
| Version | `zvec_get_version`, `zvec_get_version_{major,minor,patch}`, `zvec_check_version` |
| Init / shutdown | `zvec_initialize(const zvec_config_data_t*)`, `zvec_shutdown` |
| Memory limit | `zvec_config_data_set_memory_limit` (**global** config, not collection options) |

Before Phase 1 coding starts: audit every §2.5 method against `c_api.h` and record any remaining binding gaps in §2.0.

---

## 6. P/Invoke & Marshalling Layer

### 6.1 `[LibraryImport]` Declarations (illustrative — align every signature to `c_api.h`)

```csharp
// NativeMethods.cs — namespace ZVec.NET.Interop (internal)
internal static partial class NativeMethods
{
    private const string LibraryName = "zvec_c_api";

    // Expected native SemVer pinned at build time (also embedded in NuGet +zvec metadata)
    internal const int ExpectedMajor = /* pin */;
    internal const int ExpectedMinor = /* pin */;
    internal const int ExpectedPatch = /* pin */;

    // Version gate: prefer ints / zvec_check_version. String is library-owned static memory —
    // MUST NOT use LPUTF8Str return (that CoTaskMemFrees the pointer → crash).
    [LibraryImport(LibraryName)]
    internal static partial IntPtr zvec_get_version(); // const char* — do not free

    internal static string GetVersionString()
    {
        IntPtr ptr = zvec_get_version();
        return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
    }

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool zvec_check_version(int major, int minor, int patch);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_version_major();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_version_minor();

    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_version_patch();

    // Returns zvec_error_code_t (int). Map via Appendix A — not a custom struct.
    [LibraryImport(LibraryName)]
    internal static partial int zvec_initialize(IntPtr configData);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_shutdown();

    // error_msg is malloc'd by native code — free with libc free(), NOT Marshal.FreeCoTaskMem.
    // Copy with PtrToStringUTF8, then NativeMemory.Free / P/Invoke free(ptr).
    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_last_error(out IntPtr errorMsg); // char**; caller frees

    // Prefer zvec_get_last_error_details when you only need code/message pointers without owning allocation.
    // Note: zvec_error_details_t is a struct, not an opaque pointer — define as managed struct with explicit layout.
    [LibraryImport(LibraryName)]
    internal static partial int zvec_get_last_error_details(ref ZvecErrorDetails errorDetails);

    // Error details struct — maps to zvec_error_details_t in c_api.h
    [StructLayout(LayoutKind.Sequential)]
    internal struct ZvecErrorDetails
    {
        public ZVecErrorCode Code;      // zvec_error_code_t
        public IntPtr Message;          // const char* — do not free (owned by library)
        public IntPtr File;             // const char* — do not free
        public int Line;
        public IntPtr Function;         // const char* — do not free
    }

    // Free memory allocated by the native library (e.g., from zvec_get_last_error).
    // Use this instead of Marshal.FreeCoTaskMem — the native library uses its own allocator.
    [LibraryImport(LibraryName)]
    internal static partial void zvec_free(IntPtr ptr);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_create_and_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr schema,
        IntPtr collectionOptions,
        out IntPtr outCollection);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_open(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        IntPtr collectionOptions,
        out IntPtr outCollection);

    // C99 bool params — MUST use U1 (1 byte), not UnmanagedType.Bool (4-byte Win32 BOOL)
    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_options_set_read_only(
        IntPtr options,
        [MarshalAs(UnmanagedType.U1)] bool readOnly);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_options_set_enable_mmap(
        IntPtr options,
        [MarshalAs(UnmanagedType.U1)] bool enableMmap);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_close(IntPtr collection);

    [LibraryImport(LibraryName)]
    internal static partial int zvec_collection_destroy(IntPtr collection);

    // Document/query paths use upstream zvec_doc_t / query builders — not a flat invented zvec_insert.
    // Pin ReadOnlyMemory<float> in managed code and pass IntPtr / unsafe float* into the
    // matching upstream doc/vector setter APIs. [LibraryImport] supports ReadOnlySpan<T> for
    // blittable T on net8+ when the C signature truly takes a pointer+length pair.
}
```

**DllImport resolver** (clear RID-miss errors):

```csharp
NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, (name, assembly, searchPath) =>
{
    if (name != "zvec_c_api")
        return IntPtr.Zero;
    if (NativeLibrary.TryLoad(name, assembly, searchPath, out var handle))
        return handle;
    throw new DllNotFoundException(
        $"ZVec native library not found for RID '{RuntimeInformation.RuntimeIdentifier}'. " +
        "Ensure the ZVec.NET NuGet package supports your platform.");
});
```

### 6.2 SafeHandle Implementations

```csharp
internal sealed class SafeZvecHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeZvecHandle() : base(ownsHandle: true) { }

    public SafeZvecHandle(IntPtr handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        // Finalizer / Dispose safety net: CLOSE ONLY.
        // Never call zvec_collection_destroy here — that permanently deletes on-disk data.
        // Verify close is safe from any thread (incl. finalizer).
        if (!IsInvalid)
        {
            _ = NativeMethods.zvec_collection_close(handle);
        }
        return true;
    }
}
```

**Close vs Destroy (upstream semantics):**

| Operation | Native calls | Effect |
|-----------|--------------|--------|
| `Dispose` / `DisposeAsync` / `ReleaseHandle` | `zvec_collection_close` only | Free C handle (`shared_ptr*` delete); **data remains on disk** |
| `Destroy` / `DestroyAsync` | `zvec_collection_destroy` **then** `zvec_collection_close` | Permanent disk delete (`Collection::Destroy`), then free handle |

Note: `zvec_collection_destroy` does **not** free the C handle wrapper — `close` is still required after destroy. After destroy, mark the SafeHandle invalid so Dispose does not double-close.

### 6.3 Zero-Copy Vector Marshalling

```csharp
internal static class VectorMarshaller
{
    /// <summary>
    /// Pins a ReadOnlyMemory<float> and returns a memory handle + pointer.
    /// The caller MUST keep the MemoryHandle alive for the duration of the P/Invoke call.
    /// </summary>
    internal static (IntPtr ptr, MemoryHandle pin) PinVector(ReadOnlyMemory<float> vector)
    {
        var pin = vector.Pin();
        return ((IntPtr)pin.Pointer, pin);
    }

    internal static void SerializeSparseVector(
        IReadOnlyDictionary<int, float> sparse,
        out int[] indices, out float[] values, out int count)
    {
        count = sparse.Count;
        indices = ArrayPool<int>.Shared.Rent(count);
        values = ArrayPool<float>.Shared.Rent(count);

        int i = 0;
        foreach (var kvp in sparse.OrderBy(kv => kv.Key))
        {
            indices[i] = kvp.Key;
            values[i] = kvp.Value;
            i++;
        }
    }

    internal static void ReturnSparseArrays(int[] indices, float[] values)
    {
        ArrayPool<int>.Shared.Return(indices);
        ArrayPool<float>.Shared.Return(values);
    }
}
```

---

## 7. Public SDK API Design

> **Primary surface is DI + interfaces** under namespace **`ZVec.NET`**. All consumer imports use this root (plus `.DependencyInjection` / `.Query`). A thin static façade (scripting/console) may be added later; it must not be the only entry point.
>
> **Sync + async:** Every mutating/querying operation exposes both. Sync calls acquire the RW lock and invoke P/Invoke on the caller thread. Async acquires the same lock, then runs P/Invoke via **bounded** offload (never unbounded `Task.Run`).

### 7.1 Factory — `IZvecFactory`

```csharp
namespace ZVec.NET;

public interface IZvecFactory : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Ensure global native init + ABI version gate have run.
    /// <b>First successful init wins</b> — subsequent calls are no-ops even if
    /// <see cref="ZVecOptions"/> differ; log a warning when options diverge.
    /// </summary>
    void Initialize();
    ValueTask InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Process-wide teardown → <c>zvec_shutdown</c>. Idempotent. Prefer DI container dispose.</summary>
    void Shutdown();
    ValueTask ShutdownAsync(CancellationToken cancellationToken = default);

    IZvecCollection CreateAndOpen(
        string path,
        ZVecCollectionSchema schema,
        ZVecCollectionOptions? options = null);

    ValueTask<IZvecCollection> CreateAndOpenAsync(
        string path,
        ZVecCollectionSchema schema,
        ZVecCollectionOptions? options = null,
        CancellationToken cancellationToken = default);

    IZvecCollection Open(string path, ZVecCollectionOptions? options = null);

    ValueTask<IZvecCollection> OpenAsync(
        string path,
        ZVecCollectionOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

On first `Initialize` / `InitializeAsync`:

1. Resolve native library via `NativeLibrary.SetDllImportResolver`
2. Call `zvec_check_version(ExpectedMajor, ExpectedMinor, ExpectedPatch)` (prefer ints; use `GetVersionString()` only for exception messages)
3. On mismatch → throw `ZVecAbiMismatchException` with expected vs found versions
4. Build `zvec_config_data` from `ZVecOptions` (log, query threads, **MemoryLimitMb**) and call `zvec_initialize`
5. Remember applied options; later `Initialize*` calls return immediately (first-init-wins). If new options differ, log a warning — do not re-apply.

`Dispose` / `DisposeAsync` on the factory call `Shutdown` / `ShutdownAsync` once (`Interlocked.Exchange`). DI registers the factory as a singleton so host teardown runs shutdown.

### 7.1.1 Dependency Injection

```csharp
namespace ZVec.NET.DependencyInjection;

services.AddZVec(options =>
{
    options.LogType = ZVecLogType.Console;
    options.LogLevel = ZVecLogLevel.Warn;
    options.QueryThreads = -1;
    options.MaxConcurrentNativeCalls = 0;      // 0 = auto (e.g. ProcessorCount * 2)
    options.MemoryLimitMb = 512;               // global — maps to zvec_config_data_set_memory_limit
});

services.AddZVecCollection("products", options =>
{
    options.Path = path;
    options.Schema = schema;
    options.EnableMmap = true;
    options.MaxConcurrentReads = Environment.ProcessorCount;
});
```

**Host guidance:** ASP.NET Core / Blazor Server / MAUI — factory and named collections as **singletons**. Prefer injecting `IZvecCollection`. When the host stops, disposing the factory singleton runs `zvec_shutdown`. Blazor WASM is out of scope for v1.

### 7.2 Collection API — `IZvecCollection`

```csharp
namespace ZVec.NET;

public interface IZvecCollection : IDisposable, IAsyncDisposable
{
    ZVecCollectionSchema Schema { get; }
    ZVecCollectionStats Stats { get; }
    ZVecCollectionOptions Options { get; }
    string Path { get; }

    // --- Insert ---
    ZVecStatus Insert(ZVecDoc doc);
    IReadOnlyList<ZVecStatus> Insert(IReadOnlyList<ZVecDoc> docs);
    ValueTask<ZVecStatus> InsertAsync(ZVecDoc doc, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<ZVecStatus>> InsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken cancellationToken = default);

    // --- Upsert / Update (same sync+async pattern) ---
    ZVecStatus Upsert(ZVecDoc doc);
    IReadOnlyList<ZVecStatus> Upsert(IReadOnlyList<ZVecDoc> docs);
    ValueTask<ZVecStatus> UpsertAsync(ZVecDoc doc, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<ZVecStatus>> UpsertAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken cancellationToken = default);

    ZVecStatus Update(ZVecDoc doc);
    IReadOnlyList<ZVecStatus> Update(IReadOnlyList<ZVecDoc> docs);
    ValueTask<ZVecStatus> UpdateAsync(ZVecDoc doc, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<ZVecStatus>> UpdateAsync(IReadOnlyList<ZVecDoc> docs, CancellationToken cancellationToken = default);

    // --- Delete ---
    ZVecStatus Delete(string id);
    IReadOnlyList<ZVecStatus> Delete(IReadOnlyList<string> ids);
    ZVecStatus DeleteByFilter(string filter);
    ValueTask<ZVecStatus> DeleteAsync(string id, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<ZVecStatus>> DeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);
    ValueTask<ZVecStatus> DeleteByFilterAsync(string filter, CancellationToken cancellationToken = default);

    // --- Fetch: single ID returns ZVecDoc?; batch returns dictionary ---
    ZVecDoc? Fetch(string id);
    IReadOnlyDictionary<string, ZVecDoc> Fetch(IReadOnlyList<string> ids);
    ValueTask<ZVecDoc?> FetchAsync(string id, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyDictionary<string, ZVecDoc>> FetchAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    // --- Query ---
    IReadOnlyList<ZVecDoc> Query(ZVecQuery query, int topk = 10, string? filter = null);
    IReadOnlyList<ZVecDoc> Query(IReadOnlyList<ZVecQuery> queries, int topk = 10, string? filter = null, ZVecReranker? reranker = null);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(ZVecQuery query, int topk = 10, string? filter = null, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryAsync(IReadOnlyList<ZVecQuery> queries, int topk = 10, string? filter = null, ZVecReranker? reranker = null, CancellationToken cancellationToken = default);

    IReadOnlyList<ZVecDoc> QueryGroupBy(ZVecGroupByQuery groupQuery);
    ValueTask<IReadOnlyList<ZVecDoc>> QueryGroupByAsync(ZVecGroupByQuery groupQuery, CancellationToken cancellationToken = default);

    void Optimize();
    ValueTask OptimizeAsync(CancellationToken cancellationToken = default);
    void Destroy();
    ValueTask DestroyAsync(CancellationToken cancellationToken = default);

    void AddColumn(ZVecFieldSchema fieldSchema, string defaultExpression);
    void DropColumn(string fieldName);
    void AlterColumnRename(string oldName, string newName);
    void AlterColumnType(string fieldName, ZVecDataType newDataType);
    void CreateIndex(string fieldName, ZVecIndexParam indexParam);
    void DropIndex(string fieldName);

    ValueTask AddColumnAsync(ZVecFieldSchema fieldSchema, string defaultExpression, CancellationToken cancellationToken = default);
    ValueTask DropColumnAsync(string fieldName, CancellationToken cancellationToken = default);
    ValueTask AlterColumnRenameAsync(string oldName, string newName, CancellationToken cancellationToken = default);
    ValueTask AlterColumnTypeAsync(string fieldName, ZVecDataType newDataType, CancellationToken cancellationToken = default);
    ValueTask CreateIndexAsync(string fieldName, ZVecIndexParam indexParam, CancellationToken cancellationToken = default);
    ValueTask DropIndexAsync(string fieldName, CancellationToken cancellationToken = default);
}
```

Concrete public class **`ZVecCollection`** implements this interface (~40 members: sync+async CRUD/query/DDL is intentional for v1; no separate `IZvecSchemaManagement` split). Callers may use BCL LINQ on returned `IReadOnlyList<ZVecDoc>`. There is **no** custom `IQueryable` provider over the engine.

**Dispose vs Destroy:**

- `Dispose` / `DisposeAsync` → `zvec_collection_close` only (data stays on disk). Both must be **idempotent and mutually exclusive** — use `Interlocked.Exchange` so cleanup runs exactly once.
- `Destroy` / `DestroyAsync` → `zvec_collection_destroy` then `zvec_collection_close` (permanent delete). After success, invalidate the handle so a later Dispose is a no-op.

### 7.3 Document DTO — `ZVecDoc`

```csharp
namespace ZVec.NET;

public sealed class ZVecDoc
{
    public required string Id { get; init; }
    public IReadOnlyDictionary<string, ReadOnlyMemory<float>> DenseVectors { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>> SparseVectors { get; init; }

    /// <summary>
    /// Scalar fields. Values are boxed (<c>object</c>) for Python/Node parity.
    /// Zero-allocation goal (G2) applies to <b>vector</b> paths only — iterating
    /// Fields on a hot query path allocates. Prefer vector-only access when measuring GC.
    /// </summary>
    public IReadOnlyDictionary<string, object> Fields { get; init; }
    public float Score { get; init; }

    public static ZVecDoc Create(
        string id,
        IReadOnlyDictionary<string, ReadOnlyMemory<float>>? denseVectors = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>>? sparseVectors = null,
        IReadOnlyDictionary<string, object>? fields = null)
    {
        // Runtime validation: reject null/whitespace — required alone is compile-time only.
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        // ...
    }
}
```

### 7.4 Query Types

```csharp
namespace ZVec.NET;

public sealed class ZVecQuery
{
    public required string FieldName { get; init; }
    public ReadOnlyMemory<float>? Vector { get; init; }
    public IReadOnlyDictionary<int, float>? SparseVector { get; init; }
    public string? DocumentId { get; init; }
    public ZVecFtsQuery? Fts { get; init; }

    /// <summary>Prefer typed <see cref="QueryParams"/> over opaque dictionaries.</summary>
    public ZVecQueryParams? QueryParams { get; init; }
}

/// <summary>Index-specific search params (ef_search, nprobe, etc.). Subclass per index family.</summary>
public abstract class ZVecQueryParams { }

public sealed class ZVecHnswQueryParams : ZVecQueryParams
{
    public int? EfSearch { get; init; }
}

public sealed class ZVecIvfQueryParams : ZVecQueryParams
{
    public int? Nprobe { get; init; }
}

// Flat / DiskANN / Vamana / FTS query-param types are added when the matching
// setters are confirmed in upstream c_api.h (same binding-gap pattern as §2.0).

public sealed class ZVecFtsQuery
{
    public string? MatchString { get; init; }
    public string? QueryString { get; init; }
    public ZVecFtsDefaultOperator DefaultOperator { get; init; } = ZVecFtsDefaultOperator.Or;
}

public enum ZVecFtsDefaultOperator { Or, And }

public sealed class ZVecGroupByQuery
{
    public required ZVecQuery Query { get; init; }
    public required string GroupByField { get; init; }
    public int GroupSize { get; init; } = 1;
    public int Topk { get; init; } = 10;
    public string? Filter { get; init; }
}
```

### 7.5 Filter Builder (Fluent API)

```csharp
namespace ZVec.NET.Query;

public sealed class ZVecFilterBuilder
{
    public static ZVecFilterBuilder Create() => new();

    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, int value);
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, long value);
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, float value);
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, double value);
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, string value);
    public ZVecFilterBuilder Where(string fieldName, ZVecCompareOp op, bool value);

    public ZVecFilterBuilder And(ZVecFilterBuilder inner);
    public ZVecFilterBuilder Or(ZVecFilterBuilder inner);
    public ZVecFilterBuilder Not(ZVecFilterBuilder inner);
    public ZVecFilterBuilder In(string fieldName, params object[] values); // boxes; cold path — typed overloads deferred
    public ZVecFilterBuilder Like(string fieldName, string pattern);
    public ZVecFilterBuilder ContainAny(string fieldName, params object[] values); // boxes; cold path
    public ZVecFilterBuilder ContainAll(string fieldName, params object[] values); // boxes; cold path

    /// <summary>
    /// Generates the ZVec filter expression. String values are escaped
    /// (quotes / backslashes) so literals like O'Brien cannot break the expression.
    /// </summary>
    public override string ToString();
}

// Usage:
// var filter = ZVecFilterBuilder.Create()
//     .Where("publish_year", ZVecCompareOp.Gt, 1936)
//     .And(ZVecFilterBuilder.Create().ContainAny("category", "fiction", "romance"));
// collection.Query(query, topk: 10, filter: filter.ToString());
```

### 7.5.1 Schema Builder

```csharp
namespace ZVec.NET;

public sealed class ZVecCollectionSchemaBuilder
{
    public ZVecCollectionSchemaBuilder(string name);
    public ZVecCollectionSchemaBuilder WithMaxDocCountPerSegment(int value);
    public ZVecCollectionSchemaBuilder AddField(ZVecFieldSchema field);
    public ZVecCollectionSchemaBuilder AddField(string name, ZVecDataType dataType, bool nullable = false, ZVecInvertIndexParam? index = null);
    public ZVecCollectionSchemaBuilder AddVector(ZVecVectorSchema vector);
    public ZVecCollectionSchemaBuilder AddVector(string name, ZVecDataType dataType, int dimension, ZVecIndexParam? index = null);
    public ZVecCollectionSchema Build();
}
```

### 7.6 Reranker Types

```csharp
namespace ZVec.NET;

public abstract class ZVecReranker { }

public sealed class ZVecWeightedReranker : ZVecReranker
{
    public int TopN { get; init; }
    public ZVecMetricType Metric { get; init; }
    public IReadOnlyDictionary<string, float> Weights { get; init; }
}

public sealed class ZVecRrfReranker : ZVecReranker
{
    public int TopN { get; init; }
    public int RankConstant { get; init; } = ZVecDefaults.Rerank.RankConstant; // native int, default 60
}
```

### 7.7 Schema Types

```csharp
namespace ZVec.NET;

public sealed class ZVecCollectionSchema
{
    public required string Name { get; init; }
    public IReadOnlyList<ZVecFieldSchema> Fields { get; init; }
    public IReadOnlyList<ZVecVectorSchema> Vectors { get; init; }
    public int MaxDocCountPerSegment { get; init; } = 10_000_000;
}

public sealed class ZVecFieldSchema
{
    public required string Name { get; init; }
    public ZVecDataType DataType { get; init; }
    public bool Nullable { get; init; }
    public ZVecInvertIndexParam? IndexParam { get; init; }
}

public sealed class ZVecVectorSchema
{
    public required string Name { get; init; }
    public ZVecDataType DataType { get; init; }
    public int Dimension { get; init; }
    public ZVecIndexParam? IndexParam { get; init; }
}
```

### 7.8 Index Parameter Types

```csharp
namespace ZVec.NET;

public abstract class ZVecIndexParam { }

public sealed class ZVecHnswIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.Cosine;
    public int M { get; init; } = 16;
    public int EfConstruction { get; init; } = 200;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}

/// <summary>
/// HNSW + RaBitQ. Maps to <c>IndexType::HNSW_RABITQ = 4</c> in <c>type.h</c>.
/// <c>c_api.h</c> may omit the macro — pass value 4 until upstream adds it. x86_64/AVX2 only.
/// </summary>
public sealed class ZVecHnswRabitqIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.Cosine;
    public int M { get; init; } = 16;
    public int EfConstruction { get; init; } = 200;
    public int TotalBits { get; init; } = 7;
    public int NumClusters { get; init; } = 16;
    public int SampleCount { get; init; } = 0;
}

public sealed class ZVecIvfIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public int CentroidsNum { get; init; } = 256;
    public int Nlist { get; init; } = 16;
    public int Nprobe { get; init; } = 8;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}

public sealed class ZVecFlatIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}

public sealed class ZVecDiskAnnIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public int MaxDegree { get; init; } = 100;
    public int ListSize { get; init; } = 50;
    public int PqChunkNum { get; init; } = 0;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}

public sealed class ZVecVamanaIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public int MaxDegree { get; init; } = 64;
    public int SearchListSize { get; init; } = 100;
    public float Alpha { get; init; } = 1.2f;
    public bool SaturateGraph { get; init; } = false;
    public bool UseContiguousMemory { get; init; } = false;
    public bool UseIdMap { get; init; } = false;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}

public sealed class ZVecFtsIndexParam : ZVecIndexParam
{
    public ZVecFtsTokenizer Tokenizer { get; init; } = ZVecFtsTokenizer.Standard;
    public IReadOnlyList<ZVecFtsTokenFilter> Filters { get; init; } = [ZVecFtsTokenFilter.Lowercase];
    public ZVecFtsExtraParams? ExtraParams { get; init; }
}

public sealed class ZVecInvertIndexParam : ZVecIndexParam
{
    public bool EnableRangeOptimization { get; init; } = false;
    public bool EnableExtendedWildcard { get; init; } = false;
}
```

---

## 8. Memory & Performance Strategy

### 8.1 Zero-Copy Vector Pipeline

```
User Code:  ReadOnlyMemory<float> vec = embeddingModel.Embed("query");
                        │
                        ▼
ZVecDoc:    DenseVectors["embedding"] = vec;   // No copy — stored as Memory<T>
                        │
                        ▼
ZVecCollection.Query() / QueryAsync():
            var (ptr, pin) = VectorMarshaller.PinVector(query.Vector);
            try {
                // Upstream query/doc APIs with pinned pointer
            } finally {
                pin.Dispose();
            }
```

**Key guarantees:**
- `ReadOnlyMemory<float>` is stored in `ZVecDoc` — no `float[]` copy on the vector path
- `Memory.Pin()` for the native call duration only; dispose in `finally`
- G2 zero-allocation applies to **vectors**; scalar `Fields` may box (see §7.3)

### 8.2 SafeHandle Guarantee

Every native pointer is wrapped in a `SafeHandle` subclass:

- Consumers **must** call `Dispose()` / `await using` — that is the primary cleanup path
- If forgotten, the `SafeHandle` finalizer frees on GC — **safety net only**
- `ReleaseHandle` may run on the **finalizer thread**; P/Invoke there can deadlock if native code uses TLS or thread-affine locks. Verify `zvec_collection_close` is safe from any thread; log a diagnostic if release is detected from a finalizer (missing Dispose)
- **`ReleaseHandle` / `Dispose` call `zvec_collection_close` only** — never `destroy` (that would wipe on-disk data)
- `Dispose` and `DisposeAsync` on `ZVecCollection` must be idempotent and mutually exclusive (`Interlocked.Exchange` — cleanup once)
- Do not rely on finalizers for hot-path resource turnover

### 8.3 ArrayPool for Intermediate Native Buffers

```csharp
internal static class ResultBufferPool
{
    // Intermediate buffers for the native call only.
    // After P/Invoke returns, copy into ZVecDoc objects, then Return to the pool.
    // This is NOT zero-allocation for the final managed result list.
    internal static (int[] ids, float[] scores, int bufferSize) RentResultBuffer(int topk)
    {
        var ids = ArrayPool<int>.Shared.Rent(topk);
        var scores = ArrayPool<float>.Shared.Rent(topk);
        return (ids, scores, topk);
    }

    internal static void ReturnResultBuffer(int[] ids, float[] scores)
    {
        ArrayPool<int>.Shared.Return(ids, clearArray: true);
        ArrayPool<float>.Shared.Return(scores, clearArray: true);
    }
}
```

### 8.4 Concurrency Model (Sync + Async)

The upstream C API is **synchronous/blocking**. The managed SDK uses a minimalist concurrency
model — **no custom managed-side RW lock** — because:

- ZVec's native `GlobalConfig` uses `std::atomic<bool>` for its own thread-safe init (config.h:197)
- Factory/collection lifecycle uses `Interlocked.CompareExchange` / `Interlocked.Exchange`
- The C++ engine is responsible for its own operation-level thread safety
- The planned `AsyncReaderWriterLock` was **canceled** (redundant with native guarantees;
  CoreReview flagged correctness issues: AsyncLocal leakage, cancellation races)

```
┌──────────────┐     ┌──────────────────┐
│ Query / Fetch│     │ Insert / DDL     │
│ (sync|async) │     │ (sync|async)     │
└──────┬───────┘     └──────┬───────────┘
       │                     │
       └──────────┬──────────┘
                  ▼
┌──────────────────────────────────────┐
│  Process-wide MaxConcurrentNative    │  ← caps threads blocked in P/Invoke
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  Sync: P/Invoke on caller thread     │
│  Async: bounded offload after call   │  ← ConfigureAwait(false) in library
│  → zvec_c_api                        │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  ZVec C++ Core                       │
│  (owns its own thread safety)        │
│  + QueryThreads intra-query pool     │
└──────────────────────────────────────┘
```

**Rules:**

1. **No managed-side RW lock** — The native C++ engine handles concurrent reads and exclusive writes internally. Managed code uses `Interlocked` for lifecycle state transitions only.
2. **Sync path** — P/Invoke on caller thread. Lowest latency (console, batch jobs, MAUI background).
3. **Async path** — Bounded offload for blocking P/Invoke. Never unbounded `Task.Run` per request.
4. **Global throttle** — `MaxConcurrentNativeCalls` protects ASP.NET thread pools (`0` = auto).
5. **Native query threads** — `ZVecOptions.QueryThreads` is orthogonal (intra-query parallelism).
6. **Dispose/Destroy safety** — `Interlocked.Exchange` on `_disposed`/`_destroyed` flags ensures lifecycle operations run exactly once, even under concurrent calls.
8. **Batch over chatty P/Invoke** — Prefer list overloads.
9. **Zero-copy vectors** — Pin only for the native call duration.
10. **Cancellation** — Honored while waiting on the lock and before entering native. Mid-P/Invoke cancel is best-effort — document that limitation.

### 8.5 Memory Governance for Edge

```csharp
namespace ZVec.NET.DependencyInjection;

public sealed class ZVecOptions
{
    public ZVecLogType LogType { get; set; } = ZVecLogType.Console;
    public ZVecLogLevel LogLevel { get; set; } = ZVecLogLevel.Warn;
    public int QueryThreads { get; set; } = -1;
    public int MaxConcurrentNativeCalls { get; set; } = 0;

    /// <summary>
    /// Process-wide memory limit (MB). Maps to zvec_config_data_set_memory_limit
    /// on global init — not a per-collection option.
    /// </summary>
    public int? MemoryLimitMb { get; set; }
}

public sealed class ZVecCollectionOptions
{
    public bool ReadOnly { get; init; } = false;
    public bool EnableMmap { get; init; } = true;
    public int MaxConcurrentReads { get; init; } = 0;        // 0 = ProcessorCount
}
```

---

## 9. Cross-Platform NuGet Packaging

### 9.1 NuGet Structure

Prefer a **single** `lib/net8.0` asset when there is no TFM-specific `#if` code — net9/net10 consumers fall back to it. Multi-target locally if needed for testing.

```
ZVec.NET.nupkg/
├── lib/
│   └── net8.0/ZVec.NET.dll
├── runtimes/
│   ├── win-x64/native/zvec_c_api.dll
│   ├── win-arm64/native/zvec_c_api.dll
│   ├── linux-x64/native/libzvec_c_api.so
│   ├── linux-arm64/native/libzvec_c_api.so
│   ├── osx-x64/native/libzvec_c_api.dylib
│   └── osx-arm64/native/libzvec_c_api.dylib
├── README.md
└── (optional) ZVec.NET.snupkg alongside
```

No `build/*.props` for RID native packing — the .NET runtime resolves `runtimes/{rid}/native/` automatically.

### 9.2 .csproj NuGet Packaging Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <PackageId>ZVec.NET</PackageId>
    <AssemblyName>ZVec.NET</AssemblyName>
    <RootNamespace>ZVec.NET</RootNamespace>
    <!-- Version = our SemVer + build metadata for pinned native ZVec release.
         Example: 1.0.0-alpha.1+zvec.1.2.3 means our SDK 1.0.0-alpha.1 wrapping ZVec C++ 1.2.3.
         The .NET target is NOT in the version — it lives in TargetFrameworks + lib/ folder.
         NuGet already shows "net8.0" as the dependency framework. -->
    <Version>1.0.0-alpha.1+zvec.1.2.3</Version>
    <Authors>Adam Systems</Authors>
    <Description>High-performance .NET SDK for Alibaba ZVec — the "SQLite of Vector DBs". Zero-allocation vector pipelines (ReadOnlyMemory&lt;float&gt;), sync + async APIs, DI-first design. Wraps the official zvec_c_api C++ core with idiomatic C#: SafeHandle guarantees, HNSW/IVF/Flat/DiskANN/Vamana/FTS indexes, hybrid search, schema evolution, and cross-platform native binaries (win/linux/mac, x64/arm64).</Description>
    <PackageTags>zvec;vector-database;embeddings;HNSW;semantic-search;RAG;dotnet;alibaba;similarity-search;ann</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageReleaseNotes>Wraps ZVec C++ 1.2.3; targets .NET 8.0+ (LTS baseline)</PackageReleaseNotes>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
    <RepositoryUrl>https://github.com/AdamSystems/ZVec.NET</RepositoryUrl>
    <!-- SignAssembly + AssemblyOriginatorKeyFile come from root Directory.Build.props
         (key file: build/ZVec.NET.snk) -->
  </PropertyGroup>

  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\" />
    <Content Include="runtimes\**\*" Pack="true" PackagePath="runtimes" />
  </ItemGroup>
</Project>
```

### 9.3 Versioning (managed vs native vs .NET target)

The version encodes **three** pieces of information across different NuGet metadata fields:

| What | Where it goes | Format | Example |
|------|--------------|--------|---------|
| **Our SDK version** | `<Version>` (SemVer prefix) | `major.minor.patch[-prerelease]` | `1.0.0-alpha.1` |
| **Pinned ZVec native version** | `<Version>` build metadata (after `+`) | `+zvec.{major}.{minor}.{patch}` | `+zvec.1.2.3` |
| **Supported .NET target** | `<TargetFrameworks>` + `lib/net8.0/` folder | TFM | `net8.0` (LTS baseline) |

**Combined NuGet version:** `<Version>1.0.0-alpha.1+zvec.1.2.3</Version>`

**Why .NET target is NOT in the version string:** Per SemVer and NuGet conventions, the target framework is expressed via the TFM and the `lib/` folder structure, not the version. NuGet already displays "net8.0" as a dependency framework in the package listing. Putting it in the version would be non-standard and break tooling.

**`<PackageReleaseNotes>`** carries a human-readable summary: `"Wraps ZVec C++ 1.2.3; targets .NET 8.0+ (LTS baseline)"`

If managed adds P/Invoke for new APIs before native ships those symbols, bump the required native pin and fail the version gate clearly.

### 9.4 Announcing to Upstream (Alibaba ZVec)

Once the alpha NuGet package is published:

1. **GitHub Issue** — Open a "Community SDK" issue on `alibaba/zvec` announcing the .NET wrapper with a link to the NuGet package and repo
2. **Docs PR** — Submit a PR to ZVec's docs adding `ZVec.NET` to their SDK list (alongside Python/Node)
3. **Community channels** — Announce in ZVec's Discord/Slack channels
4. **NuGet discoverability** — `<PackageTags>` with `zvec`, `vector-database`, `alibaba` ensures developers searching NuGet for vector DBs find it

---

## 10. Testing Strategy

### 10.1 Test Categories

| Category | Scope | Native Library | Count (est.) |
|----------|-------|---------------|-------------|
| **Unit** | DTO serialization, filter building, enum mapping, SafeHandle lifecycle | Mock | ~60 |
| **Integration** | Full CRUD lifecycle, query accuracy, FTS, hybrid search, schema evolution | Real | ~40 |
| **Memory** | Zero-allocation vector verification, SafeHandle leak detection, ArrayPool recycling | Real + Mock | ~15 |
| **Concurrency** | Multi-threaded read/write under RW lock | Real | ~10 |

### 10.2 Mock Native Library — Full Specification

#### 10.2.1 Design Goals

- Implements the **entire** `c_api.h` surface (every exported function)
- Builds as `zvec_c_api` shared library (same name as real binary)
- Unit tests link against mock; integration tests link against real
- Switching via `NativeLibrary.SetDllImportResolver` or `LD_LIBRARY_PATH`

#### 10.2.2 Architecture

```cpp
// zvec_c_api_mock.cpp — namespace ::mock (internal linkage)

#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>
#include <cmath>
#include <algorithm>
#include <cstring>
#include "zvec/c_api.h"  // Include upstream header to ensure signature compatibility

namespace mock {

// Thread-safe global state
static std::mutex g_mutex;
static bool g_initialized = false;
static zvec_config_data_t* g_config = nullptr;
static int g_version_major = 0, g_version_minor = 3, g_version_patch = 0;

// Per-collection store
struct MockDocument {
    std::string pk;
    std::unordered_map<std::string, std::string> string_fields;
    std::unordered_map<std::string, std::vector<float>> vector_fields;
    float score = 0.0f;
};

struct MockCollection {
    std::string path;
    std::string name;
    bool is_open = true;
    std::unordered_map<std::string, MockDocument> documents;  // pk -> doc
};

static std::unordered_map<std::string, MockCollection> g_collections;  // path -> collection

// Error state (thread-local to match upstream behavior)
static thread_local zvec_error_code_t t_last_error = ZVEC_OK;
static thread_local std::string t_last_error_msg;

} // namespace mock
```

#### 10.2.3 Function Categories & Implementation Depth

| Category | # Functions | Mock Depth | Notes |
|----------|-----------|------------|-------|
| Version | 4 | Full | Return hardcoded version matching `ExpectedMajor/Minor/Patch` |
| Error handling | 3 | Full | Thread-local storage; `zvec_get_last_error` allocates via `zvec_malloc` |
| String/array memory | 15 | Full | Simple alloc/free with `std::vector` backing |
| Config + Init/Shutdown | 20+ | Full | Parse config; track initialized state |
| Index params | 30+ | Stub | `zvec_index_params_create` stores type; getters return defaults; setters accept and store values |
| Field schema | 15+ | Full | Store name, type, dimension, nullable, index params |
| Collection schema | 15+ | Full | Store name, fields list; `add_field`/`alter_field`/`drop_field`/`add_index`/`drop_index` mutate |
| Collection options | 5 | Full | Store mmap, read_only, max_buffer_size |
| Collection lifecycle | 6 | Full | `create_and_open` makes directory + in-memory store; `open` loads existing; `close`/`destroy` as expected |
| Query params (6 types) | 30+ | Stub | Store params; getters return stored values |
| Vector query | 10+ | Full | Store field_name, vector data (copy), filter, topk, query_params |
| FTS query | 5 | Stub | Store query_string/match_string; mock search returns all docs with matching fields |
| Group-by query | 8 | Partial | Store params; group results by specified field |
| Multi-query + sub-query | 10+ | Full | Store sub-queries; mock reranking merges results |
| Doc create/destroy/get/set | 15+ | Full | Store pk, fields, vectors; getters return stored values |
| DML (insert/upsert/update/delete) | 10 | Full | Mutate `MockCollection::documents`; return success/error counts |
| DQL (query/multi_query/fetch) | 3 | **Core logic** | See §10.2.4 |
| DDL (add_column/drop_column/alter_column) | 3 | Full | Mutate schema fields |
| Stats | 3 | Full | Return doc_count, index_count from store |
| Utility (to_string converters) | 4 | Full | Switch on enum value, return static strings |

#### 10.2.4 Mock Query Implementation (Brute-Force)

This is the most important mock logic — it must produce **plausible ranked results**:

```cpp
// Simplified brute-force cosine similarity search
extern "C" ZVEC_EXPORT zvec_error_code_t ZVEC_CALL
zvec_collection_query(const zvec_collection_t* collection,
                       const zvec_vector_query_t* query,
                       zvec_doc_t*** results, size_t* result_count)
{
    auto* col = reinterpret_cast<mock::MockCollection*>(collection);
    auto* q = reinterpret_cast<mock::MockQueryState*>(query);

    std::lock_guard<std::mutex> lock(mock::g_mutex);

    // Get query vector from stored state
    const float* query_vec = q->query_vector_data;
    size_t query_dim = q->query_vector_size / sizeof(float);

    // Score every document
    struct ScoredDoc {
        mock::MockDocument* doc;
        float score;
    };
    std::vector<ScoredDoc> scored;

    for (auto& [pk, doc] : col->documents) {
        auto it = doc.vector_fields.find(q->field_name);
        if (it == doc.vector_fields.end()) continue;

        const auto& vec = it->second;
        if (vec.size() != query_dim) continue;

        // Cosine similarity
        float dot = 0, normA = 0, normB = 0;
        for (size_t i = 0; i < query_dim; i++) {
            dot += query_vec[i] * vec[i];
            normA += query_vec[i] * query_vec[i];
            normB += vec[i] * vec[i];
        }
        float sim = (normA > 0 && normB > 0) ? dot / (std::sqrt(normA) * std::sqrt(normB)) : 0;

        scored.push_back({&doc, sim});
    }

    // Sort by score descending
    std::sort(scored.begin(), scored.end(),
              [](const auto& a, const auto& b) { return a.score > b.score; });

    // Apply topk
    int topk = q->topk > 0 ? q->topk : 10;
    size_t count = std::min(static_cast<size_t>(topk), scored.size());

    // Allocate result array
    *results = static_cast<zvec_doc_t**>(mock_alloc(sizeof(zvec_doc_t*) * count));
    for (size_t i = 0; i < count; i++) {
        (*results)[i] = create_mock_doc_from_document(scored[i].doc, scored[i].score);
    }
    *result_count = count;

    return ZVEC_OK;
}
```

#### 10.2.5 Build & Integration (CMake)

```cmake
# src/Mock/ZVec.Native.Mock/CMakeLists.txt
cmake_minimum_required(VERSION 3.16)
project(zvec_c_api_mock CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

add_library(zvec_c_api_mock SHARED
    src/zvec_c_api_mock.cpp
)

target_include_directories(zvec_c_api_mock PRIVATE
    ${CMAKE_CURRENT_SOURCE_DIR}/../../Native/ZVec.Native/external/zvec/src/include
)

# Output with the same name as the real library
set_target_properties(zvec_c_api_mock PROPERTIES
    OUTPUT_NAME "zvec_c_api"   # Same name for DllImportResolver switching
    WINDOWS_EXPORT_ALL_SYMBOLS ON
)
```

#### 10.2.6 C# Switching Mechanism

```csharp
// NativeLibraryResolver.cs
internal static class NativeLibraryResolver
{
    private static string? _mockLibraryPath;
    private static bool _useMock;

    internal static void UseMockLibrary(string mockPath)
    {
        _mockLibraryPath = mockPath;
        _useMock = true;
    }

    internal static void UseRealLibrary() => _useMock = false;

    internal static void EnsureRegistered()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
    }

    private static IntPtr Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (name != "zvec_c_api") return IntPtr.Zero;

        if (_useMock && _mockLibraryPath is not null)
        {
            if (NativeLibrary.TryLoad(_mockLibraryPath, out var handle))
                return handle;
        }

        // Fall back to real library from runtimes/{rid}/native/
        if (NativeLibrary.TryLoad(name, assembly, searchPath, out var realHandle))
            return realHandle;

        throw new DllNotFoundException(
            $"ZVec native library not found for RID '{RuntimeInformation.RuntimeIdentifier}'.");
    }
}
```

**Advisory alternative:** a pure managed mock that returns function pointers through `SetDllImportResolver` (no C++ mock project) — optional later; v1 keeps the C++ mock.

### 10.3 Concurrency Testing Framework

#### 10.3.1 Framework Choice

No external concurrency testing framework is required — raw `Task` + `xUnit` assertions + `Interlocked` + `Barrier` are sufficient. The .NET BCL provides everything:

- `Task.WhenAll` for concurrent launch
- `SemaphoreSlim` for throttling
- `CancellationTokenSource` for cancellation
- `Interlocked` for thread-safe counters
- `xUnit` `[Fact]` with `async Task` for async tests

For **systematic concurrency testing**, add:

```xml
<PackageReference Include="System.Threading.Channels" Version="8.0.0" />
<!-- No special testing package — use raw Task + xUnit assertions -->
```

#### 10.3.2 ~~AsyncReaderWriterLock~~ Test Suite

**🗑️ Canceled** — This subsection described correctness + stress + linearizability tests for the
`AsyncReaderWriterLock` component, which was removed from the codebase. The native C++ engine
handles its own thread safety; no managed-side RW lock exists to test.

#### 10.3.3 Collection-Level Concurrency Tests (~8 tests)

These test the `ZVecCollection`'s use of the lock with real (or mock) native calls:

| # | Test | Scenario |
|---|------|----------|
| 1 | `ConcurrentReads_DontBlock` | 10 `QueryAsync` calls complete concurrently |
| 2 | `WriteBlocksReads` | `InsertAsync` blocks `QueryAsync` until complete |
| 3 | `MixedReadWrite_NoCorruption` | 5 readers + 2 writers, all complete without exception |
| 4 | `Cancellation_DuringQuery` | Cancel `QueryAsync` while waiting on lock |
| 5 | `DisposeDuringActiveRead` | `DisposeAsync` waits for active reads to finish |
| 6 | `DestroyDuringActiveRead` | `DestroyAsync` waits for active reads, then destroys |
| 7 | `MaxConcurrentReads_Throttles` | `MaxConcurrentReads=4` limits to 4 concurrent native calls |
| 8 | `GlobalThrottle_LimitsNativeCalls` | `MaxConcurrentNativeCalls=8` caps concurrent P/Invokes |

#### 10.3.4 Test Infrastructure Helpers

```csharp
// Test helpers for deterministic concurrency testing
internal static class ConcurrencyTestHelper
{
    /// <summary>
    /// Runs an action on multiple threads simultaneously, with a barrier
    /// to ensure all threads start at the same time.
    /// </summary>
    internal static async Task RunConcurrently(int threadCount, Func<int, Task> action)
    {
        var barrier = new Barrier(threadCount);
        var tasks = Enumerable.Range(0, threadCount)
            .Select(i => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                await action(i);
            }));
        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Verifies no deadlock by timing out after a specified duration.
    /// </summary>
    internal static async Task VerifyNoDeadlock(Func<Task> action, TimeSpan timeout)
    {
        var task = action();
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        if (completed != task)
            throw new TimeoutException($"Deadlock suspected: operation did not complete within {timeout}");
        await task; // Propagate exceptions
    }
}
```

### 10.4 Test Examples

```csharp
using ZVec.NET;
using ZVec.NET.Query;

// Unit: Filter builder
[Fact]
public void FilterBuilder_CompoundExpression_GeneratesCorrectString()
{
    var filter = ZVecFilterBuilder.Create()
        .Where("publish_year", ZVecCompareOp.Gt, 1936)
        .And(ZVecFilterBuilder.Create().ContainAny("category", "fiction", "romance"));

    filter.ToString().Should().Be("publish_year > 1936 AND category CONTAIN_ANY [\"fiction\", \"romance\"]");
}

// Integration: CRUD lifecycle (sync path)
[Fact]
public void Collection_InsertFetchDelete_Lifecycle()
{
    using var factory = /* resolve IZvecFactory */;
    factory.Initialize();
    using var col = factory.CreateAndOpen(tempPath, schema);

    var doc = ZVecDoc.Create("doc1",
        denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
        {
            ["embedding"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
        },
        fields: new Dictionary<string, object> { ["title"] = "Test" });

    var insertResult = col.Insert(doc);
    insertResult.Code.Should().Be(ZVecErrorCode.Ok);

    var fetched = col.Fetch("doc1");
    fetched.Should().NotBeNull();
    fetched!.Fields["title"].Should().Be("Test");

    var deleteResult = col.Delete("doc1");
    deleteResult.Code.Should().Be(ZVecErrorCode.Ok);
}

// Integration: async path
[Fact]
public async Task Collection_QueryAsync_ReturnsHits()
{
    await factory.InitializeAsync();
    await using var col = await factory.OpenAsync(tempPath);
    var results = await col.QueryAsync(
        new ZVecQuery { FieldName = "embedding", Vector = memory },
        topk: 10);
    results.Should().NotBeEmpty();
}

// Memory: vector path — prefer BenchmarkDotNet [MemoryDiagnoser] for accuracy.
// GC.GetAllocatedBytesForCurrentThread includes framework noise; keep threshold tight.
[Fact]
public void Query_WithPinnedMemory_DoesNotAllocateFloatArrayCopy()
{
    var vector = new float[768];
    var memory = new ReadOnlyMemory<float>(vector);

    long before = GC.GetAllocatedBytesForCurrentThread();
    _ = collection.Query(new ZVecQuery { FieldName = "embedding", Vector = memory }, topk: 10);
    long after = GC.GetAllocatedBytesForCurrentThread();

    // Must not copy 768*4 bytes; allow small overhead only (prefer BDN for CI gates)
    (after - before).Should().BeLessThan(256);
}
```

---

## 11. Benchmark Strategy

### 11.1 Benchmark Categories

| Benchmark | Measures | Comparison |
|-----------|----------|-----------|
| `VectorMarshallingBench` | Time to pass 768-dim vector through P/Invoke | vs. raw `DllImport` with `float[]` |
| `QueryThroughputBench` | QPS for single-vector search (sync path) | vs. Python ZVec SDK |
| `InsertThroughputBench` | Docs/sec for batch insert | vs. Python ZVec SDK |
| `MemoryDiagnosisBench` | GC allocations per operation | Near-zero on vector query path |
| `FilterParsingBench` | Filter builder string generation | Must be < 1 µs |

### 11.2 BenchmarkDotNet Configuration

```csharp
using ZVec.NET;

[MemoryDiagnoser]
[RankColumn]
public class QueryThroughputBench
{
    private IZvecCollection _collection = null!;
    private ReadOnlyMemory<float> _queryVector;
    private float[] _queryArray;

    [GlobalSetup]
    public void Setup()
    {
        var factory = /* build IZvecFactory */;
        factory.Initialize();
        _collection = factory.Open("/bench_collection");
        _queryVector = new float[768];
        _queryArray = new float[768];
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<ZVecDoc> Query_ReadOnlyMemory()
    {
        return _collection.Query(
            new ZVecQuery { FieldName = "embedding", Vector = _queryVector },
            topk: 10);
    }

    /// <summary>
    /// Forces a managed copy so GC allocation differs from the zero-copy baseline.
    /// Note: passing float[] alone does NOT copy — it wraps as ReadOnlyMemory without alloc.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_ExplicitCopy()
    {
        float[] copy = _queryArray.ToArray();
        return _collection.Query(
            new ZVecQuery { FieldName = "embedding", Vector = copy },
            topk: 10);
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_WithFilter()
    {
        return _collection.Query(
            new ZVecQuery { FieldName = "embedding", Vector = _queryVector },
            topk: 10,
            filter: "publish_year > 2000");
    }
}
```

### 11.3 Performance Targets

| Metric | Target | Baseline |
|--------|--------|---------|
| Sync P/Invoke overhead (768-dim vector) | < 50 µs | `DllImport float[]` ≈ 30 µs |
| Single-vector query (10k docs, topk=10) | < 1 ms | Python ZVec ≈ 1.2 ms |
| Batch insert (1000 docs) | > 50k docs/sec | Python ZVec ≈ 40k docs/sec |
| GC allocation per query (vector path) | < 256 B intermediate + result objects | `float[]` copy ≈ 3 KB |
| Filter builder string generation | < 1 µs | String concatenation ≈ 0.5 µs |

---

## 12. Work Breakdown Structure (WBS)

### Recently Completed Epics
- **Epic E14 (Schema DDL)**: Fully implemented DDL methods in `ZVecCollection` (`AddColumn`, `DropColumn`, `AlterColumn`, etc.) and validated with tests.
- **Epic E16 (Dependency Injection)**: Integrated `Microsoft.Extensions.DependencyInjection` with `AddZVec` and `AddZVecCollection` using keyed singletons and DI options.
- **Epic E17 (Mock Native Library)**: Created `zvec_c_api_mock.cpp` and `mock_structs.h` using CMake, removing the legacy C# mock and establishing a cross-platform mock matching the C API.

### Phase 1: Native Layer (upstream `zvec_c_api`) — ~2.5 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 1.1 | Submodule + CMake wrapper for upstream `zvec_c_api` | C++ Lead | — | 2d |
| 1.2 | Audit §2.5 vs `c_api.h`; freeze gap table | C++ Lead | 1.1 | 1d |
| 1.3 | Align P/Invoke surface with upstream `c_api.h` | C++ Lead | 1.2 | 1d |
| 1.4 | Wire global init + version APIs + schema construction | C++ Lead | 1.3 | 2d |
| 1.5 | Wire collection lifecycle (create_and_open, open, close, destroy) | C++ Lead | 1.4 | 2d |
| 1.6 | Wire insert/upsert/update/delete | C++ Lead | 1.4 | 3d |
| 1.7 | Wire query/fetch | C++ Lead | 1.4 | 3d |
| 1.8 | Wire optimize + schema DDL | C++ Lead | 1.4 | 2d |
| 1.9 | Compile test libraries (win-x64, linux-x64) | C++ Lead | 1.6-1.8 | 1d |
| 1.10 | Cross-compile macOS + ARM64 | C++ Lead | 1.9 | 2d |
| 1.11 | Implement mock C-API library | C++ Dev | 1.3 | 5–8d |

### Phase 2: P/Invoke Layer — ~2 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 2.1 | Create ZVec.NET class library | .NET Lead | — | 0.5d |
| 2.2 | Define C# enums matching Appendix A | .NET Lead | 1.2 | 0.5d |
| 2.3 | Implement `[LibraryImport]` (`U1` bools, version APIs) | .NET Lead | 2.1, 1.3 | 2d |
| 2.4 | SafeHandles + finalizer/Dispose guidance | .NET Lead | 2.3 | 1d |
| 2.5 | VectorMarshaller + ArrayPool | .NET Lead | 2.3 | 2d |
| 2.6 | NativeLibraryResolver + clear DllNotFound | .NET Lead | 2.3 | 0.5d |
| 2.7 | Unit tests for SafeHandle + enum mapping | .NET Dev | 2.4, 2.2 | 2d |

### Phase 3: Public SDK — ~3.5 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 3.0 | `IZvecFactory` / `IZvecCollection` sync+async + DI + Shutdown | .NET Lead | 2.2 | 1.5d |
| 3.1 | DTOs + SchemaBuilder + typed query params | .NET Lead | 2.2 | 2d |
| 3.2 | `ZVecFactory` (init, first-init-wins, version gate, create/open, shutdown) | .NET Lead | 2.3, 3.0, 3.1 | 2d |
| 3.3 | ~~Custom `Internal.AsyncReaderWriterLock` + unit tests~~ 🗑️ Canceled | — | — | — |
| 3.4 | `ZVecCollection` CRUD sync+async + RW lock + close-vs-destroy | .NET Lead | 3.2, 3.3 | 4d |
| 3.5 | Query sync+async (single + multi-vector) | .NET Lead | 3.4 | 3d |
| 3.6 | Optimize / Destroy sync+async (destroy then close) | .NET Lead | 3.2 | 0.5d |
| 3.7 | Schema DDL sync+async | .NET Lead | 3.2 | 2d |
| 3.8 | `ZVecFilterBuilder` (`ZVecCompareOp` + escaping) | .NET Lead | 3.1 | 1.5d |
| 3.9 | Reranker types | .NET Lead | 3.1 | 1d |
| 3.10 | Collection properties | .NET Lead | 3.2 | 1d |
| 3.11 | Integration tests | .NET Dev | 3.4-3.10 | 5–7d |
| 3.12 | Memory/concurrency tests | .NET Dev | 3.3-3.10 | 3d |

### Phase 4: CI/CD & Publishing — ~2.5 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 4.1 | GitHub Actions: C++ matrix (6 RIDs) | DevOps | 1.9 | 3–5d |
| 4.2 | GitHub Actions: .NET build + test | DevOps | 2.1 | 1d |
| 4.3 | NuGet pack (`PackageLicenseExpression`, snupkg, README, runtimes/) | .NET Lead | 4.1, 4.2 | 2–3d |
| 4.4 | BenchmarkDotNet suite | .NET Dev | 3.11 | 3d |
| 4.5 | Validate performance targets | .NET Dev | 4.4 | 1d |
| 4.6 | Publish alpha NuGet | .NET Lead | 4.3 | 0.5d |
| 4.7 | README + getting-started | .NET Lead | 4.6 | 1d |


### Total Estimated Duration: **~10–12 weeks** (2 developers)

Optimistic calendar floor remains ~8 weeks if native/CI goes smoothly; plan to **10–12 weeks** for cross-platform RIDs, packaging, and integration depth.

---

## 13. CI/CD Pipeline

### 13.1 Build Matrix

```yaml
# .github/workflows/build-native.yml
strategy:
  matrix:
    include:
      - os: windows-latest
        rid: win-x64
        cmake_generator: "Ninja"
      - os: windows-latest
        rid: win-arm64
        cmake_generator: "Ninja"
        cmake_extra: "-A ARM64"
      - os: ubuntu-latest
        rid: linux-x64
        cmake_generator: "Unix Makefiles"
      - os: ubuntu-latest
        rid: linux-arm64
        cmake_generator: "Unix Makefiles"
        cmake_extra: "-DCMAKE_TOOLCHAIN_FILE=toolchain-arm64.cmake"
      - os: macos-latest
        rid: osx-x64
        cmake_generator: "Unix Makefiles"
      - os: macos-latest
        rid: osx-arm64
        cmake_generator: "Unix Makefiles"
        cmake_extra: "-DCMAKE_OSX_ARCHITECTURES=arm64"
```

### 13.2 Pipeline Stages

1. **Build Native** — Compile `zvec_c_api` for all 6 RIDs
2. **Build Managed** — `dotnet build` ZVec.NET
3. **Test (Mock)** — Unit tests against mock native library
4. **Test (Integration)** — Integration tests against real ZVec binaries (Linux x64)
5. **Benchmark** — BenchmarkDotNet on Linux x64
6. **Pack** — `dotnet pack` with `runtimes/` + symbols
7. **Publish** — Push to nuget.org (release tag only)

### 13.3 Cross-Compilation Strategy for 6 RIDs (No ARM64 Hardware)

#### 13.3.1 Problem

You don't own ARM64 devices. Building native ARM64 binaries requires cross-compilation. Testing them requires emulation.

#### 13.3.2 Strategy: CI-First Cross-Compilation + QEMU Emulation

| RID | Build Host | Cross-Compile Method | Test Method |
|-----|-----------|---------------------|-------------|
| **win-x64** | `windows-latest` | Native build (MSVC) | Run directly |
| **win-arm64** | `windows-latest` | MSVC with `-A ARM64` | **No run test** — compile-only gate |
| **linux-x64** | `ubuntu-latest` | Native build (GCC) | Run directly |
| **linux-arm64** | `ubuntu-latest` | `aarch64-linux-gnu-gcc` cross-compiler | **QEMU user-mode** (`qemu-aarch64`) |
| **osx-x64** | `macos-latest` (Intel runner) | Native build (Clang) | Run directly |
| **osx-arm64** | `macos-latest` (M-series runner) | Native build (Clang) | Run directly (GitHub now has M1 runners) |

#### 13.3.3 CI Workflow: Cross-Compile + Emulated Test

```yaml
# .github/workflows/build-native.yml — linux-arm64 job
build-linux-arm64:
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
      with:
        submodules: recursive

    - name: Install cross-compilation toolchain
      run: |
        sudo apt-get update
        sudo apt-get install -y gcc-aarch64-linux-gnu g++-aarch64-linux-gnu qemu-user

    - name: Configure CMake for ARM64
      run: |
        cmake -B build-arm64 \
          -DCMAKE_SYSTEM_NAME=Linux \
          -DCMAKE_SYSTEM_PROCESSOR=aarch64 \
          -DCMAKE_C_COMPILER=aarch64-linux-gnu-gcc \
          -DCMAKE_CXX_COMPILER=aarch64-linux-gnu-g++ \
          -S src/Native/ZVec.Native

    - name: Build
      run: cmake --build build-arm64 --config Release

    - name: Test with QEMU
      run: |
        # Run a simple smoke test binary that exercises the built library
        qemu-aarch64 -L /usr/aarch64-linux-gnu \
          ./build-arm64/smoke_test_zvec
```

#### 13.3.4 Windows ARM64 Strategy

Windows ARM64 is the hardest because QEMU can't run Windows binaries. Strategy:

1. **Compile-only gate:** Build with MSVC `-A ARM64` in CI. This verifies the code compiles correctly for ARM64.
2. **No run test in CI** — document this as a known gap.
3. **Community testing:** Add a GitHub Issue template: "ARM64 Windows Testing Report" where users can report success/failure.
4. **Alternative:** Use Windows ARM64 runners when GitHub makes them generally available (currently in preview).

#### 13.3.5 Build Matrix Summary

```yaml
strategy:
  fail-fast: false
  matrix:
    include:
      - os: windows-latest
        rid: win-x64
        cmake_extra: ""
        test: true

      - os: windows-latest
        rid: win-arm64
        cmake_extra: "-A ARM64"
        test: false  # compile-only

      - os: ubuntu-latest
        rid: linux-x64
        cmake_extra: ""
        test: true

      - os: ubuntu-latest
        rid: linux-arm64
        cmake_extra: >
          -DCMAKE_SYSTEM_NAME=Linux
          -DCMAKE_SYSTEM_PROCESSOR=aarch64
          -DCMAKE_C_COMPILER=aarch64-linux-gnu-gcc
          -DCMAKE_CXX_COMPILER=aarch64-linux-gnu-g++
        test: true  # via QEMU
        cross: true

      - os: macos-latest
        rid: osx-x64
        cmake_extra: ""
        test: true

      - os: macos-latest
        rid: osx-arm64
        cmake_extra: "-DCMAKE_OSX_ARCHITECTURES=arm64"
        test: true  # M-series runner
```

#### 13.3.6 Local Development Without ARM64

For developers without ARM64 hardware:

1. **Docker + QEMU:** Run `docker run --platform linux/arm64 ...` with QEMU emulation.
2. **CI is the gate:** Don't build ARM64 locally. Push to CI and let the matrix handle it.
3. **The mock library** (`zvec_c_api_mock`) is x64-only — all unit tests run on x64. ARM64 testing is integration-only.

---

## 14. Risk Register

### 14.1 Summary Table

| # | Risk | Impact | Probability | Status |
|---|------|--------|-------------|--------|
| R1 | ZVec C++ ABI changes between versions | High | Medium | ✅ Mitigated |
| R2 | P/Invoke marshalling overhead exceeds 50µs (sync path) | High | Low | ✅ Mitigated |
| R3 | Cross-compilation fails for ARM64 | Medium | Medium | ✅ Mitigated (§13.3) |
| R4 | NuGet package size too large (>50MB) | Medium | Low | ✅ Mitigated |
| R5 | GC pauses / Field boxing on hot paths | Medium | Medium | ✅ Mitigated |
| R6 | Filter expression / escaping bugs | Medium | Medium | ✅ Mitigated |
| R7 | .NET multi-target / SDK churn | Medium | Low | ✅ Mitigated |
| R8 | SafeHandle finalizer-thread P/Invoke deadlock | High | Low | ✅ Mitigated |
| R9 | Dual-semaphore race if RW lock regresses | High | Low | ✅ Mitigated |
| R10 | Accidental on-disk delete via Dispose | High | Low | ✅ Mitigated |
| R11 | `LPUTF8Str` / wrong free on version or error strings | High | Low | ✅ Mitigated |
| R12 | Upstream license change (non-permissive) | High | Low | ✅ Mitigated |
| R13 | Binding gaps (missing C API for documented DB features) | Medium | Medium | ✅ Closed (§2.0 audit) |
| R14 | Native DLL name conflict with another package | Medium | Low | ✅ Mitigated |

### 14.2 Detailed Mitigations

#### R1: ZVec C++ ABI changes between versions

The version gate already mitigates this, but we add a **compile-time lock**:

```csharp
// NativeMethods.cs — pinned at build time
internal const int ExpectedMajor = 0;  // TODO: set from zvec submodule tag
internal const int ExpectedMinor = 3;
internal const int ExpectedPatch = 0;

// During Initialize:
if (!NativeMethods.zvec_check_version(ExpectedMajor, ExpectedMinor, ExpectedPatch))
    throw new ZVecAbiMismatchException(...);
```

**CI enforcement:** GitHub Action checks that the submodule tag matches `ExpectedMajor/Minor/Patch` constants. If someone bumps the submodule without updating the constants, CI fails.

**NuGet metadata:** `<Version>{sdk-semver}+zvec.{major}.{minor}.{patch}</Version>` makes the pinned version visible to consumers.

---

#### R2: P/Invoke marshalling overhead exceeds 50µs

Already using `[LibraryImport]` (source-generated, no runtime reflection). Additional mitigation:

```csharp
// Use unsafe pointers for vector hot path instead of managed arrays:
[LibraryImport(LibraryName)]
internal static partial int zvec_vector_query_set_query_vector(
    zvec_vector_query_t* query, void* data, UIntPtr size);
```

**Benchmark gate in CI:** BenchmarkDotNet test runs on every PR targeting the `dev` branch. If sync P/Invoke overhead exceeds 50µs on a 768-dim vector, the PR is blocked.

---

#### R3: Cross-compilation fails for ARM64

See §13.3 for the dedicated cross-compilation strategy. Key points: CI-first with QEMU emulation for linux-arm64, compile-only gate for win-arm64, native runners for macOS.

---

#### R4: NuGet package size too large (>50MB)

1. Build native libraries with `CMAKE_BUILD_TYPE=Release` and strip debug symbols:
   ```cmake
   set(CMAKE_BUILD_TYPE Release)
   set(CMAKE_CXX_FLAGS_RELEASE "-O2 -DNDEBUG")
   if(NOT WIN32)
       add_link_options(-s)  # strip symbols
   endif()
   ```

2. Size budget per RID:
   | RID | Expected Size | Stripped? |
   |-----|--------------|-----------|
   | win-x64 | ~8 MB | Yes (.pdb separate) |
   | win-arm64 | ~7 MB | Yes |
   | linux-x64 | ~6 MB | Yes (strip) |
   | linux-arm64 | ~5 MB | Yes (strip) |
   | osx-x64 | ~6 MB | Yes (strip) |
   | osx-arm64 | ~5 MB | Yes (strip) |
   | **Total** | **~37 MB** | |

3. CI gate: `dotnet pack` fails if `.nupkg` exceeds 50 MB.

4. Fallback: If total exceeds 50 MB, split into RID-specific packages (`ZVec.NET.runtime.win-x64`, etc.) with a meta-package.

---

#### R5: GC pauses / Field boxing on hot paths

1. Vector path is already zero-allocation (`ReadOnlyMemory<float>` + `Memory.Pin()`).
2. Document that scalar `Fields` boxing is intentional and acceptable:
   ```csharp
   /// <summary>
   /// Scalar fields. Values are boxed (object) for Python/Node parity.
   /// Zero-allocation goal (G2) applies to VECTOR paths only — iterating
   /// Fields on a hot query path allocates. Prefer vector-only access when measuring GC.
   /// </summary>
   public IReadOnlyDictionary<string, object> Fields { get; init; }
   ```
3. Add `ZVecQuery.OutputFields` (maps to `zvec_vector_query_set_output_fields`) so consumers can request only needed fields, reducing boxing.

---

#### R6: Filter expression / escaping bugs

1. `ZVecFilterBuilder` escapes single quotes, backslashes, and special characters:
   ```csharp
   private static string EscapeString(string value) =>
       value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\"", "\\\"");
   ```
2. **Integration test gate:** Every filter builder test has a mirror integration test that runs the generated filter against the real ZVec engine. This catches escaping bugs that unit tests miss.
3. Fuzz testing: Generate random filter expressions with special characters and verify no crashes.

---

#### R7: .NET multi-target / SDK churn

1. Ship a single `lib/net8.0/` asset when no TFM-specific `#if` code exists. net9/net10 consumers fall back automatically.
2. Pin `<LangVersion>latest</LangVersion>` but avoid preview features.
3. CI tests against net8.0 and net9.0 only — net10.0 is optional until GA.

---

#### R8: SafeHandle finalizer-thread P/Invoke deadlock

1. `ReleaseHandle` calls `zvec_collection_close` only — verified thread-safe by upstream (uses `shared_ptr` with atomic refcount, no TLS).
2. Add diagnostic logging when finalizer runs:
   ```csharp
   protected override bool ReleaseHandle()
   {
       if (!IsInvalid)
       {
           if (!Environment.HasShutdownStarted && Thread.CurrentThread.IsThreadPoolThread)
               Debug.WriteLine("[ZVec] WARNING: SafeZvecHandle released from finalizer — missing Dispose()");
           _ = NativeMethods.zvec_collection_close(handle);
       }
       return true;
   }
   ```
3. Integration test: Force GC + `WaitForPendingFinalizers` after abandoning a collection, verify no deadlock within 5 seconds.

---

#### R9: Dual-semaphore race ~~if RW lock regresses~~

**🗑️ No longer applicable** — The `AsyncReaderWriterLock` was canceled; there is no custom
managed-side lock to regress against. Native C++ owns all operation-level thread safety.
Managed code uses `Interlocked` for lifecycle state only — the `SemaphoreSlim`/`ReaderWriterLockSlim`
restriction does not apply.

---

#### R10: Accidental on-disk delete via Dispose

1. `Dispose`/`ReleaseHandle` calls `zvec_collection_close` ONLY — never `destroy`.
2. `Destroy`/`DestroyAsync` is a separate explicit method that calls `zvec_collection_destroy` then `zvec_collection_close`.
3. After `Destroy`, mark the `SafeHandle` invalid so subsequent `Dispose` is a no-op.
4. Integration test:
   ```csharp
   [Fact]
   public void Dispose_DoesNotDeleteOnDiskData()
   {
       using var col = factory.CreateAndOpen(tempPath, schema);
       col.Insert(doc);
       col.Dispose();
       // Re-open and verify data persists
       using var col2 = factory.Open(tempPath);
       var fetched = col2.Fetch("doc1");
       fetched.Should().NotBeNull();
   }
   ```

---

#### R11: LPUTF8Str / wrong free on version or error strings

1. `zvec_get_version()` returns `const char*` — library-owned static memory. P/Invoke as `IntPtr`, read with `Marshal.PtrToStringUTF8`, **never free**.
2. `zvec_get_last_error()` returns `char**` — caller must free with `free()` (libc). P/Invoke as `out IntPtr`, read with `Marshal.PtrToStringUTF8`, then free with `zvec_free()` (the library's own allocator):
   ```csharp
   internal static string GetLastErrorMessage()
   {
       int rc = NativeMethods.zvec_get_last_error(out IntPtr msgPtr);
       if (msgPtr == IntPtr.Zero) return string.Empty;
       string msg = Marshal.PtrToStringUTF8(msgPtr) ?? string.Empty;
       NativeMethods.zvec_free(msgPtr); // Use library's free(), NOT Marshal.FreeCoTaskMem
       return msg;
   }
   ```
3. **`zvec_free` P/Invoke declaration:**
   ```csharp
   [LibraryImport(LibraryName)]
   internal static partial void zvec_free(IntPtr ptr);
   ```

---

#### R12: Upstream license change (non-permissive)

1. CI step checks the LICENSE file in the submodule:
   ```yaml
   - name: Verify upstream license
     run: |
       head -1 src/Native/ZVec.Native/external/zvec/LICENSE | grep -q "Apache License"
   ```
2. If license changes, CI fails and blocks the build.
3. NuGet metadata: `<PackageLicenseExpression>MIT</PackageLicenseExpression>` covers our package. The upstream Apache-2.0 license is for the native binary which we redistribute — legally compatible with MIT wrapping.

---

#### R13: Binding gaps (missing C API for documented DB features)

This is now **closed** — see §2.0 audit. Only 2 gaps exist (`HNSW_RABITQ=4`, `RABITQ=4`), both have concrete resolutions using `type.h` values.

---

#### R14: Native DLL name conflict with another package

1. The native library is named `zvec_c_api` (upstream's choice). If another NuGet package also ships `zvec_c_api.dll`, the .NET runtime loads only one.
2. Mitigation: Use `NativeLibrary.SetDllImportResolver` to load from our package's `runtimes/` path explicitly.
3. Future option: If conflict occurs, rename the native binary to `adamsystems_zvec_c_api` and add a `DllImportResolver` alias. This is a breaking change and only done if a real conflict is reported.

---

## 15. Timeline & Milestones

| Milestone | Date (Target) | Deliverable |
|-----------|--------------|-------------|
| **M1: Native MVP** | Week 3 | Upstream `c_api.h` wired; win-x64 + linux-x64 binaries; mock library started |
| **M2: P/Invoke Layer** | Week 5 | `NativeMethods` + SafeHandles + version gate + VectorMarshaller; unit tests |
| **M3: Public SDK Alpha** | Week 8–9 | DI + sync/async CRUD/Query/FTS/DDL + RW lock; integration tests |
| **M4: Cross-Platform Pack** | Week 10–11 | CI builds 6 RIDs; NuGet pack (license expression, snupkg, README) |
| **M5: Benchmark & Release** | Week 11–12 | Performance targets validated; alpha `ZVec.NET` published |

---

## Appendix A — ZVec Enum Reference

> **Sole numeric enum / error-code table in this document.** Do not reintroduce parallel enum lists in §5/§6.
>
> **Source of truth for numeric values:** `src/Native/ZVec.Native/external/zvec/src/include/zvec/db/type.h`
> (and matching macros in `c_api.h` when present). Prefer `type.h` if the C header lags
> (notably `HNSW_RABITQ = 4` is defined in `type.h` but currently missing from `c_api.h`).
>
> **Public docs subset:** [zvec.org Data Types](https://zvec.org/en/docs/db/concepts/data-modeling/) /
> [llms-full.txt](https://zvec.org/llms-full.txt) (local audit snapshot: [`docs/llms-full.txt`](docs/llms-full.txt)) document the product-facing types
> (`STRING`…`DOUBLE`, `ARRAY_*`, `VECTOR_FP16`/`FP32`/`INT8`, `SPARSE_VECTOR_*`).
> The full ABI enum below is a **superset** (includes `BINARY`, `VECTOR_FP64`, `VECTOR_INT4`/`INT16`,
> `ARRAY_BINARY`, etc.) for P/Invoke fidelity — nothing from the public docs set was removed.

### ZVecDataType (from `DataType` / `ZVEC_DATA_TYPE_*`)

| Value | Name | C# Member | In public docs? |
|-------|------|-----------|-----------------|
| 0 | `UNDEFINED` | `ZVecDataType.Undefined` | — |
| 1 | `BINARY` | `ZVecDataType.Binary` | ABI-only |
| 2 | `STRING` | `ZVecDataType.String` | yes |
| 3 | `BOOL` | `ZVecDataType.Bool` | yes |
| 4 | `INT32` | `ZVecDataType.Int32` | yes |
| 5 | `INT64` | `ZVecDataType.Int64` | yes |
| 6 | `UINT32` | `ZVecDataType.UInt32` | yes |
| 7 | `UINT64` | `ZVecDataType.UInt64` | yes |
| 8 | `FLOAT` | `ZVecDataType.Float` | yes |
| 9 | `DOUBLE` | `ZVecDataType.Double` | yes |
| 20 | `VECTOR_BINARY32` | `ZVecDataType.VectorBinary32` | ABI-only |
| 21 | `VECTOR_BINARY64` | `ZVecDataType.VectorBinary64` | ABI-only |
| 22 | `VECTOR_FP16` | `ZVecDataType.VectorFp16` | yes |
| 23 | `VECTOR_FP32` | `ZVecDataType.VectorFp32` | yes |
| 24 | `VECTOR_FP64` | `ZVecDataType.VectorFp64` | ABI-only |
| 25 | `VECTOR_INT4` | `ZVecDataType.VectorInt4` | ABI-only |
| 26 | `VECTOR_INT8` | `ZVecDataType.VectorInt8` | yes |
| 27 | `VECTOR_INT16` | `ZVecDataType.VectorInt16` | ABI-only |
| 30 | `SPARSE_VECTOR_FP16` | `ZVecDataType.SparseVectorFp16` | yes |
| 31 | `SPARSE_VECTOR_FP32` | `ZVecDataType.SparseVectorFp32` | yes |
| 40 | `ARRAY_BINARY` | `ZVecDataType.ArrayBinary` | ABI-only |
| 41 | `ARRAY_STRING` | `ZVecDataType.ArrayString` | yes |
| 42 | `ARRAY_BOOL` | `ZVecDataType.ArrayBool` | yes |
| 43 | `ARRAY_INT32` | `ZVecDataType.ArrayInt32` | yes |
| 44 | `ARRAY_INT64` | `ZVecDataType.ArrayInt64` | yes |
| 45 | `ARRAY_UINT32` | `ZVecDataType.ArrayUInt32` | yes |
| 46 | `ARRAY_UINT64` | `ZVecDataType.ArrayUInt64` | yes |
| 47 | `ARRAY_FLOAT` | `ZVecDataType.ArrayFloat` | yes |
| 48 | `ARRAY_DOUBLE` | `ZVecDataType.ArrayDouble` | yes |

### ZVecMetricType (from `MetricType` / `ZVEC_METRIC_TYPE_*`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `UNDEFINED` | `ZVecMetricType.Undefined` |
| 1 | `L2` | `ZVecMetricType.L2` |
| 2 | `IP` | `ZVecMetricType.Ip` |
| 3 | `COSINE` | `ZVecMetricType.Cosine` |
| 4 | `MIPSL2` | `ZVecMetricType.MipsL2` |

### ZVecIndexType (from `IndexType` in `type.h`)

| Value | Name | C# Member | Notes |
|-------|------|-----------|-------|
| 0 | `UNDEFINED` | `ZVecIndexType.Undefined` | |
| 1 | `HNSW` | `ZVecIndexType.Hnsw` | |
| 2 | `IVF` | `ZVecIndexType.Ivf` | First-class in v1 |
| 3 | `FLAT` | `ZVecIndexType.Flat` | |
| 4 | `HNSW_RABITQ` | `ZVecIndexType.HnswRabitq` | In `type.h`; missing `#define` in `c_api.h` — use `4`. x86_64/AVX2 only |
| 5 | `DISKANN` | `ZVecIndexType.DiskAnn` | |
| 6 | `VAMANA` | `ZVecIndexType.Vamana` | |
| 10 | `INVERT` | `ZVecIndexType.Invert` | |
| 11 | `FTS` | `ZVecIndexType.Fts` | |

### ZVecQuantizeType (from `QuantizeType` in `type.h`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `UNDEFINED` | `ZVecQuantizeType.Undefined` |
| 1 | `FP16` | `ZVecQuantizeType.Fp16` |
| 2 | `INT8` | `ZVecQuantizeType.Int8` |
| 3 | `INT4` | `ZVecQuantizeType.Int4` |
| 4 | `RABITQ` | `ZVecQuantizeType.Rabitq` |

### ZVecErrorCode (from `zvec_error_code_t`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `ZVEC_OK` | `ZVecErrorCode.Ok` |
| 1 | `NOT_FOUND` | `ZVecErrorCode.NotFound` |
| 2 | `ALREADY_EXISTS` | `ZVecErrorCode.AlreadyExists` |
| 3 | `INVALID_ARGUMENT` | `ZVecErrorCode.InvalidArgument` |
| 4 | `PERMISSION_DENIED` | `ZVecErrorCode.PermissionDenied` |
| 5 | `FAILED_PRECONDITION` | `ZVecErrorCode.FailedPrecondition` |
| 6 | `RESOURCE_EXHAUSTED` | `ZVecErrorCode.ResourceExhausted` |
| 7 | `UNAVAILABLE` | `ZVecErrorCode.Unavailable` |
| 8 | `INTERNAL_ERROR` | `ZVecErrorCode.InternalError` |
| 9 | `NOT_SUPPORTED` | `ZVecErrorCode.NotSupported` |
| 10 | `UNKNOWN` | `ZVecErrorCode.Unknown` |

### ZVecLogLevel / ZVecLogType

Match `zvec_log_level_t` / `zvec_log_type_t` in `c_api.h`: `Debug=0` … `Fatal=4`; `Console=0` / `File=1`.

Match `ZVEC_QUANTIZE_TYPE_*` / `QuantizeType` in `type.h`: `Undefined=0`, `Fp16=1`, `Int8=2`, `Int4=3`, `Rabitq=4`.

### ZVecOperator (from `Operator` in `type.h`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `INSERT` | `ZVecOperator.Insert` |
| 1 | `UPSERT` | `ZVecOperator.Upsert` |
| 2 | `UPDATE` | `ZVecOperator.Update` |
| 3 | `DELETE` | `ZVecOperator.Delete` |

### ZVecCompareOp (from `CompareOp` in `type.h`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `NONE` | `ZVecCompareOp.None` |
| 1 | `EQ` | `ZVecCompareOp.Eq` |
| 2 | `NE` | `ZVecCompareOp.Ne` |
| 3 | `LT` | `ZVecCompareOp.Lt` |
| 4 | `LE` | `ZVecCompareOp.Le` |
| 5 | `GT` | `ZVecCompareOp.Gt` |
| 6 | `GE` | `ZVecCompareOp.Ge` |
| 7 | `LIKE` | `ZVecCompareOp.Like` |
| 8 | `CONTAIN_ALL` | `ZVecCompareOp.ContainAll` |
| 9 | `CONTAIN_ANY` | `ZVecCompareOp.ContainAny` |
| 10 | `NOT_CONTAIN_ALL` | `ZVecCompareOp.NotContainAll` |
| 11 | `NOT_CONTAIN_ANY` | `ZVecCompareOp.NotContainAny` |
| 12 | `IS_NULL` | `ZVecCompareOp.IsNull` |
| 13 | `IS_NOT_NULL` | `ZVecCompareOp.IsNotNull` |
| 14 | `HAS_PREFIX` | `ZVecCompareOp.HasPrefix` |
| 15 | `HAS_SUFFIX` | `ZVecCompareOp.HasSuffix` |

### ZVecRelationOp (from `RelationOp` in `type.h`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `NONE` | `ZVecRelationOp.None` |
| 1 | `AND` | `ZVecRelationOp.And` |
| 2 | `OR` | `ZVecRelationOp.Or` |

### ZVecColumnOp (from `ColumnOp` in `type.h`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `UNDEFINED` | `ZVecColumnOp.Undefined` |
| 1 | `ADD` | `ZVecColumnOp.Add` |
| 2 | `ALTER` | `ZVecColumnOp.Alter` |
| 3 | `DROP` | `ZVecColumnOp.Drop` |

### ZVecBlockType (from `BlockType` in `type.h`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `UNDEFINED` | `ZVecBlockType.Undefined` |
| 1 | `SCALAR` | `ZVecBlockType.Scalar` |
| 2 | `SCALAR_INDEX` | `ZVecBlockType.ScalarIndex` |
| 3 | `VECTOR_INDEX` | `ZVecBlockType.VectorIndex` |
| 4 | `VECTOR_INDEX_QUANTIZE` | `ZVecBlockType.VectorIndexQuantize` |
| 5 | `FTS_INDEX` | `ZVecBlockType.FtsIndex` |

### ZVecFileFormat (from `FileFormat` in `type.h`)

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `UNKNOWN` | `ZVecFileFormat.Unknown` |
| 1 | `IPC` | `ZVecFileFormat.Ipc` |
| 2 | `PARQUET` | `ZVecFileFormat.Parquet` |

---

## Appendix B — ZVec Method Signature Reference

Complete method signatures extracted from the official Python and Node.js SDK documentation, serving as the ground truth for C# wrapper implementation.

### B.1 Global Init

```python
zvec.init(log_type: LogType, log_level: LogLevel, query_threads: int)
```
```ts
ZVecInitialize({ logType: ZVecLogType, logLevel: ZVecLogLevel, queryThreads: number })
```

### B.2 Create and Open

```python
zvec.create_and_open(path: str, schema: CollectionSchema, option: CollectionOption = None) -> Collection
```
```ts
ZVecCreateAndOpen(path: string, schema: ZVecCollectionSchema, options?: ZVecCollectionOptions): ZVecCollection
```

### B.3 Open

```python
zvec.open(path: str, option: CollectionOption = None) -> Collection
```
```ts
ZVecOpen(path: string, options?: ZVecCollectionOptions): ZVecCollection
```

### B.4 Insert

```python
collection.insert(doc: Doc | list[Doc]) -> Status | list[Status]
```
```ts
collection.insertSync(doc: ZVecDocInput | ZVecDocInput[]): ZVecStatus | ZVecStatus[]
```

### B.5 Upsert

```python
collection.upsert(doc: Doc | list[Doc]) -> Status | list[Status]
```
```ts
collection.upsertSync(doc: ZVecDocInput | ZVecDocInput[]): ZVecStatus | ZVecStatus[]
```

### B.6 Update

```python
collection.update(doc: Doc | list[Doc]) -> Status | list[Status]
```
```ts
collection.updateSync(doc: ZVecDocInput | ZVecDocInput[]): ZVecStatus | ZVecStatus[]
```

### B.7 Delete

```python
collection.delete(ids: str | list[str]) -> Status | list[Status]
collection.delete_by_filter(filter: str) -> Status
```
```ts
collection.deleteSync(ids: string | string[]): ZVecStatus | ZVecStatus[]
collection.deleteByFilterSync(filter: string): ZVecStatus
```

### B.8 Fetch

```python
collection.fetch(ids: str | list[str]) -> dict[str, Doc]
```
```ts
collection.fetchSync(ids: string | string[]): Record<string, ZVecDoc>
```

### B.9 Query

```python
collection.query(
    queries: Query | list[Query],
    topk: int = 10,
    filter: str | None = None,
    reranker: ReRanker | None = None,
) -> list[Doc]
```
```ts
collection.querySync(params: {
    fieldName: string,
    vector?: number[],
    fts?: { matchString?: string, queryString?: string, defaultOperator?: string },
    topk?: number,
    filter?: string
}): ZVecDoc[]
```

### B.10 Optimize

```python
collection.optimize() -> None
```
```ts
collection.optimizeSync(): void
collection.optimize(): Promise<void>
```

### B.11 Destroy

```python
collection.destroy() -> None
```
```ts
collection.destroySync(): void
```

### B.12 Schema DDL

```python
collection.add_column(field_schema: FieldSchema, expression: str) -> None
collection.drop_column(field_name: str) -> None
collection.alter_column(old_name: str = None, new_name: str = None, field_schema: FieldSchema = None) -> None
collection.create_index(field_name: str, index_param: IndexParam) -> None
collection.drop_index(field_name: str) -> None
```

### B.13 Collection Properties

```python
collection.schema -> CollectionSchema
collection.stats -> { doc_count: int, index_completeness: dict[str, float] }
collection.option -> { enable_mmap: int, read_only: int }
collection.path -> str
```

### B.14 Query Object

```python
Query(
    field_name: str,
    vector: list[float] | dict[int, float] | None = None,
    id: str | None = None,
    fts: Fts | None = None,
    params: dict | None = None,
)
Fts(
    match_string: str | None = None,
    query_string: str | None = None,
    default_operator: str | None = None,
)
```

### B.15 Reranker Objects

```python
WeightedReRanker(topn: int, metric: MetricType, weights: dict[str, float])
RrfReRanker(topn: int, rank_constant: float = 60.0)
```

---

*End of ZVec.NET Project Plan*
