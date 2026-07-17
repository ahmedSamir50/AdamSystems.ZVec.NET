# 🚧 ZVec.NET

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512bd4.svg)](https://dotnet.microsoft.com/)

> **⚠️ UNDER CONSTRUCTION — PRE-ALPHA**  
> This project is in early active development. APIs are unstable. Not yet available on NuGet.  
> See [Project Status](ZVec.NET-Implementation-Plan.md) for what's implemented.

**The definitive .NET SDK for [Alibaba ZVec](https://github.com/alibaba/zvec)**

---

## Why ZVec.NET?

| Feature | What it means for you |
|---------|----------------------|
| **Pin-based vector pipelines** | `ReadOnlyMemory&lt;float&gt;` on hot paths — no intermediate `float[]` copies on the query pin path (measure with `MemoryDiagnosisBench`) |
| **Sync + Async APIs** | Lowest-latency sync path for batch jobs; async `ValueTask` APIs for ASP.NET Core (cooperative cancel; no thread-pool offload today) |
| **DI-first design** | `AddZVec()` / `AddZVecCollection<T>()` — works with ASP.NET Core, MAUI, Blazor Server out of the box |
| **Typed ODM** | Map POCOs with `ZVec.NET.Mapping` attributes — schema `From<T>()`, expression filters, typed CRUD/DDL without magic field strings |
| **Safe native lifecycle** | Collection handles owned by `SafeZvecHandle` (close-only); `Dispose` closes, `Destroy` deletes then closes; `Shutdown` disposes all tracked open collections before `zvec_shutdown` |
| **Cross-platform natives** | Single NuGet `ZVec.NET` with `runtimes/{rid}/native/` for Windows, Linux, macOS, **Android**, **iOS / Mac Catalyst** (CI-built; MAUI sample is the proof) |
| **Full ZVec DB coverage** | HNSW, Flat, IVF, HNSW-RaBitQ, DiskANN, Vamana, Invert, FTS indexes; hybrid search; schema evolution; in-DB RRF/Weighted rerankers |
| **Idiomatic C#** | .NET naming guidelines, `ValueTask`, `CancellationToken`, fluent builders |

---

## Architecture & Concurrency

ZVec.NET is built for thread-safe use against the native engine:

1. **Instance-based factory:** `ZVecFactory` tracks its own state and open collection handles. A process-wide `lock` ensures exactly-once native library initialization (`zvec_initialize`).
2. **Collection lifecycle:** Open collections own a `SafeZvecHandle`. `Dispose` closes (data preserved); `Destroy` deletes on-disk data then closes. `Destroy` after `Dispose` throws `ObjectDisposedException`. `ZVecFactory.Shutdown` disposes every tracked open collection before native shutdown. Short-lived query/doc native objects also use `SafeHandle` helpers.
3. **Optional throttles:** `MaxConcurrentNativeCalls` / `MaxConcurrentReads` use `SemaphoreSlim` when &gt; 0; `0` means unlimited. The canceled managed RW lock (E9) is not used — native ZVec is already thread-safe.
4. **Zero-copy pipelines:** Hot paths pin `ReadOnlyMemory<float>` and pass pointers to native code. Performance targets are below (not published latency claims until benchmarks stabilize).

---

## Quick Start

### Install

```bash
dotnet add package ZVec.NET
```

> **Requires .NET 8.0+ (LTS).** Version scheme: `1.0.0-alpha.1+zvec.0.5.1` (SDK SemVer + pinned native). Supported TFMs are `lib/net8.0`, `lib/net9.0`, `lib/net10.0` — **not** encoded in the version string. Pre-alpha: CI packs whatever RID natives are built; local tests Skip if the native for your RID is missing.

### Native RIDs (NuGet `runtimes/`)

| RID | Native file | Built by |
|-----|-------------|----------|
| `win-x64`, `win-arm64` | `zvec_c_api.dll` | GitHub Actions (Windows) |
| `linux-x64`, `linux-arm64` | `libzvec_c_api.so` | GitHub Actions (Ubuntu; arm64 cross + QEMU) |
| `osx-x64`, `osx-arm64` | `libzvec_c_api.dylib` | GitHub Actions (macOS) |
| `android-arm64`, `android-x64` | `libzvec_c_api.so` | Android NDK + CMake (CI / local) |
| `ios-arm64`, `iossimulator-arm64`, `maccatalyst-arm64` | `libzvec_c_api.dylib` | macOS + Xcode (CI) |

Package size grows with each RID (desktop natives are already large). There is **no** fixed 50 MB gate — see pack workflow / release notes for measured size. Blazor WebAssembly is out of scope (no native RID).

**Owner on nuget.org:** [AdamSystems](https://www.nuget.org/profiles/AdamSystems). **Source:** [ahmedSamir50/AdamSystems.ZVec.NET](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET). PackageId remains **`ZVec.NET`**.

### Samples (Epic E25 — .NET 10 only)

Live demos live under [`samples/`](samples/) (Console, ASP.NET Minimal API, **MAUI Blazor Hybrid** flagship for offline/edge RAG with LM Studio + Gemma 4). They are **not** part of the NuGet package and never gate packaging CI.

```bash
dotnet build samples/ZVec.NET.Samples.slnx
```

See [`samples/README.md`](samples/README.md) for prerequisites, MB-only datasets, and a manual smoke checklist.

### Two APIs

| API | When to use |
|-----|-------------|
| **Typed (recommended)** | `IZvecCollection<T>`, `ZVecCollectionSchemaBuilder.From<T>()`, `AddZVecCollection<T>`, expression filters (`p => p.Category == x`). Compile-time field safety via `ZVec.NET.Mapping`. |
| **Dynamic (escape hatch)** | `IZvecCollection`, `ZVecDoc`, string field names, `ZVecFilterBuilder.Where("…")`, `AddZVecCollection("key", …)`. Tooling, dynamic schemas, parity with Python/Node shapes. |

Typed is a thin façade over dynamic (`IZvecCollection<T>.Untyped`). Contributor guidance for contributors: [`CONTRIBUTING.md`](CONTRIBUTING.md).

**DDL note:** native `add_column` / typed `EnsureSchema` only add **nullable numeric** columns. Put string/array fields in the create-time schema.

### ASP.NET Core / Blazor Server (typed — recommended)

```csharp
// Program.cs
using ZVec.NET.DependencyInjection;
using ZVec.NET.Mapping;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVec(options =>
{
    options.LogLevel = ZVecLogLevel.Warn;
    options.QueryThreads = -1;
    options.MemoryLimitMb = 512;
});

builder.Services.AddZVecCollection<Product>(options =>
{
    options.Path = "/data/products";
    options.EnableMmap = true;
    // Schema defaults to ZVecCollectionSchemaBuilder.From<Product>()
});

var app = builder.Build();
```

```csharp
using ZVec.NET.Mapping;

public sealed class Product
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";

    [ZVecVector(768, Metric = ZVecMetricType.Cosine, M = 32, EfConstruction = 256)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}

// Inject anywhere
public class ProductService(IZvecCollection<Product> products)
{
    public async Task<IReadOnlyList<ZVecHit<Product>>> SearchAsync(
        ReadOnlyMemory<float> queryVector,
        string? category = null)
    {
        return await products.QueryAsync(
            p => p.Embedding,
            queryVector,
            topK: 10,
            filter: category is null ? null : p => p.Category == category);
    }
}
```

**Mapping rules:** `Product` is a plain document POCO — it does **not** implement `IZvecCollection`. Inject / hold `IZvecCollection<Product>` (the collection handle). Schema comes from `ZVecCollectionSchemaBuilder.From<Product>()` / `AddZVecCollection<Product>`.

| Member | Required? | Rule |
|--------|-----------|------|
| Identity | Yes (exactly one) | Convention: public `string Id` / `ID`, **or** `[ZVecId]` |
| Vector properties | **Yes** `[ZVecVector(dim, …)]` | Dimension / metric / index cannot be inferred from `ReadOnlyMemory<float>` alone |
| Scalar properties | Usually none | Mapped by property name + CLR type; optional `[ZVecField("storageName")]` / `Nullable` |
| Skip a property | `[ZVecIgnore]` | |
| Collection name | Optional `[ZVecCollection("name")]` | Defaults to the CLR type name |

### Console / Batch (typed, no DI)

```csharp
using ZVec.NET;
using ZVec.NET.Mapping;

using var factory = new ZVecFactory();
factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });

var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();
using var untyped = factory.CreateAndOpen("/tmp/products", schema);
using IZvecCollection<Product> products = new ZVecCollection<Product>(untyped);

products.Insert(new Product
{
    Id = "p1",
    Title = "Hello ZVec",
    Category = "demo",
    Embedding = new float[768]
});

var hits = products.Query(p => p.Embedding, queryVec, topK: 10, filter: p => p.Category == "demo");
foreach (var hit in hits)
    Console.WriteLine($"{hit.Record.Id} (score: {hit.Score:F4})");
```

### Typed filters

Expression filters on `IZvecCollection<T>` compile to native filter strings via `ZVecExpressionFilter` (same engine as `DeleteByFilter`). Property names use the mapped storage name (`[ZVecField]` overrides apply).

**Supported**

```csharp
// Equality / relational
products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Category == "demo");
products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Year > 2020);
products.Query(p => p.Embedding, vec, topK: 10, filter: p => 5 < p.Year); // inverted sides OK

// Compound + not + null
products.Query(p => p.Embedding, vec, topK: 10,
    filter: p => p.Category == "ai" && p.Year >= 2020);
products.Query(p => p.Embedding, vec, topK: 10,
    filter: p => p.Title != null || p.Year >= 2000);
products.Query(p => p.Embedding, vec, topK: 10,
    filter: p => !(p.Year < 2000) && (p.Category == "a" || p.Category == "b"));

// Delete by expression
products.DeleteByFilter(p => p.Category == "expired");
```

| Supported | Ops / shapes |
|-----------|----------------|
| Compare | `==` `!=` `<` `<=` `>` `>=` (constant on either side) |
| Boolean | `&&` `\|\|` `!`, nested parentheses |
| Null | `== null` / `!= null` → `IsNull` / `IsNotNull` |
| Values | string, bool, int/long/float/double (and similar numerics) |

**Unsupported** (throws `ZVecException`): method calls (`StartsWith`, `Contains`, …), indexers, invoking other methods, or anything that is not a comparison / boolean tree. For those, use the escape hatch:

```csharp
products.Untyped.Query(
    new ZVecQuery { FieldName = "Embedding", Vector = vec },
    topk: 10,
    filter: ZVecFilterBuilder.Create().Where("Category", ZVecCompareOp.Eq, "demo"));
```

### Advanced / dynamic (`ZVecDoc`)

String field names and `ZVecDoc` remain available for tooling and dynamic schemas:

```csharp
using var col = factory.CreateAndOpen("/tmp/docs", new ZVecCollectionSchemaBuilder("docs")
    .AddField("title", ZVecDataType.String)
    .AddVector("vec", ZVecDataType.VectorFp32, 768, new ZVecHnswIndexParam())
    .Build());

col.Insert(ZVecDoc.Create("doc1",
    denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["vec"] = queryVec },
    fields: new Dictionary<string, object> { ["title"] = "Hello" }));

var docs = col.Query(
    new ZVecQuery { FieldName = "vec", Vector = queryVec },
    topk: 10,
    filter: ZVecFilterBuilder.Create().Where("title", ZVecCompareOp.Eq, "Hello"));
```

---

## Architecture

```
┌────────────────────────────────────────────────────┐
│        Consumer (ASP.NET Core / MAUI / Console)     │
│     DI: AddZVec / AddZVecCollection<T> (typed ODM) │
└───────────────────────┬────────────────────────────┘
                        │
          ┌─────────────▼──────────────┐
          │   ZVec.NET SDK             │
          │  IZvecFactory / IZvecCollection[T] │
          │  Mapping (attrs, mapper, expr) │
          │  (+ Lifecycle/Writes/Queries/Ddl) │
          │  Builders / DTOs / DI        │
          ├──────────────────────────────┤
          │  Collection*Ops + CallGate   │
          │  SafeZvecHandle (collection) │
          │  SafeHandle helpers (query/doc)│
          │  [LibraryImport] P/Invoke    │
          └─────────────┬──────────────┘
                        │  Flat C ABI
          ┌─────────────▼──────────────┐
          │  zvec_c_api (C bindings)    │
          │  Alibaba's official C API   │
          └─────────────┬──────────────┘
                        │
          ┌─────────────▼──────────────┐
          │  ZVec C++ Core (Proxima)    │
          └────────────────────────────┘
```

---

## API Overview

### Index Types

| Index | Use Case | SDK Type | Platform notes |
|-------|----------|----------|----------------|
| **HNSW** | General-purpose ANN | `ZVecHnswIndexParam` | All supported RIDs |
| **Flat** | Exact search (small datasets) | `ZVecFlatIndexParam` | All supported RIDs |
| **IVF** | Clustered ANN | `ZVecIvfIndexParam` | All supported RIDs |
| **HNSW-RaBitQ** | Quantized HNSW | `ZVecHnswRabitqIndexParam` | **x86_64 with AVX2 or higher only** — not available on ARM. SDK throws `PlatformNotSupportedException` on Arm/Arm64. |
| **DiskANN** | Disk-based ANN | `ZVecDiskAnnIndexParam` | **Linux only**, requires **libaio**. SDK throws `PlatformNotSupportedException` on non-Linux. |
| **Vamana** | Graph-based ANN | `ZVecVamanaIndexParam` | All supported RIDs |
| **Invert** | Scalar field index | `ZVecInvertIndexParam` | All supported RIDs |
| **FTS** | Full-text search | `ZVecFtsIndexParam` | All supported RIDs |

### Query Modes

- **Single vector** — `col.Query(new ZVecQuery { FieldName = "vec", Vector = myVec }, topk: 10)`
- **Multi-vector** — `col.Query(queries, topk: 10, reranker: new ZVecRrfReranker { TopN = 10 })`
- **Hybrid** (dense + sparse) — multi-query with dense + sparse sub-queries and optional `filter`
- **Full-text** — `new ZVecQuery { FieldName = "content", Fts = new ZVecFtsQuery { QueryString = "search terms" } }`
- **Group-by** — `col.QueryGroupBy(new ZVecGroupByQuery { Query = q, GroupByField = "category", GroupSize = 5 })`
- **Filtered (typed)** — `products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Year > 2020)` — see [Typed filters](#typed-filters)
- **Filtered (dynamic)** — `col.Query(query, topk: 10, filter: ZVecFilterBuilder.Create().Where("year", ZVecCompareOp.Gt, 2020))`

### CRUD (typed)

```csharp
products.Insert(product);
products.Upsert(product);
products.Update(product);
products.Delete("p1");
products.DeleteByFilter(p => p.Category == "expired");
Product? single = products.Fetch("p1");
```

### Schema Evolution (DDL)

```csharp
// Typed — EnsureSchema adds missing scalar columns only (never drops).
// Native add_column supports nullable numeric types; string columns belong in create schema.
await products.EnsureSchemaAsync();
await products.DropColumnAsync(p => p.Year); // explicit destructive
await products.CreateIndexAsync(p => p.Year, new ZVecInvertIndexParam());

// Dynamic escape hatch
col.AddColumn(new ZVecFieldSchema { Name = "rating", DataType = ZVecDataType.Float, Nullable = true }, "0.0");
col.DropColumn("legacy_field");
col.Optimize();
```

### Filter Builder

Immutable AST builder — one `Create()`, nest with lambdas, call `Build()` (or pass the builder to `Query` overloads):

```csharp
var filter = ZVecFilterBuilder.Create()
    .Where("publish_year", ZVecCompareOp.Gt, 2020)
    .And(f => f
        .Where("category", ZVecCompareOp.Eq, "fiction")
        .Or(g => g.ContainAny("tags", "AI", "ML")))
    .Build();

// -> publish_year > 2020 AND (category = "fiction" OR tags CONTAIN_ANY ("AI", "ML"))
// Native list syntax uses parentheses, not square brackets.
```

### Sync + Async

Every mutating/querying operation exposes both sync and async variants. Async methods are **cancellation-aware wrappers** around synchronous native P/Invoke (they complete on the caller thread — not a thread-pool offload).

```csharp
// Sync (lowest latency — P/Invoke on caller thread)
var results = col.Query(query, topk: 10);

// Async (ValueTask; completes synchronously after the native call)
var results = await col.QueryAsync(query, topk: 10, cancellationToken: ct);
```

Pass `includeVector: false` when you do not need result embeddings (lower latency and GC alloc). Default remains `true` for backward compatibility. Queries that set `ZVecQuery.DocumentId` perform an extra `Fetch` (with vectors) before search.

---

## Performance

Workload for primary numbers: **768-dim Flat**, `win-x64`, native ZVec `v0.5.1-35-g1afdea8`, .NET 8.0.29, Intel Core i7-8850H, BenchmarkDotNet **medium** job (2026-07-17, after hot-path recovery). Query/memory benches seed **10_000** docs; insert batch size **1000**.

**Primary latency / alloc benches use `includeVector: false`** (search + id/score/scalars). That matches Python’s `Collection.query(..., include_vector=False)` default. Full materialization (`includeVector: true`) is reported separately — it copies ~10 × 768 floats (~30 KB) and dominates GC alloc.

**Why older smoke numbers looked different:** legacy `ZVecPerformanceBenchmarks` used **128-dim** / tiny corpus. Do not compare those ~ns figures to this suite.

### Upstream engine scale (VectorDBBench — published)

Official ZVec [Benchmarks](https://zvec.org/en/docs/db/benchmarks/) use VectorDBBench on Cohere **1M / 10M** (768-dim). Documented in code as `UpstreamEngineScaleBaseline` / `EngineScaleReferenceBench` (not re-run locally):

| Case | Scale | Published figure | Notes |
|------|-------|------------------|-------|
| `Performance768D1M` | Cohere **1M** × 768-d | See VectorDBBench run on docs page | Engine scale |
| `Performance768D10M` | Cohere **10M** × 768-d | Homepage **8500+ QPS**; index build **~1 hour** | Engine scale |

Not apples-to-apples with the 10k Flat SDK binding suite. Local single-threaded inverse of `Query_Sync` (~2.88 ms → ~350 QPS) is a different metric shape than concurrent VectorDBBench QPS at 10M.

### Binding suite: .NET vs Python vs Node.js (same machine, 10k Flat)

Same-host parity. **Python `zvec` 0.5.1 re-confirmed this session** (`include_vector=False` for fair compare). Node `@zvec/zvec` column from the earlier same-day run (package not present in this verify session). Scripts are ephemeral (not checked in).

| Metric | .NET (ours) | Python | Node.js |
|--------|-------------|--------|---------|
| Warm query (128 docs, topk=10, no result vectors) | **0.512 ms** (median 0.390 ms) | 0.414 ms | 1.594 ms |
| Query (10k docs, topk=10, no result vectors) | **2.88 ms** | 4.33 ms | 4.103 ms |
| Batch insert (1000 docs) | **~16.8k docs/sec** | ~7.1k docs/sec | ~5.8k docs/sec |
| GC alloc / query (no result vectors) | **6.8 KB** | n/a | n/a |
| GC alloc / query (with topk vectors) | **40.5 KB** | n/a | n/a |

### Targets vs measured (.NET)

Two tiers — Status is against the **medium** job after hot-path recovery.

**Tier A — binding / search (`includeVector: false`)**

| Metric | Target | Measured | Status |
|--------|--------|----------|--------|
| Warm query, 128 docs, topk=10 | &lt; 200 µs | 512 µs mean / 390 µs median (`Query_WarmTinyCorpus`) | Miss — stretch; still competitive with Python warm |
| Single-vector query, 10k Flat, topk=10 | &lt; 2.0 ms (stretch &lt; 1.5 ms); beat Python | **2.88 ms** (`Query_Sync`) vs Python **4.33 ms** | Miss stretch target; **Pass vs Python** |
| GC alloc / query (no vectors) | &lt; 4 KB | **6.8 KB** (`Query_768Dim` / `Query_Sync`) | Miss — much improved vs ~44 KB with vectors |

**Tier B — full materialization (`includeVector: true`)**

| Metric | Target | Measured | Status |
|--------|--------|----------|--------|
| 10k query with vectors | report / &lt; 4 ms | 2.57–3.16 ms (`Query_Sync_WithVectors` / `Query_768Dim_WithVectors`) | Pass report band |
| GC alloc / query with vectors | ~40–45 KB expected | **40.5–41.5 KB** | Expected floor (not a &lt;256 B target) |

**Tier C — insert**

| Metric | Target | Measured | Status |
|--------|--------|----------|--------|
| Batch insert 1000 docs | &gt; 20k docs/sec (stretch &gt; 30k) | **~16.8k docs/sec** (`Insert_Batch`, 59.4 ms) | Miss; ahead of this-session Python (~7.1k) |

**Allocation split:** query vector still uses `ReadOnlyMemory<float>` + `Memory.Pin()`. With `includeVector: false`, Allocated is mostly result doc/id/scalar unmarshall (~7 KB). With vectors on, ~40 KB is dominated by copying topk embeddings.

### Measured suite (primary, .NET BDN medium)

| Method | Mean | Allocated |
|--------|------|----------:|
| `Query_Sync` (10k, no vectors) | 2.88 ms | 6.8 KB |
| `Query_Sync_WithVectors` (10k) | 2.57 ms | 40.5 KB |
| `Query_WithFilter` (10k, no vectors) | 1.43 ms | 1.7 KB |
| `Query_WarmTinyCorpus` (128, no vectors) | 512 µs | 6.7 KB |
| `Query_768Dim` (no vectors) | 2.82 ms | 6.8 KB |
| `Query_768Dim_WithVectors` | 3.16 ms | 40.5 KB |
| `Query_ReadOnlyMemory` (pin, no result vectors) | 4.29 ms | 6.9 KB |
| `Query_ExplicitCopy` (no result vectors) | 2.61 ms | 9.9 KB |
| `Fetch_ScalarOnly` | 193 µs | 904 B |
| `Insert_Single` | 53.1 µs | 1.4 KB |
| `Insert_Batch` (1000 docs) | 59.4 ms | 446 KB |
| `Build_SimpleFilter` | 67 ns | 128 B |
| `Build_CompoundFilter` | 198 ns | 544 B |
| `Local_10k_Query_ForEngineScaleContext` | 3.52 ms | 6.9 KB |

### Typed ODM overhead (vs dynamic `ZVecDoc`)

Measured with `TypedOdmOverheadBench` (Release, .NET 9, short job: WarmupCount=1, IterationCount=5, 128-doc Flat corpus, 768-dim). **Primary latency suite above stays `ZVecDoc`-only.**

| Method | Mean | Allocated | Notes |
|--------|------|-----------|--------|
| `Insert_Dynamic` (baseline) | 66.6 µs | 1.4 KB | |
| `Insert_Typed` | 55.9 µs | 2.1 KB | Wall time ≈ dynamic (noise); **~1.6× alloc** from mapper |
| `Query_Dynamic` | 442 µs | 6.5 KB | |
| `Query_Typed` | 381 µs | 8.8 KB | Wall time ≈ dynamic; **higher alloc** (`ZVecHit<T>` + map) |
| `QueryFilter_Dynamic` | 569 µs | 6.5 KB | |
| `QueryFilter_Typed` | 540 µs | 9.5 KB | Expression translate + map; still native-dominated |
| `Mapper_ToDoc` | 414 ns | 1.0 KB | Managed-only |
| `Mapper_FromDoc` | 194 ns | 160 B | Managed-only |
| `ExpressionFilter_Translate` | 372 ns | 592 B | Managed-only |

**Conclusion:** Typed façade does **not** add significant wall-clock cost vs magic-string/`ZVecDoc` on insert/query (native dominates). Expect **more managed allocations** per op. Micro-costs (mapper / expression) are sub-µs.

```bash
dotnet run -c Release --project testing/ZVec.NET.Benchmarks --filter *TypedOdmOverheadBench*
```

### How to reproduce (.NET)

Primary numbers in this README come from a **full-assembly** BenchmarkDotNet run with the **`medium`** job (not `short`). Use `short` only for a quick smoke check.

**Full suite (all classes in the assembly — preferred for README numbers):**

```bash
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *
```

**Job options** (BenchmarkDotNet): `short` (smoke), `medium` (README gate), `default` / `long` / `verylong` (more iterations). Invalid names like `MediumRun` / `ShortRun` are rejected; use lowercase `medium` / `short`.

**Filter examples** (run a subset when iterating):

```bash
# Query latency + with/without vectors
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *QueryThroughputBench*

# Memory / alloc diagnosis
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *MemoryDiagnosisBench*

# Insert throughput
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *InsertThroughputBench*

# Query-vector pin vs explicit copy
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *VectorMarshallingBench*

# Filter AST Build() only (no native)
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *FilterParsingBench*

# Local 10k Flat next to upstream VectorDBBench doc context
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *EngineScaleReferenceBench*

# Typed vs ZVecDoc overhead (managed façade cost)
dotnet run -c Release --project testing/ZVec.NET.Benchmarks --filter *TypedOdmOverheadBench*

# Legacy 128-dim smoke (not the primary README baseline)
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j short -f *ZVecPerformanceBenchmarks*
```

**Benchmark classes in `testing/ZVec.NET.Benchmarks`:**

| Class | Role | Notes |
|-------|------|--------|
| `QueryThroughputBench` | Primary query latency (10k + warm 128) | `Query_Sync` / `Query_WarmTinyCorpus` use `includeVector: false`; `Query_Sync_WithVectors` is the materialization path |
| `MemoryDiagnosisBench` | GC alloc per query / fetch | `Query_768Dim` vs `Query_768Dim_WithVectors`; `Fetch_ScalarOnly` |
| `InsertThroughputBench` | Single + batch insert | Batch size 1000; docs built in `IterationSetup` |
| `VectorMarshallingBench` | Query-vector pin vs `ToArray()` copy | Result vectors off so alloc delta is marshalling, not topk copies |
| `FilterParsingBench` | Managed filter `Build()` only | No native collection; `Build_SimpleFilter` / `Build_CompoundFilter` |
| `TypedOdmOverheadBench` | Typed vs dynamic insert/query + mapper micro | Does **not** replace primary `ZVecDoc` latency suite |
| `EngineScaleReferenceBench` | Local 10k Flat + prints upstream Cohere 1M/10M context | Does **not** re-run VectorDBBench |
| `ZVecPerformanceBenchmarks` | Legacy **128-dim** smoke insert/query | Not the primary README baseline; keep for quick local checks only |

Also in the project (not a BDN class): `UpstreamEngineScaleBaseline` / `BenchmarkEnvironment` constants for workload labels and published upstream figures.

---

## Versioning

| What | Format | Example |
|------|--------|---------|
| **SDK version** | SemVer | `1.0.0-alpha.1` |
| **ZVec native pin** | Build metadata after `+` | `+zvec.0.5.1` |
| **.NET target** | TFM + `lib/` folder | `net8.0` (LTS) |
| **ABI floor** | `ZVecNativeAbi` | Minimum `0.5.1`, same major |

NuGet version example: `1.0.0-alpha.1+zvec.0.5.1` — SDK `1.0.0-alpha.1` wrapping ZVec C++ `0.5.1`. Do **not** put TFM or branch names into the version string.

### Git vs NuGet (branch ≠ version)

| Concept | Example | Where it lives |
|---------|---------|----------------|
| Git branch (train) | `release/1.0` | Git — long-lived for all 1.0.x |
| Git tag (one ship) | `v1.0.0-alpha.1` | Git — **no** `+zvec…` in the tag |
| NuGet / csproj Version | `1.0.0-alpha.1+zvec.0.5.1` | `ZVec.NET.csproj` |

There is **no** branch named `release/1.0.0-alpha.1+zvec.0.5.1`. `+zvec.0.5.1` is build metadata only.

### Branch topology

```mermaid
flowchart LR
  feat[feature branches]
  dev[development]
  main[main]
  rel["release/1.0"]
  tag["tag v1.0.0-alpha.1"]

  feat --> dev
  dev -->|"PR merge"| main
  main -->|"cut release line"| rel
  rel -->|"tag for nuget.org"| tag
  rel -->|"hotfix commits"| rel
  rel -->|"merge backfixes"| main
  main -->|"merge"| dev
```

| Branch | Role |
|--------|------|
| **`development`** | Daily integration; open `feature/*` / `bugfix/*` PRs here |
| **`main`** | Stable trunk; parent of every `release/*` cut |
| **`release/1.0`** | 1.0.x maintenance (alphas, RTM, patches via tags) |

**Contributors:** branch off **`development`**. **Hotfixes** for a shipped train: branch off **`release/X.Y`**, PR back into that release line, then merge back to `main` → `development` when the fix still applies.

```mermaid
flowchart TB
  subgraph daily [Daily work]
    feat[feature_or_bugfix_branch]
    devel[development]
    feat -->|"PR"| devel
    devel -->|"PR"| mainBr[main]
  end
  subgraph ship [Ship and maintain 1.0]
    mainBr -->|"cut once"| rel10[release_1.0]
    rel10 -->|"tag"| t1[v1.0.0-alpha.1]
    hf[hotfix_branch]
    rel10 -->|"checkout base"| hf
    hf -->|"PR"| rel10
    rel10 -->|"tag"| t2[v1.0.0-alpha.2]
    rel10 -->|"merge back"| mainBr
    mainBr -->|"merge"| devel
  end
```

**Examples**

```text
# Normal feature
git checkout development && git pull
git checkout -b feature/fix-filter-null
# PR → development

# Hotfix published 1.0 train
git checkout release/1.0 && git pull
git checkout -b hotfix/1.0-null-filter
# PR → release/1.0 → bump Version → tag v1.0.0-alpha.2 → merge back to main
```

nuget.org publish is **tag-only** and the tagged commit must be on `release/*` (CI guard). Full policy, CI table, and first-tag steps: [CONTRIBUTING.md](CONTRIBUTING.md) (Branching & releases).

At startup the ABI gate requires:
1. `zvec_check_version(MinimumMajor, MinimumMinor, MinimumPatch)` (native ≥ minimum), **and**
2. `zvec_get_version_major() == MinimumMajor` (same major).

A mismatch throws `ZVecAbiMismatchException`.

---

## Project Structure

```
ZVec.NET/
├── src/
│   ├── Native/ZVec.Native/           # CMake -> upstream zvec_c_api
│   │   └── external/zvec/            # Git submodule (alibaba/zvec)
│   └── Core/ZVec.NET/                # Published assembly (PackageId: ZVec.NET)
│       ├── Abstractions/              # IZvecFactory, IZvecCollection, IZvecCollectionOfT (+ role interfaces)
│       ├── Mapping/                  # ZVec.NET.Mapping — attrs, TypeModel, Mapper, ExpressionFilter
│       ├── Concurrency/              # CollectionCallGate
│       ├── DependencyInjection/      # AddZVec, AddZVecCollection / AddZVecCollection<T>, ZVecOptions
│       ├── Builders/                 # SchemaBuilder (+ From<T>())
│       ├── Interop/                  # NativeMethods, SafeHandles, NativeLibraryResolver
│       ├── Internal/                 # Collection*Ops, native builders/unmarshallers
│       ├── Models/                   # ZVecDoc, ZVecStatus, enums
│       ├── IndexParams/              # All 8 index param types
│       ├── Query/                    # ZVecQuery, ZVecFtsQuery, ZVecReranker, FilterBuilder
│       ├── ZVecFactory.cs
│       ├── ZVecCollection.cs         # Dynamic façade over Collection*Ops
│       ├── ZVecCollectionOfT.cs      # Typed ODM façade IZvecCollection<T>
│       └── ZVecNativeAbi.cs
├── testing/
│   ├── ZVec.NET.Tests/               # xUnit + FluentAssertions (real native + Skip)
│   └── ZVec.NET.Benchmarks/          # BenchmarkDotNet
├── build/                            # .snk + CI scripts
├── ZVec.NET.slnx                     # Solution (VS .slnx)
├── Directory.Build.props
└── Directory.Packages.props
```

---

## Contributing

We welcome contributions! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for:

- Local development setup (C++ submodule init + CMake build)
- Branching topology (`development` → `main` → `release/1.0` → tag), mermaid diagrams, and examples
- `feature/*` / `bugfix/*` off **`development`**; `hotfix/*` off **`release/X.Y`**
- API shape guidelines (DI-first, Builder pattern, full `type.h` enum coverage)
- Zero-allocation rules on hot paths
- Testing approach (real native library; tests Skip when the DLL is unavailable)

---

## License

[MIT](LICENSE) — same as [upstream ZVec](https://github.com/alibaba/zvec/blob/main/LICENSE).

---

## Links

- **ZVec (upstream):** [github.com/alibaba/zvec](https://github.com/alibaba/zvec)
- **ZVec docs:** [zvec.org](https://zvec.org)
- **Project Plan:** [ZVec.NET-Project-Plan.md](ZVec.NET-Project-Plan.md)
- **Implementation Epics:** [ZVec.NET-Implementation-Plan.md](ZVec.NET-Implementation-Plan.md)
- **NuGet:** [nuget.org/packages/ZVec.NET](https://www.nuget.org/packages/ZVec.NET/)
