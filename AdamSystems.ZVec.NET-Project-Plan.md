# ZVec.NET SDK — Production-Grade Project Plan

> **ZVec** is Alibaba's open-source, in-process vector database — the "SQLite of Vector DBs" — built on the battle-tested Proxima search engine. Written in C++, it delivers sub-millisecond HNSW search, hybrid scalar+vector filtering, full-text search, WAL durability, and memory-mapped I/O with zero network overhead.
>
> **ZVec.NET** is a lightweight, high-performance .NET NuGet package that wraps ZVec's C++ core via a thin `extern "C"` bridge and exposes an idiomatic, async-friendly C# API with zero-allocation vector pipelines.

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
9. [Cross-Platform NuGet Packaging](#9-cross-platform-nuget-packaging)
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
| G1 | **Idiomatic C# API** | All public methods follow .NET naming guidelines; async pattern via `ValueTask`; `IAsyncEnumerable` for streaming |
| G2 | **Zero-allocation vector pipeline** | `ReadOnlySpan<float>` / `ReadOnlyMemory<float>` throughout; no `float[]` copies on hot paths |
| G3 | **Sub-millisecond overhead** | P/Invoke marshalling overhead < 50 µs on a 768-dim vector query (verified by BenchmarkDotNet) |
| G4 | **Cross-platform single NuGet** | One `.nupkg` with native binaries for win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64 |
| G5 | **Edge-ready / MAUI-compatible** | Memory-mapped I/O exposed; resource governance knobs exposed; no server dependency |
| G6 | **Full API surface coverage** | 100% of ZVec's Python SDK functionality wrapped (CRUD, query, FTS, hybrid search, schema evolution, optimize) |
| G7 | **SafeHandle guarantees** | Every native pointer wrapped in `SafeHandle`; zero native memory leaks even without `Dispose()` |
| G8 | **Comprehensive test & benchmark suite** | ≥90% line coverage; BenchmarkDotNet comparison vs. raw P/Invoke baseline |

### 1.3 Non-Goals (v1)

- Embedding generation (dense/sparse) — left to user or separate package
- MCP server integration
- Reranker implementations (client-side re-ranking only via `RrfReRanker` / `WeightedReRanker`)
- IVF index support (deferred to v1.1; HNSW + Flat + HNSW_RABITQ only)

### 1.4 Constraints & Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| .NET Target | `net8.0` – `net10.0` | User requirement; `net8.0` is LTS |
| C++ Standard | C++17 | User requirement; matches ZVec upstream |
| License | MIT | User decision |
| Test Framework | xUnit + FluentAssertions | User decision |
| Mock Strategy | Mock native layer + real bridge | Unit tests use a mock C-API DLL; integration tests use real ZVec binaries |
| NuGet Layout | Single `.nupkg` with `runtimes/` RID folders | User decision; MSBuild auto-selects correct native binary |
| P/Invoke Style | `[LibraryImport]` (source generator) | Compile-time marshalling code gen; faster than `[DllImport]` at runtime |
| Async Pattern | `ValueTask<T>` + `SemaphoreSlim` | Avoid allocations on synchronous completions; throttle P/Invoke concurrency |

---

## 2. ZVec API Surface Catalog

The following is the complete catalog of ZVec's Python SDK API, extracted from the official documentation at https://zvec.org/llms-full.txt. Every item listed below **must** have a corresponding C# wrapper.

### 2.1 Top-Level Module Functions

| Python | Node.js | Description |
|--------|---------|-------------|
| `zvec.init(log_type, log_level, query_threads)` | `ZVecInitialize({ logType, logLevel, queryThreads })` | Global configuration; call once at startup |
| `zvec.create_and_open(path, schema, option?)` | `ZVecCreateAndOpen(path, schema, options?)` | Create a new collection and open it |
| `zvec.open(path, option?)` | `ZVecOpen(path, options?)` | Open an existing collection |

### 2.2 Enumerations

| Enum | Values |
|------|--------|
| `DataType` | `STRING`, `BOOL`, `INT32`, `INT64`, `UINT32`, `UINT64`, `FLOAT`, `DOUBLE`, `ARRAY_STRING`, `ARRAY_BOOL`, `ARRAY_INT32`, `ARRAY_INT64`, `ARRAY_UINT32`, `ARRAY_UINT64`, `ARRAY_FLOAT`, `ARRAY_DOUBLE`, `VECTOR_FP16`, `VECTOR_FP32`, `VECTOR_INT8`, `SPARSE_VECTOR_FP32`, `SPARSE_VECTOR_FP16` |
| `MetricType` | `L2`, `IP`, `COSINE` |
| `LogType` | `CONSOLE`, `FILE` |
| `LogLevel` | `DEBUG`, `INFO`, `WARN`, `ERROR`, `FATAL` |
| `IndexType` (implicit) | `HNSW`, `HNSW_RABITQ`, `IVF`, `FLAT`, `INVERT`, `FTS` |
| `QuantizeType` (implicit) | `UNDEFINED`, `INT8`, `FP16` |

### 2.3 Schema Definition Types

| Type | Properties |
|------|-----------|
| `CollectionSchema` | `name: string`, `fields: list[FieldSchema]`, `vectors: list[VectorSchema]`, `max_doc_count_per_segment: int` |
| `FieldSchema` | `name: string`, `data_type: DataType`, `nullable: bool`, `index_param: InvertIndexParam?` |
| `VectorSchema` | `name: string`, `data_type: DataType`, `dimension: int`, `index_param: HnswIndexParam? or HnswRabitqIndexParam? or IvfIndexParam? or FtsIndexParam?` |
| `InvertIndexParam` | `enable_range_optimization: bool`, `enable_extended_wildcard: bool` |
| `HnswIndexParam` | `metric_type: MetricType`, `m: int`, `ef_construction: int`, `quantize_type: QuantizeType` |
| `HnswRabitqIndexParam` | `metric_type: MetricType`, `total_bits: int`, `num_clusters: int`, `m: int`, `ef_construction: int` |
| `IvfIndexParam` | `metric_type: MetricType`, `centroids_num: int`, `nlist: int`, `nprobe: int`, `quantize_type: QuantizeType` |
| `FtsIndexParam` | `tokenizer_name: string`, `filters: list[string]`, `extra_params: string` |
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

| Type | Properties |
|------|-----------|
| `Query` | `field_name: string`, `vector?: float[] or dict[int,float]`, `id?: string`, `fts?: Fts`, `params?: dict` |
| `Fts` | `match_string?: string`, `query_string?: string`, `default_operator?: string` |

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
│                      Consumer Application                     │
│                  (.NET 8 / 9 / 10 / MAUI)                    │
└───────────────────────┬──────────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────────┐
│                    ZVec.NET Public SDK                        │
│                                                               │
│  ┌─────────────┐  ┌──────────────┐  ┌───────────────────┐   │
│  │ ZVecClient   │  │ ZVecCollection│  │ Query Builders    │   │
│  │ (top-level)  │  │ (CRUD+Query) │  │ (fluent filter)   │   │
│  └──────┬──────┘  └──────┬───────┘  └────────┬──────────┘   │
│         │                │                    │               │
│  ┌──────┴────────────────┴────────────────────┴──────────┐   │
│  │              DTO Layer (Schema, Doc, Status, etc.)      │   │
│  └──────────────────────────┬───────────────────────────┘   │
│                              │                               │
│  ┌──────────────────────────┴───────────────────────────┐   │
│  │          SafeHandle Layer (SafeZvecHandle, etc.)       │   │
│  └──────────────────────────┬───────────────────────────┘   │
│                              │                               │
│  ┌──────────────────────────┴───────────────────────────┐   │
│  │        P/Invoke Layer ([LibraryImport] source-gen)     │   │
│  │              NativeMethods.cs                          │   │
│  └──────────────────────────┬───────────────────────────┘   │
└─────────────────────────────┼───────────────────────────────┘
                              │  P/Invoke (flat C ABI)
                              ▼
┌──────────────────────────────────────────────────────────────┐
│              zvec_c_api (C++ extern "C" Bridge)               │
│                                                               │
│  zvec_init()  zvec_create_collection()  zvec_open()          │
│  zvec_insert() zvec_query() zvec_fetch() zvec_delete()       │
│  zvec_optimize() zvec_destroy() zvec_add_column() ...        │
└──────────────────────────┬───────────────────────────────────┘
                           │  C++ function calls
                           ▼
┌──────────────────────────────────────────────────────────────┐
│                   ZVec C++ Core (Proxima Engine)              │
│               (libzvec.so / zvec.dll / libzvec.dylib)        │
└──────────────────────────────────────────────────────────────┘
```

### 3.1 Layer Responsibilities

| Layer | Responsibility | Key Design Principle |
|-------|---------------|---------------------|
| **Public SDK** | Idiomatic C# API; hides all unsafe code | Dependency Inversion — consumers never see `IntPtr` |
| **DTO Layer** | Strongly-typed data transfer objects | Immutable records; `readonly struct` for vectors |
| **SafeHandle Layer** | Wraps native pointers; guarantees cleanup | Critical finalizer; prevents GC-induced native leaks |
| **P/Invoke Layer** | `[LibraryImport]` declarations; zero-copy spans | `ReadOnlySpan<float>` marshalling; no `float[]` in signatures |
| **C-API Bridge** | Upstream official `zvec_c_api` (Alibaba C bindings) | C-linkage; no exceptions across boundary; `zvec_error_code_t` + last-error APIs |
| **ZVec Core** | Upstream C++ library (git submodule) | Consumed via CMake `add_subdirectory`; not forked for API surface |

---

## 4. Detailed Design

### 4.1 Project Structure

```
ZVec.NET/
├── src/
│   ├── Native/
│   │   └── ZVec.Native/                 # CMake wrapper → upstream zvec_c_api
│   │       ├── CMakeLists.txt           # Forces BUILD_C_BINDINGS=ON; add_subdirectory(external/zvec)
│   │       ├── steps.md                 # Windows operator build guide
│   │       └── external/zvec/           # Git submodule (alibaba/zvec)
│   │           ├── src/include/zvec/c_api.h   # Official C API (P/Invoke source of truth)
│   │           └── src/binding/c/             # Builds fat zvec_c_api shared library
│   │
│   ├── ZVec.Core/                      # .NET class library
│   │   ├── ZVec.Core.csproj           # net8.0; net9.0; net10.0
│   │   ├── Interop/
│   │   │   ├── NativeMethods.cs        # [LibraryImport] declarations
│   │   │   ├── SafeZvecHandle.cs       # SafeHandle for collection
│   │   │   ├── SafeZvecQueryHandle.cs  # SafeHandle for query results
│   │   │   └── SafeZvecSchemaHandle.cs # SafeHandle for schema
│   │   ├── Models/
│   │   │   ├── ZVecDoc.cs             # Document DTO
│   │   │   ├── ZVecStatus.cs          # Operation result
│   │   │   ├── ZVecCollectionSchema.cs # Schema definition
│   │   │   ├── ZVecFieldSchema.cs
│   │   │   ├── ZVecVectorSchema.cs
│   │   │   ├── ZVecQueryResult.cs
│   │   │   └── Enums/
│   │   │       ├── ZVecDataType.cs
│   │   │       ├── ZVecMetricType.cs
│   │   │       ├── ZVecLogLevel.cs
│   │   │       ├── ZVecLogType.cs
│   │   │       └── ZVecQuantizeType.cs
│   │   ├── IndexParams/
│   │   │   ├── ZVecHnswIndexParam.cs
│   │   │   ├── ZVecHnswRabitqIndexParam.cs
│   │   │   ├── ZVecIvfIndexParam.cs
│   │   │   ├── ZVecFtsIndexParam.cs
│   │   │   ├── ZVecInvertIndexParam.cs
│   │   │   └── ZVecFlatIndexParam.cs
│   │   ├── Query/
│   │   │   ├── ZVecQuery.cs           # Single query specification
│   │   │   ├── ZVecFtsQuery.cs        # Full-text search spec
│   │   │   ├── ZVecFilterBuilder.cs   # Fluent filter builder
│   │   │   └── ZVecReranker.cs        # Reranker abstractions
│   │   ├── ZVecClient.cs             # Top-level: init, create_and_open, open
│   │   └── ZVecCollection.cs         # CRUD, query, optimize, schema DDL
│   │
│   └── ZVec.Native.Mock/              # Mock native library for testing
│       ├── CMakeLists.txt
│       └── src/
│           └── zvec_c_api_mock.cpp     # In-memory mock matching upstream C API surface
│
├── tests/
│   ├── ZVec.Core.Tests/               # xUnit + FluentAssertions
│   │   ├── ZVec.Core.Tests.csproj
│   │   ├── Unit/
│   │   │   ├── SchemaTests.cs
│   │   │   ├── DocSerializationTests.cs
│   │   │   ├── FilterBuilderTests.cs
│   │   │   ├── SafeHandleTests.cs
│   │   │   └── EnumMappingTests.cs
│   │   ├── Integration/
│   │   │   ├── CollectionCrudTests.cs
│   │   │   ├── QueryTests.cs
│   │   │   ├── HybridSearchTests.cs
│   │   │   ├── FtsTests.cs
│   │   │   ├── SchemaEvolutionTests.cs
│   │   │   └── OptimizeTests.cs
│   │   └── Memory/
│   │       ├── ZeroAllocVectorTests.cs
│   │       └── SafeHandleLeakTests.cs
│   │
│   └── ZVec.Core.Benchmarks/          # BenchmarkDotNet
│       ├── ZVec.Core.Benchmarks.csproj
│       ├── VectorMarshallingBench.cs
│       ├── QueryThroughputBench.cs
│       ├── InsertThroughputBench.cs
│       └── MemoryDiagnosisBench.cs
│
├── build/
│   ├── ZVec.NET.sln
│   ├── Directory.Build.props
│   ├── Directory.Packages.props
│   └── ci/
│       ├── build-native.yml           # GitHub Actions: C++ matrix build
│       ├── build-managed.yml          # GitHub Actions: .NET build + test
│       └── publish-nuget.yml          # GitHub Actions: pack + push
│
├── Directory.Packages.props           # Central package management
└── README.md
```

### 4.2 Multi-Targeting Strategy

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <RootNamespace>ZVec</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
</Project>
```

---

## 5. Native C-API Bridge Specification

> **Superseded for implementation:** ZVec.NET consumes Alibaba’s official C API at
> `src/Native/ZVec.Native/external/zvec/src/include/zvec/c_api.h` (built by
> `src/binding/c` as `zvec_c_api`). Do **not** invent a parallel custom
> `include/zvec_c_api.h` / stub `.cpp` in the wrapper. The hand-written sketches
> below (§5.2+) remain historical design notes; align P/Invoke with upstream
> signatures (`zvec_initialize`, `zvec_error_code_t`, etc.) when implementing
> `ZVec.Core`.

### 5.1 Design Principles

1. **Flat C linkage only** — no C++ name mangling, no exceptions across boundary
2. **Error code return pattern** — upstream returns `zvec_error_code_t`; details via `zvec_get_last_error` / `zvec_get_last_error_details` (managed layer maps to exceptions / status DTOs)
3. **Opaque handles** — C++ objects exposed as opaque pointers to C#
4. **No heap allocations in the managed hot path** — native allocations stay in ZVec core; C# uses spans / pooling

### 5.2 C Header (`zvec_c_api.h`) — historical sketch (see callout above)

```c
#ifndef ZVEC_C_API_H
#define ZVEC_C_API_H

#include <stdint.h>
#include <stdbool.h>

#ifdef _WIN32
  #define ZVEC_API __declspec(dllexport)
#else
  #define ZVEC_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

/* ───── Opaque Handles ───── */
typedef void* ZvecCollectionHandle;
typedef void* ZvecSchemaHandle;

/* ───── Error Reporting ───── */
typedef struct {
    int32_t code;        // 0 = success
    char message[512];   // Human-readable error
} ZvecStatus;

/* ───── Data Type Enum ───── */
typedef enum {
    ZVEC_DATA_TYPE_STRING = 1,
    ZVEC_DATA_TYPE_BOOL = 2,
    ZVEC_DATA_TYPE_INT32 = 3,
    ZVEC_DATA_TYPE_INT64 = 4,
    ZVEC_DATA_TYPE_UINT32 = 5,
    ZVEC_DATA_TYPE_UINT64 = 6,
    ZVEC_DATA_TYPE_FLOAT = 7,
    ZVEC_DATA_TYPE_DOUBLE = 8,
    ZVEC_DATA_TYPE_ARRAY_STRING = 31,
    ZVEC_DATA_TYPE_ARRAY_BOOL = 32,
    ZVEC_DATA_TYPE_ARRAY_INT32 = 33,
    ZVEC_DATA_TYPE_ARRAY_INT64 = 34,
    ZVEC_DATA_TYPE_ARRAY_UINT32 = 35,
    ZVEC_DATA_TYPE_ARRAY_UINT64 = 36,
    ZVEC_DATA_TYPE_ARRAY_FLOAT = 37,
    ZVEC_DATA_TYPE_ARRAY_DOUBLE = 38,
    ZVEC_DATA_TYPE_VECTOR_FP16 = 21,
    ZVEC_DATA_TYPE_VECTOR_FP32 = 23,
    ZVEC_DATA_TYPE_VECTOR_INT8 = 24,
    ZVEC_DATA_TYPE_SPARSE_VECTOR_FP32 = 25,
    ZVEC_DATA_TYPE_SPARSE_VECTOR_FP16 = 26,
} ZvecDataType;

/* ───── Metric Type ───── */
typedef enum {
    ZVEC_METRIC_L2 = 1,
    ZVEC_METRIC_IP = 2,
    ZVEC_METRIC_COSINE = 3,
} ZvecMetricType;

/* ───── Global Init ───── */
ZVEC_API ZvecStatus zvec_init(
    int32_t log_type,     // 0=CONSOLE, 1=FILE
    int32_t log_level,    // 0=DEBUG, 1=INFO, 2=WARN, 3=ERROR, 4=FATAL
    int32_t query_threads
);

/* ───── Schema Construction ───── */
ZVEC_API ZvecStatus zvec_schema_create(
    const char* name,
    ZvecSchemaHandle* out_handle
);
ZVEC_API ZvecStatus zvec_schema_add_field(
    ZvecSchemaHandle schema,
    const char* field_name,
    ZvecDataType data_type,
    bool nullable,
    bool enable_range_optimization,
    bool enable_extended_wildcard
);
ZVEC_API ZvecStatus zvec_schema_add_vector_field(
    ZvecSchemaHandle schema,
    const char* vector_name,
    ZvecDataType data_type,
    int32_t dimension,
    int32_t index_type,          // 0=HNSW, 1=HNSW_RABITQ, 2=IVF, 3=FLAT, 4=FTS
    ZvecMetricType metric_type,
    int32_t m,                   // HNSW param
    int32_t ef_construction,     // HNSW param
    int32_t quantize_type,       // 0=UNDEFINED, 1=INT8, 2=FP16
    int32_t total_bits,          // HNSW_RABITQ param
    int32_t num_clusters,        // HNSW_RABITQ param
    int32_t centroids_num,       // IVF param
    int32_t nlist,               // IVF param
    int32_t nprobe,              // IVF param
    const char* tokenizer_name,  // FTS param
    const char* filters,         // FTS param (comma-separated)
    const char* extra_params     // FTS param (JSON string)
);
ZVEC_API void zvec_schema_destroy(ZvecSchemaHandle schema);

/* ───── Collection Lifecycle ───── */
ZVEC_API ZvecStatus zvec_create_and_open(
    const char* path,
    ZvecSchemaHandle schema,
    bool read_only,
    bool enable_mmap,
    ZvecCollectionHandle* out_handle
);
ZVEC_API ZvecStatus zvec_open(
    const char* path,
    bool read_only,
    bool enable_mmap,
    ZvecCollectionHandle* out_handle
);
ZVEC_API void zvec_collection_destroy(ZvecCollectionHandle collection);

/* ───── Document CRUD ───── */
ZVEC_API ZvecStatus zvec_insert(
    ZvecCollectionHandle collection,
    const char* id,
    const float* dense_vectors,    // flat array: [v1_dim1, v1_dim2, ..., v2_dim1, ...]
    const int32_t* dense_vector_dims,  // array of dimensions per vector
    const char** dense_vector_names,   // array of vector name strings
    int32_t num_dense_vectors,
    const int32_t* sparse_indices,     // flat: [sv1_idx1, sv1_idx2, ..., sv2_idx1, ...]
    const float* sparse_values,        // flat: [sv1_val1, sv1_val2, ..., sv2_val1, ...]
    const int32_t* sparse_vector_lengths, // count of non-zeros per sparse vector
    const char** sparse_vector_names,
    int32_t num_sparse_vectors,
    const char** field_names,
    const uint8_t* field_values,      // serialized field values
    const int32_t* field_value_lengths,
    int32_t num_fields
);
ZVEC_API ZvecStatus zvec_upsert(/* same signature as insert */);
ZVEC_API ZvecStatus zvec_update(/* same signature, but only provided fields/vectors */);
ZVEC_API ZvecStatus zvec_delete_by_ids(
    ZvecCollectionHandle collection,
    const char** ids,
    int32_t count,
    ZvecStatus* out_statuses
);
ZVEC_API ZvecStatus zvec_delete_by_filter(
    ZvecCollectionHandle collection,
    const char* filter
);

/* ───── Fetch ───── */
typedef struct {
    char id[256];
    float score;
    // Field and vector data returned via separate buffers
} ZvecFetchResultEntry;

ZVEC_API ZvecStatus zvec_fetch(
    ZvecCollectionHandle collection,
    const char** ids,
    int32_t count,
    ZvecFetchResultEntry** out_entries,  // caller must free via zvec_fetch_result_free
    int32_t* out_count
);
ZVEC_API void zvec_fetch_result_free(ZvecFetchResultEntry* entries);

/* ───── Query ───── */
typedef struct {
    char id[256];
    float score;
} ZvecQueryResultEntry;

ZVEC_API ZvecStatus zvec_query(
    ZvecCollectionHandle collection,
    // Single or multi-query
    const char** query_field_names,
    const float** query_vectors,        // one per query
    const int32_t* query_vector_dims,
    const int32_t* sparse_query_indices,  // for sparse vectors
    const float* sparse_query_values,
    int32_t num_queries,
    int32_t topk,
    const char* filter,                 // nullable
    // Reranker params
    int32_t reranker_type,              // 0=none, 1=Weighted, 2=RRF
    int32_t topn,                       // reranker topn
    ZvecMetricType reranker_metric,
    const char** reranker_weight_names,
    const float* reranker_weight_values,
    int32_t reranker_weight_count,
    float rrf_rank_constant,
    // FTS params
    const char* fts_match_string,       // nullable
    const char* fts_query_string,       // nullable
    int32_t fts_default_operator,       // 0=OR, 1=AND
    // Output
    ZvecQueryResultEntry** out_results,
    int32_t* out_result_count
);
ZVEC_API void zvec_query_result_free(ZvecQueryResultEntry* results);

/* ───── Optimize ───── */
ZVEC_API ZvecStatus zvec_optimize(ZvecCollectionHandle collection);

/* ───── Destroy Collection ───── */
ZVEC_API ZvecStatus zvec_destroy_collection(ZvecCollectionHandle collection);

/* ───── Schema DDL ───── */
ZVEC_API ZvecStatus zvec_add_column(
    ZvecCollectionHandle collection,
    const char* field_name,
    ZvecDataType data_type,
    const char* default_expression
);
ZVEC_API ZvecStatus zvec_drop_column(
    ZvecCollectionHandle collection,
    const char* field_name
);
ZVEC_API ZvecStatus zvec_alter_column_rename(
    ZvecCollectionHandle collection,
    const char* old_name,
    const char* new_name
);
ZVEC_API ZvecStatus zvec_alter_column_type(
    ZvecCollectionHandle collection,
    const char* field_name,
    ZvecDataType new_data_type
);
ZVEC_API ZvecStatus zvec_create_index(
    ZvecCollectionHandle collection,
    const char* field_name,
    int32_t index_type,
    /* index-specific params as needed */
    ...
);
ZVEC_API ZvecStatus zvec_drop_index(
    ZvecCollectionHandle collection,
    const char* field_name
);

/* ───── Collection Info ───── */
ZVEC_API ZvecStatus zvec_get_stats(
    ZvecCollectionHandle collection,
    int64_t* out_doc_count,
    float* out_index_completeness,
    int32_t* out_vector_field_count
);
ZVEC_API ZvecStatus zvec_get_option(
    ZvecCollectionHandle collection,
    bool* out_read_only,
    bool* out_enable_mmap
);
ZVEC_API ZvecStatus zvec_get_path(
    ZvecCollectionHandle collection,
    char* out_path,
    int32_t path_buffer_size
);

#ifdef __cplusplus
}
#endif

#endif /* ZVEC_C_API_H */
```

### 5.3 Error Code Convention

| Code | Meaning |
|------|---------|
| 0 | Success |
| 1 | Invalid argument |
| 2 | Collection not found |
| 3 | Schema mismatch |
| 4 | Duplicate ID |
| 5 | Document not found |
| 6 | Index error |
| 7 | I/O error |
| 8 | Out of memory |
| 9 | Unknown error |

---

## 6. P/Invoke & Marshalling Layer

### 6.1 `[LibraryImport]` Declarations

```csharp
// NativeMethods.cs
internal static partial class NativeMethods
{
    private const string LibraryName = "zvec_c_api";

    [LibraryImport(LibraryName)]
    internal static partial ZvecStatusNative zvec_init(
        int logType, int logLevel, int queryThreads);

    [LibraryImport(LibraryName)]
    internal static partial ZvecStatusNative zvec_create_and_open(
        [MarshalAs(UnmanagedType.LPStr)] string path,
        IntPtr schemaHandle,
        [MarshalAs(UnmanagedType.Bool)] bool readOnly,
        [MarshalAs(UnmanagedType.Bool)] bool enableMmap,
        out IntPtr outHandle);

    [LibraryImport(LibraryName)]
    internal static partial ZvecStatusNative zvec_open(
        [MarshalAs(UnmanagedType.LPStr)] string path,
        [MarshalAs(UnmanagedType.Bool)] bool readOnly,
        [MarshalAs(UnmanagedType.Bool)] bool enableMmap,
        out IntPtr outHandle);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_collection_destroy(IntPtr collection);

    [LibraryImport(LibraryName)]
    internal static partial ZvecStatusNative zvec_insert(
        IntPtr collection,
        [MarshalAs(UnmanagedType.LPStr)] string id,
        ReadOnlySpan<float> denseVectors,        // ZERO-COPY: pinned memory
        ReadOnlySpan<int> denseVectorDims,
        string[] denseVectorNames,
        int numDenseVectors,
        // ... sparse + fields ...
        );

    [LibraryImport(LibraryName)]
    internal static partial ZvecStatusNative zvec_query(
        IntPtr collection,
        // ... query params using ReadOnlySpan<float> for vectors ...
        out IntPtr outResults,
        out int outResultCount);

    [LibraryImport(LibraryName)]
    internal static partial void zvec_query_result_free(IntPtr results);

    // ... etc.
}
```

### 6.2 SafeHandle Implementations

```csharp
// SafeZvecHandle.cs
internal sealed class SafeZvecHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeZvecHandle() : base(ownsHandle: true) { }

    public SafeZvecHandle(IntPtr handle) : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        if (!IsInvalid)
        {
            NativeMethods.zvec_collection_destroy(handle);
        }
        return true;
    }
}
```

### 6.3 Zero-Copy Vector Marshalling

```csharp
// VectorMarshaller.cs
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

    /// <summary>
    /// Converts a sparse vector (dict of int->float) to flat index/value arrays
    /// for C-API consumption. Uses ArrayPool to avoid heap allocation.
    /// </summary>
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

### 7.1 Top-Level API — `ZVecClient`

```csharp
namespace ZVec;

public static class ZVecClient
{
    /// <summary>
    /// Initialize global ZVec configuration. Call once at application startup,
    /// before any collection operations.
    /// </summary>
    public static void Initialize(
        ZVecLogType logType = ZVecLogType.Console,
        ZVecLogLevel logLevel = ZVecLogLevel.Warn,
        int queryThreads = -1);   // -1 = auto-detect

    /// <summary>
    /// Create a new collection at the specified path and open it for read/write.
    /// </summary>
    public static ZVecCollection CreateAndOpen(
        string path,
        ZVecCollectionSchema schema,
        ZVecCollectionOptions? options = null);

    /// <summary>
    /// Open an existing collection from disk.
    /// </summary>
    public static ZVecCollection Open(
        string path,
        ZVecCollectionOptions? options = null);
}
```

### 7.2 Collection API — `ZVecCollection`

```csharp
namespace ZVec;

public sealed class ZVecCollection : IDisposable
{
    // ───── Properties ─────
    public ZVecCollectionSchema Schema { get; }
    public ZVecCollectionStats Stats { get; }
    public ZVecCollectionOptions Options { get; }
    public string Path { get; }

    // ───── Insert ─────
    public ZVecStatus Insert(ZVecDoc doc);
    public IReadOnlyList<ZVecStatus> Insert(IReadOnlyList<ZVecDoc> docs);

    // ───── Upsert ─────
    public ZVecStatus Upsert(ZVecDoc doc);
    public IReadOnlyList<ZVecStatus> Upsert(IReadOnlyList<ZVecDoc> docs);

    // ───── Update ─────
    public ZVecStatus Update(ZVecDoc doc);
    public IReadOnlyList<ZVecStatus> Update(IReadOnlyList<ZVecDoc> docs);

    // ───── Delete ─────
    public ZVecStatus Delete(string id);
    public IReadOnlyList<ZVecStatus> Delete(IReadOnlyList<string> ids);
    public ZVecStatus DeleteByFilter(string filter);

    // ───── Fetch ─────
    public IReadOnlyDictionary<string, ZVecDoc> Fetch(string id);
    public IReadOnlyDictionary<string, ZVecDoc> Fetch(IReadOnlyList<string> ids);

    // ───── Query ─────
    public IReadOnlyList<ZVecDoc> Query(
        ZVecQuery query,
        int topk = 10,
        string? filter = null);

    public IReadOnlyList<ZVecDoc> Query(
        IReadOnlyList<ZVecQuery> queries,
        int topk = 10,
        string? filter = null,
        ZVecReranker? reranker = null);

    // ───── Optimize ─────
    public void Optimize();

    // ───── Destroy ─────
    public void Destroy();

    // ───── Schema DDL ─────
    public void AddColumn(ZVecFieldSchema fieldSchema, string defaultExpression);
    public void DropColumn(string fieldName);
    public void AlterColumnRename(string oldName, string newName);
    public void AlterColumnType(string fieldName, ZVecDataType newDataType);
    public void CreateIndex(string fieldName, ZVecIndexParam indexParam);
    public void DropIndex(string fieldName);

    // ───── IDisposable ─────
    public void Dispose();
}
```

### 7.3 Document DTO — `ZVecDoc`

```csharp
namespace ZVec;

public sealed class ZVecDoc
{
    public string Id { get; init; }
    public IReadOnlyDictionary<string, ReadOnlyMemory<float>> DenseVectors { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>> SparseVectors { get; init; }
    public IReadOnlyDictionary<string, object> Fields { get; init; }
    public float Score { get; init; }  // Populated on query results

    // Convenience factory
    public static ZVecDoc Create(
        string id,
        IReadOnlyDictionary<string, ReadOnlyMemory<float>>? denseVectors = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<int, float>>? sparseVectors = null,
        IReadOnlyDictionary<string, object>? fields = null);
}
```

### 7.4 Query Types

```csharp
namespace ZVec;

public sealed class ZVecQuery
{
    public string FieldName { get; init; }
    public ReadOnlyMemory<float>? Vector { get; init; }       // For dense vector search
    public IReadOnlyDictionary<int, float>? SparseVector { get; init; } // For sparse vector search
    public string? DocumentId { get; init; }                   // Search by document's own vector
    public ZVecFtsQuery? Fts { get; init; }                   // For full-text search
    public IReadOnlyDictionary<string, object>? Params { get; init; } // Index-specific params (ef_search, etc.)
}

public sealed class ZVecFtsQuery
{
    public string? MatchString { get; init; }
    public string? QueryString { get; init; }
    public ZVecFtsDefaultOperator DefaultOperator { get; init; } = ZVecFtsDefaultOperator.Or;
}

public enum ZVecFtsDefaultOperator { Or, And }
```

### 7.5 Filter Builder (Fluent API)

```csharp
namespace ZVec.Query;

public sealed class ZVecFilterBuilder
{
    public static ZVecFilterBuilder Create() => new();

    public ZVecFilterBuilder Where(string fieldName, string op, object value);
    public ZVecFilterBuilder And(ZVecFilterBuilder inner);
    public ZVecFilterBuilder Or(ZVecFilterBuilder inner);
    public ZVecFilterBuilder Not(ZVecFilterBuilder inner);
    public ZVecFilterBuilder In(string fieldName, params object[] values);
    public ZVecFilterBuilder Like(string fieldName, string pattern);
    public ZVecFilterBuilder ContainAny(string fieldName, params object[] values);
    public ZVecFilterBuilder ContainAll(string fieldName, params object[] values);

    public override string ToString(); // Generates the ZVec filter expression string
}

// Usage:
// var filter = ZVecFilterBuilder.Create()
//     .Where("publish_year", ">", 1936)
//     .And(ZVecFilterBuilder.Create().ContainAny("category", "fiction", "romance"));
// collection.Query(query, topk: 10, filter: filter.ToString());
```

### 7.6 Reranker Types

```csharp
namespace ZVec;

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
    public float RankConstant { get; init; } = 60.0f;
}
```

### 7.7 Schema Types

```csharp
namespace ZVec;

public sealed class ZVecCollectionSchema
{
    public string Name { get; init; }
    public IReadOnlyList<ZVecFieldSchema> Fields { get; init; }
    public IReadOnlyList<ZVecVectorSchema> Vectors { get; init; }
    public int MaxDocCountPerSegment { get; init; } = 10_000_000;
}

public sealed class ZVecFieldSchema
{
    public string Name { get; init; }
    public ZVecDataType DataType { get; init; }
    public bool Nullable { get; init; }
    public ZVecInvertIndexParam? IndexParam { get; init; }
}

public sealed class ZVecVectorSchema
{
    public string Name { get; init; }
    public ZVecDataType DataType { get; init; }
    public int Dimension { get; init; }
    public ZVecIndexParam? IndexParam { get; init; }
}
```

### 7.8 Index Parameter Types

```csharp
namespace ZVec;

public abstract class ZVecIndexParam { }

public sealed class ZVecHnswIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.Cosine;
    public int M { get; init; } = 16;
    public int EfConstruction { get; init; } = 200;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}

public sealed class ZVecHnswRabitqIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.Cosine;
    public int TotalBits { get; init; } = 7;
    public int NumClusters { get; init; } = 64;
    public int M { get; init; } = 16;
    public int EfConstruction { get; init; } = 200;
}

public sealed class ZVecIvfIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
    public int CentroidsNum { get; init; } = 256;
    public int Nlist { get; init; } = 16;
    public int Nprobe { get; init; } = 8;
    public ZVecQuantizeType QuantizeType { get; init; } = ZVecQuantizeType.Undefined;
}

public sealed class ZVecFtsIndexParam : ZVecIndexParam
{
    public string TokenizerName { get; init; } = "standard";
    public IReadOnlyList<string> Filters { get; init; } = ["lowercase"];
    public string? ExtraParams { get; init; }
}

public sealed class ZVecInvertIndexParam : ZVecIndexParam
{
    public bool EnableRangeOptimization { get; init; } = false;
    public bool EnableExtendedWildcard { get; init; } = false;
}

public sealed class ZVecFlatIndexParam : ZVecIndexParam
{
    public ZVecMetricType MetricType { get; init; } = ZVecMetricType.L2;
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
ZVecCollection.Query():
            var (ptr, pin) = VectorMarshaller.PinVector(query.Vector);
            try {
                NativeMethods.zvec_query(..., ptr, ...);  // Pointer passed directly to C++
            } finally {
                pin.Dispose();  // Unpin after P/Invoke returns
            }
```

**Key guarantees:**
- `ReadOnlyMemory<float>` is stored in `ZVecDoc` — no `float[]` copy
- On P/Invoke, `Memory.Pin()` creates a `MemoryHandle` that prevents GC relocation
- The `MemoryHandle` is disposed in a `finally` block — ensures the GC can move the array again after the native call returns
- For batch inserts, vectors are pinned once for the entire batch

### 8.2 SafeHandle Guarantee

Every native pointer (`ZvecCollectionHandle`, `ZvecSchemaHandle`, etc.) is wrapped in a `SafeHandle` subclass:

- If the user calls `Dispose()`, the native resource is freed immediately
- If the user forgets `Dispose()`, the `SafeHandle` finalizer frees it on GC — preventing native memory leaks
- `SafeHandle` is critical-finalized, so it runs even if the process is exiting

### 8.3 ArrayPool for Search Results

```csharp
internal static class ResultBufferPool
{
    // For search result IDs and scores
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

### 8.4 Concurrency Model

```
┌──────────────┐     ┌──────────────────┐
│ Thread 1     │     │ Thread 2         │
│ query()      │     │ insert()         │
└──────┬───────┘     └──────┬───────────┘
       │                     │
       ▼                     ▼
┌──────────────────────────────────────┐
│  SemaphoreSlim(maxConcurrent: env)   │  ← throttles P/Invoke concurrency
│  (prevents P/Invoke boundary         │
│   bottleneck under high QPS)         │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  P/Invoke → zvec_c_api              │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  ZVec C++ Core                       │
│  (concurrent reads, single-process   │
│   exclusive writes)                  │
└──────────────────────────────────────┘
```

- ZVec supports concurrent reads natively; the C# SDK allows parallel `Query`/`Fetch` calls
- Write operations (`Insert`, `Upsert`, `Update`, `Delete`) are serialized via `SemaphoreSlim(1,1)` on the collection level
- A configurable `maxConcurrentPInvokes` throttles total P/Invoke concurrency to prevent thread-pool starvation

### 8.5 Memory Governance for Edge

```csharp
public sealed class ZVecCollectionOptions
{
    public bool ReadOnly { get; init; } = false;
    public bool EnableMmap { get; init; } = true;

    /// <summary>
    /// Maximum memory (in MB) the collection may use.
    /// Maps to ZVec's memory_limit_mb configuration.
    /// Critical for MAUI/mobile scenarios to prevent OOM.
    /// </summary>
    public int? MemoryLimitMb { get; init; }
}
```

---

## 9. Cross-Platform NuGet Packaging

### 9.1 NuGet Structure

```
ZVec.NET.nupkg/
├── lib/
│   ├── net8.0/ZVec.Core.dll
│   ├── net9.0/ZVec.Core.dll
│   └── net10.0/ZVec.Core.dll
├── runtimes/
│   ├── win-x64/native/zvec_c_api.dll
│   ├── win-arm64/native/zvec_c_api.dll
│   ├── linux-x64/native/libzvec_c_api.so
│   ├── linux-arm64/native/libzvec_c_api.so
│   ├── osx-x64/native/libzvec_c_api.dylib
│   └── osx-arm64/native/libzvec_c_api.dylib
└── build/
    ├── net8.0/ZVec.Core.props    # MSBuild targets for native binary resolution
    ├── net9.0/ZVec.Core.props
    └── net10.0/ZVec.Core.props
```

### 9.2 MSBuild Props for Native Binary Resolution

```xml
<!-- build/ZVec.Core.props -->
<Project>
  <ItemGroup Condition="'$(TargetFramework)' == 'net8.0' Or
                         '$(TargetFramework)' == 'net9.0' Or
                         '$(TargetFramework)' == 'net10.0'">
    <None Include="$(MSBuildThisFileDirectory)../runtimes/**/native/*"
          Pack="true"
          PackagePath="runtimes/%(RecursiveDir)%(FileName)%(Extension)" />
  </ItemGroup>
</Project>
```

### 9.3 .csproj NuGet Packaging Configuration

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>
    <PackageId>ZVec.NET</PackageId>
    <Version>1.0.0-alpha.1</Version>
    <Authors>ZVec.NET Contributors</Authors>
    <Description>High-performance .NET SDK for ZVec vector database</Description>
    <License>MIT</License>
    <RepositoryUrl>https://github.com/zvec-ai/zvec-dotnet</RepositoryUrl>
  </PropertyGroup>

  <ItemGroup>
    <Content Include="runtimes\**\*" Pack="true" PackagePath="runtimes" />
  </ItemGroup>
</Project>
```

---

## 10. Testing Strategy

### 10.1 Test Categories

| Category | Scope | Native Library | Count (est.) |
|----------|-------|---------------|-------------|
| **Unit** | DTO serialization, filter building, enum mapping, SafeHandle lifecycle | Mock | ~60 |
| **Integration** | Full CRUD lifecycle, query accuracy, FTS, hybrid search, schema evolution | Real | ~40 |
| **Memory** | Zero-allocation verification, SafeHandle leak detection, ArrayPool recycling | Real + Mock | ~15 |
| **Concurrency** | Multi-threaded read/write, query under load | Real | ~10 |

### 10.2 Mock Native Library

The `ZVec.Native.Mock` project implements the **same upstream C-API surface** (`zvec/c_api.h`) with an in-memory engine. This allows unit tests to run without the real ZVec C++ library:

- Stores documents in `ConcurrentDictionary<string, MockDocument>`
- Implements brute-force vector search for query verification
- Supports filter expression evaluation (subset)
- Returns realistic error codes

**Switching between mock and real** is done at the P/Invoke layer via a `NativeLibraryResolver`:

```csharp
[TestFixture]
public abstract class ZVecTestBase
{
    [OneTimeSetUp]
    public void SetupNativeLibrary()
    {
        if (UseRealLibrary)
            NativeLibraryResolver.Register("zvec_c_api", "/path/to/real/libzvec_c_api.so");
        else
            NativeLibraryResolver.Register("zvec_c_api", "/path/to/mock/libzvec_c_api_mock.so");
    }

    protected virtual bool UseRealLibrary => false;
}
```

### 10.3 Test Examples

```csharp
// Unit: Filter builder
[Fact]
public void FilterBuilder_CompoundExpression_GeneratesCorrectString()
{
    var filter = ZVecFilterBuilder.Create()
        .Where("publish_year", ">", 1936)
        .And(ZVecFilterBuilder.Create().ContainAny("category", "fiction", "romance"));

    filter.ToString().Should().Be("publish_year > 1936 AND category CONTAIN_ANY [\"fiction\", \"romance\"]");
}

// Integration: CRUD lifecycle
[Fact]
public void Collection_InsertFetchDelete_Lifecycle()
{
    using var col = ZVecClient.CreateAndOpen(tempPath, schema);

    var doc = ZVecDoc.Create("doc1",
        denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
        {
            ["embedding"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
        },
        fields: new Dictionary<string, object> { ["title"] = "Test" });

    var insertResult = col.Insert(doc);
    insertResult.Code.Should().Be(0);

    var fetched = col.Fetch("doc1");
    fetched.Should().ContainKey("doc1");
    fetched["doc1"].Fields["title"].Should().Be("Test");

    var deleteResult = col.Delete("doc1");
    deleteResult.Code.Should().Be(0);
}

// Memory: Zero-allocation verification
[Fact]
public void Query_WithPinnedMemory_DoesNotAllocateFloatArray()
{
    // Use GC.GetAllocatedBytesForCurrentThread() before/after
    // to verify no heap allocation on the hot path
    var vector = new float[768];
    var memory = new ReadOnlyMemory<float>(vector);

    long before = GC.GetAllocatedBytesForCurrentThread();
    collection.Query(new ZVecQuery { FieldName = "embedding", Vector = memory }, topk: 10);
    long after = GC.GetAllocatedBytesForCurrentThread();

    (after - before).Should().BeLessThan(1024); // Allow small overhead, not 768*4 bytes
}
```

---

## 11. Benchmark Strategy

### 11.1 Benchmark Categories

| Benchmark | Measures | Comparison |
|-----------|----------|-----------|
| `VectorMarshallingBench` | Time to pass 768-dim vector through P/Invoke | vs. raw `DllImport` with `float[]` |
| `QueryThroughputBench` | QPS for single-vector search | vs. Python ZVec SDK |
| `InsertThroughputBench` | Docs/sec for batch insert | vs. Python ZVec SDK |
| `MemoryDiagnosisBench` | GC allocations per operation | Must be near-zero on query path |
| `FilterParsingBench` | Filter builder string generation | Must be < 1 µs |

### 11.2 BenchmarkDotNet Configuration

```csharp
[MemoryDiagnoser]
[RankColumn]
public class QueryThroughputBench
{
    private ZVecCollection _collection = null!;
    private ReadOnlyMemory<float> _queryVector;
    private float[] _queryArray;

    [GlobalSetup]
    public void Setup()
    {
        _collection = ZVecClient.Open("/bench_collection");
        _queryVector = new float[768];
        _queryArray = new float[768];
        // ... populate with random vectors ...
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<ZVecDoc> Query_ReadOnlyMemory()
    {
        return _collection.Query(
            new ZVecQuery { FieldName = "embedding", Vector = _queryVector },
            topk: 10);
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> Query_FloatArray_Copy()
    {
        // Deliberately using float[] to show the allocation cost
        return _collection.Query(
            new ZVecQuery { FieldName = "embedding", Vector = _queryArray },
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
| P/Invoke overhead (768-dim vector) | < 50 µs | `DllImport float[]` ≈ 30 µs |
| Single-vector query (10k docs, topk=10) | < 1 ms | Python ZVec ≈ 1.2 ms |
| Batch insert (1000 docs) | > 50k docs/sec | Python ZVec ≈ 40k docs/sec |
| GC allocation per query | < 1 KB | `float[]` copy ≈ 3 KB |
| Filter builder string generation | < 1 µs | String concatenation ≈ 0.5 µs |

---

## 12. Work Breakdown Structure (WBS)

### Phase 1: C-API Bridge (Native Layer) — ~2 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 1.1 | Submodule + CMake wrapper for upstream `zvec_c_api` (`src/Native/ZVec.Native`) | C++ Lead | — | 2d |
| 1.2 | Align P/Invoke surface with upstream `zvec/c_api.h` (types, enums, signatures) | C++ Lead | 1.1 | 1d |
| 1.3 | Wire global init + schema construction via upstream C API | C++ Lead | 1.2 | 2d |
| 1.4 | Wire collection lifecycle (create_and_open, open, destroy) | C++ Lead | 1.3 | 2d |
| 1.5 | Wire insert/upsert/update/delete via upstream C API | C++ Lead | 1.3 | 3d |
| 1.6 | Wire query/fetch via upstream C API | C++ Lead | 1.3 | 3d |
| 1.7 | Wire optimize + schema DDL via upstream C API | C++ Lead | 1.3 | 2d |
| 1.8 | Compile test libraries (Windows x64 .dll, Linux x64 .so) | C++ Lead | 1.5-1.7 | 1d |
| 1.9 | Cross-compile for macOS (x64 + ARM64) | C++ Lead | 1.8 | 1d |
| 1.10 | Implement mock C-API library matching upstream C surface | C++ Dev | 1.2 | 5d |

### Phase 2: P/Invoke Layer — ~1.5 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 2.1 | Create .NET 8/9/10 class library project | .NET Lead | — | 0.5d |
| 2.2 | Define C# enums matching ZVec C enums | .NET Lead | 1.2 | 0.5d |
| 2.3 | Implement `[LibraryImport]` declarations in `NativeMethods.cs` | .NET Lead | 2.1, 1.2 | 2d |
| 2.4 | Implement `SafeZvecHandle`, `SafeZvecSchemaHandle` | .NET Lead | 2.3 | 1d |
| 2.5 | Implement `VectorMarshaller` (pin, sparse serialization, ArrayPool) | .NET Lead | 2.3 | 2d |
| 2.6 | Implement `NativeLibraryResolver` for mock/real switching | .NET Lead | 2.3 | 0.5d |
| 2.7 | Unit tests for SafeHandle lifecycle + enum mapping | .NET Dev | 2.4, 2.2 | 2d |

### Phase 3: Public SDK — ~2.5 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 3.1 | Define DTO types (ZVecDoc, ZVecStatus, schemas, index params) | .NET Lead | 2.2 | 2d |
| 3.2 | Implement `ZVecClient` (init, create_and_open, open) | .NET Lead | 2.3, 3.1 | 2d |
| 3.3 | Implement `ZVecCollection` CRUD methods | .NET Lead | 3.2 | 3d |
| 3.4 | Implement `ZVecCollection.Query` (single + multi-vector) | .NET Lead | 3.3 | 3d |
| 3.5 | Implement `ZVecCollection.Optimize` + `Destroy` | .NET Lead | 3.2 | 0.5d |
| 3.6 | Implement schema DDL methods (add/drop/alter column, create/drop index) | .NET Lead | 3.2 | 2d |
| 3.7 | Implement `ZVecFilterBuilder` fluent API | .NET Lead | 3.1 | 1d |
| 3.8 | Implement reranker types (Weighted, RRF) | .NET Lead | 3.1 | 1d |
| 3.9 | Implement collection properties (schema, stats, options, path) | .NET Lead | 3.2 | 1d |
| 3.10 | Integration tests (CRUD, query, FTS, hybrid, schema evolution) | .NET Dev | 3.3-3.9 | 5d |
| 3.11 | Memory/concurrency tests | .NET Dev | 3.3-3.9 | 3d |

### Phase 4: CI/CD & Publishing — ~1.5 weeks

| ID | Task | Owner | Depends On | Est. |
|----|------|-------|-----------|------|
| 4.1 | Set up GitHub Actions: C++ matrix build (win/ubuntu/macOS) | DevOps | 1.8 | 2d |
| 4.2 | Set up GitHub Actions: .NET build + test (multi-TFM) | DevOps | 2.1 | 1d |
| 4.3 | Configure .csproj for NuGet pack with native binary inclusion | .NET Lead | 4.1, 4.2 | 1d |
| 4.4 | Write BenchmarkDotNet benchmarks | .NET Dev | 3.10 | 3d |
| 4.5 | Run benchmarks; validate performance targets | .NET Dev | 4.4 | 1d |
| 4.6 | Publish alpha NuGet to nuget.org | .NET Lead | 4.3 | 0.5d |
| 4.7 | Write README + getting-started guide | .NET Lead | 4.6 | 1d |

### Total Estimated Duration: ~7.5 weeks (2 developers)

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
        cmake_generator: "Ninja"   # prefer Ninja; local VS 2026 also OK
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
2. **Build Managed** — `dotnet build` for `net8.0;net9.0;net10.0`
3. **Test (Mock)** — Run unit tests against mock native library
4. **Test (Integration)** — Run integration tests against real ZVec binaries (Linux x64 only)
5. **Benchmark** — Run BenchmarkDotNet on Linux x64 runner
6. **Pack** — `dotnet pack` with native binaries from Stage 1
7. **Publish** — Push to nuget.org (on release tag only)

---

## 14. Risk Register

| # | Risk | Impact | Probability | Mitigation |
|---|------|--------|-------------|-----------|
| R1 | ZVec C++ ABI changes between versions | High | Medium | Pin to specific ZVec release; add version checks in C-API bridge |
| R2 | P/Invoke marshalling overhead exceeds 50µs target | High | Low | Use `[LibraryImport]` source-gen; benchmark early; fall back to unsafe pointers |
| R3 | Cross-compilation fails for ARM64 targets | Medium | Medium | Use QEMU emulation in CI; test on real ARM hardware if available |
| R4 | NuGet package size too large (>50MB) | Medium | Low | Strip debug symbols; use `RuntimeIdentifier`-specific packages as fallback |
| R5 | GC-induced pauses during high-QPS queries | High | Low | Pin vectors during P/Invoke; use `ArrayPool`; avoid finalizers on hot path |
| R6 | ZVec's filter expression syntax differs from documented | Medium | Medium | Validate against real ZVec binary in integration tests |
| R7 | .NET 10 preview API changes break multi-targeting | Medium | Medium | Use `#if NET10_0_OR_GREATER` guards; test against latest preview SDK |
| R8 | Memory leaks in SafeHandle under exception scenarios | High | Low | Dedicated leak-detection tests; use `dotnet-counters` in CI |

---

## 15. Timeline & Milestones

| Milestone | Date (Target) | Deliverable |
|-----------|--------------|-------------|
| **M1: C-API Bridge MVP** | Week 2 | Upstream `zvec/c_api.h` + compiled `zvec_c_api` `.dll`/`.so` for win-x64 + linux-x64; mock library |
| **M2: P/Invoke Layer** | Week 3.5 | `NativeMethods.cs` + SafeHandles + VectorMarshaller; all unit tests passing |
| **M3: Public SDK Alpha** | Week 6 | Full CRUD + Query + FTS + Schema DDL; integration tests passing; README |
| **M4: Cross-Platform Build** | Week 7 | CI pipeline builds all 6 RIDs; NuGet pack succeeds |
| **M5: Benchmark & Release** | Week 8 | BenchmarkDotNet results validate performance targets; alpha NuGet published |

---

## Appendix A — ZVec Enum Reference

### ZVecDataType

| Value | Name | C# Member |
|-------|------|-----------|
| 1 | `STRING` | `ZVecDataType.String` |
| 2 | `BOOL` | `ZVecDataType.Bool` |
| 3 | `INT32` | `ZVecDataType.Int32` |
| 4 | `INT64` | `ZVecDataType.Int64` |
| 5 | `UINT32` | `ZVecDataType.UInt32` |
| 6 | `UINT64` | `ZVecDataType.UInt64` |
| 7 | `FLOAT` | `ZVecDataType.Float` |
| 8 | `DOUBLE` | `ZVecDataType.Double` |
| 21 | `VECTOR_FP16` | `ZVecDataType.VectorFp16` |
| 23 | `VECTOR_FP32` | `ZVecDataType.VectorFp32` |
| 24 | `VECTOR_INT8` | `ZVecDataType.VectorInt8` |
| 25 | `SPARSE_VECTOR_FP32` | `ZVecDataType.SparseVectorFp32` |
| 26 | `SPARSE_VECTOR_FP16` | `ZVecDataType.SparseVectorFp16` |
| 31 | `ARRAY_STRING` | `ZVecDataType.ArrayString` |
| 32 | `ARRAY_BOOL` | `ZVecDataType.ArrayBool` |
| 33 | `ARRAY_INT32` | `ZVecDataType.ArrayInt32` |
| 34 | `ARRAY_INT64` | `ZVecDataType.ArrayInt64` |
| 35 | `ARRAY_UINT32` | `ZVecDataType.ArrayUInt32` |
| 36 | `ARRAY_UINT64` | `ZVecDataType.ArrayUInt64` |
| 37 | `ARRAY_FLOAT` | `ZVecDataType.ArrayFloat` |
| 38 | `ARRAY_DOUBLE` | `ZVecDataType.ArrayDouble` |

### ZVecMetricType

| Value | Name | C# Member |
|-------|------|-----------|
| 1 | `L2` | `ZVecMetricType.L2` |
| 2 | `IP` | `ZVecMetricType.Ip` |
| 3 | `COSINE` | `ZVecMetricType.Cosine` |

### ZVecIndexType

| Value | Name | C# Member |
|-------|------|-----------|
| 0 | `HNSW` | `ZVecIndexType.Hnsw` |
| 1 | `HNSW_RABITQ` | `ZVecIndexType.HnswRabitq` |
| 2 | `IVF` | `ZVecIndexType.Ivf` |
| 3 | `FLAT` | `ZVecIndexType.Flat` |
| 10 | `INVERT` | `ZVecIndexType.Invert` |
| 20 | `FTS` | `ZVecIndexType.Fts` |

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

*End of ZVec.NET SDK Project Plan*
