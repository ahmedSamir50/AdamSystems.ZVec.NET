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
| **DI-first design** | `AddZVec()` / `AddZVecCollection()` — works with ASP.NET Core, MAUI, Blazor Server out of the box |
| **Safe native lifecycle** | Collection handles owned by `SafeZvecHandle` (close-only); `Dispose` closes, `Destroy` deletes then closes; `Shutdown` disposes all tracked open collections before `zvec_shutdown` |
| **Cross-platform NuGet (planned)** | Target RIDs: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64 — full multi-RID packaging is Epic E21 |
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

> **Requires .NET 8.0+ (LTS).** Pre-alpha: native binaries for your current RID must be present locally (or tests Skip). Multi-RID NuGet bundling is planned in Epic E21.

### ASP.NET Core / Blazor Server

```csharp
// Program.cs
using ZVec.NET.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddZVec(options =>
{
    options.LogLevel = ZVecLogLevel.Warn;
    options.QueryThreads = -1;
    options.MemoryLimitMb = 512;
});

builder.Services.AddZVecCollection("products", options =>
{
    options.Path = "/data/products";
    options.Schema = new ZVecCollectionSchemaBuilder("products")
        .AddField("title", ZVecDataType.String)
        .AddField("category", ZVecDataType.String)
        .AddVector("embedding", ZVecDataType.VectorFp32, 768,
            new ZVecHnswIndexParam { MetricType = ZVecMetricType.Cosine, M = 32, EfConstruction = 256 })
        .Build();
    options.EnableMmap = true;
});

var app = builder.Build();
```

```csharp
// Inject anywhere
public class ProductService(IZvecCollection products)
{
    public async Task<IReadOnlyList<ZVecDoc>> SearchAsync(ReadOnlyMemory<float> queryVector, string? category = null)
    {
        var filter = category is not null
            ? ZVecFilterBuilder.Create().Where("category", ZVecCompareOp.Eq, category).Build()
            : null;

        return await products.QueryAsync(
            new ZVecQuery { FieldName = "embedding", Vector = queryVector },
            topk: 10,
            filter: filter);
    }
}
```

### Console / Batch (no DI)

```csharp
using ZVec.NET;
using ZVec.NET.Query;

using var factory = new ZVecFactory();
factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });

var schema = new ZVecCollectionSchemaBuilder("docs")
    .AddVector("vec", ZVecDataType.VectorFp32, 768, new ZVecHnswIndexParam())
    .Build();

using var col = factory.CreateAndOpen("/tmp/docs", schema);

// Insert
var doc = ZVecDoc.Create("doc1",
    denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
    {
        ["vec"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
    },
    fields: new Dictionary<string, object> { ["title"] = "Hello ZVec" });

col.Insert(doc);

// Query
var results = col.Query(
    new ZVecQuery { FieldName = "vec", Vector = queryVec },
    topk: 10);

foreach (var hit in results)
    Console.WriteLine($"{hit.Id} (score: {hit.Score:F4})");
```

---

## Architecture

```
┌────────────────────────────────────────────────────┐
│        Consumer (ASP.NET Core / MAUI / Console)     │
│              DI: AddZVec / AddZVecCollection         │
└───────────────────────┬────────────────────────────┘
                        │
          ┌─────────────▼──────────────┐
          │   ZVec.NET SDK             │
          │  IZvecFactory / IZvecCollection │
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
- **Filtered** — `col.Query(query, topk: 10, filter: ZVecFilterBuilder.Create().Where("year", ZVecCompareOp.Gt, 2020))`

### CRUD

```csharp
// Insert / Upsert / Update
col.Insert(doc);              // single
col.Insert(docList);          // batch
col.Upsert(doc);
col.Update(doc);

// Delete
col.Delete("doc1");
col.Delete(idList);
col.DeleteByFilter("category = \"expired\"");

// Fetch
ZVecDoc? single = col.Fetch("doc1");
IReadOnlyList<ZVecDoc> batch = col.Fetch(idList);
```

### Schema Evolution (DDL)

```csharp
col.AddColumn(new ZVecFieldSchema { Name = "rating", DataType = ZVecDataType.Float }, "0.0");
col.DropColumn("legacy_field");
col.AlterColumn("old_name", newName: "new_name");
col.CreateIndex("embedding", new ZVecHnswIndexParam { M = 32 });
col.DropIndex("embedding");
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

Queries that set `ZVecQuery.DocumentId` perform an extra `Fetch` (with vectors) before search — expect an additional native round-trip.
---

## Performance

Workload for primary numbers: **768-dim Flat**, `win-x64`, native ZVec `v0.5.1-35-g1afdea8`, .NET 8.0.29, Intel Core i7-8850H, BenchmarkDotNet **ShortRun** (2026-07-17, re-run after Core lifetime/ops split). Query/memory benches seed **10_000** docs; insert batch size **1000**. ShortRun Error margins are wide — treat means as directional.

**Why these differ from older smoke numbers:** an earlier table used legacy `ZVecPerformanceBenchmarks` (**128-dim**, empty/tiny corpus). That path reported ~ns latency and ~208 B allocated. The primary suite below uses **768-dim / 10k Flat** and materializes **topk=10** results (including vectors), so mean latency is in milliseconds and Allocated is dominated by result unmarshalling (~10 × 768 × 4 B ≈ 30 KB of vector bytes alone, ~44 KB total) — not a silent P/Invoke regression. Filtered query (`Query_WithFilter`, one match) allocating ~5.6 KB shows the same pin path with a smaller result set.

### Upstream engine scale (VectorDBBench — published)

Official ZVec [Benchmarks](https://zvec.org/en/docs/db/benchmarks/) use VectorDBBench on Cohere **1M / 10M** (768-dim). Documented in code as `UpstreamEngineScaleBaseline` / `EngineScaleReferenceBench` (not re-run locally):

| Case | Scale | Published figure | Notes |
|------|-------|------------------|-------|
| `Performance768D1M` | Cohere **1M** × 768-d | See VectorDBBench run on docs page | Engine scale |
| `Performance768D10M` | Cohere **10M** × 768-d | Homepage **8500+ QPS**; index build **~1 hour** | Engine scale |

These are **not** apples-to-apples with the 10k Flat SDK binding suite. A single-threaded inverse of local query mean (~3.3 ms → ~300 QPS) is a different metric shape than concurrent VectorDBBench QPS at 10M.

### Binding suite: .NET vs Python vs Node.js (same machine, 10k Flat)

Ephemeral parity scripts (Python `zvec` **0.5.1**, Node `@zvec/zvec` **0.5.0** via `npm install --registry https://registry.npmjs.org/`) were run once on the same host and discarded — not kept in this repo. Official docs do not publish this 10k Flat recipe. Python/Node columns are historical; .NET column refreshed with the latest ShortRun.

| Metric | .NET (ours) | Python | Node.js |
|--------|-------------|--------|---------|
| Warm query (128 docs, topk=10) | **0.495 ms** | 0.396 ms | 1.594 ms |
| Query (10k docs, topk=10) | **3.33 ms** | 3.180 ms | 4.103 ms |
| Batch insert (1000 docs) | **~15.7k docs/sec** | ~15.2k docs/sec | ~5.8k docs/sec |
| GC alloc / query (topk=10 + vectors) | 44.4 KB | n/a | n/a |

### Targets vs measured (.NET)

| Metric | Target | Measured | Status |
|--------|--------|----------|--------|
| Warm query, tiny corpus (128 docs, topk=10) | &lt; 50 µs | 495 µs (`Query_WarmTinyCorpus`) | Miss — full warm query, not a no-op P/Invoke stub |
| Single-vector query (10k docs, topk=10) | &lt; 1 ms | 3.33 ms (`Query_Sync`) | Miss |
| Batch insert (1000 docs) | &gt; 50k docs/sec | ~15.7k docs/sec (`Insert_Batch`, 63.8 ms/batch) | Miss |
| GC allocation per query (pin path, 10k / topk=10) | &lt; 256 B | 44.4 KB (`Query_768Dim` / `Query_Sync`) | Miss — dominated by **result materialization** (not query-vector marshalling) |

**Allocation split:** the query vector still uses `ReadOnlyMemory<float>` + `Memory.Pin()` (no intermediate `float[]` copy). Pin vs explicit copy is **44.4 KB** vs **47.4 KB** (`Query_ReadOnlyMemory` vs `Query_ExplicitCopy`) — the ~3 KB delta is the marshalling comparison; the ~44 KB floor is returning topk docs with vectors.

### Measured suite (primary, .NET BDN)

| Method | Mean | Allocated |
|--------|------|----------:|
| `Query_Sync` (10k docs) | 3.33 ms | 44.4 KB |
| `Query_WithFilter` (10k docs) | 1.77 ms | 5.6 KB |
| `Query_WarmTinyCorpus` (128 docs) | 495 µs | 44.3 KB |
| `Query_768Dim` | 3.13 ms | 44.4 KB |
| `Query_ReadOnlyMemory` (pin) | 3.43 ms | 44.4 KB |
| `Query_ExplicitCopy` | 3.52 ms | 47.4 KB |
| `Fetch_ScalarOnly` | 210 µs | 1.2 KB |
| `Insert_Single` | 58.1 µs | 1.4 KB |
| `Insert_Batch` (1000 docs) | 63.8 ms | 454 KB |
| `Build_SimpleFilter` | 59 ns | 128 B |
| `Build_CompoundFilter` | 217 ns | 544 B |
| `Local_10k_Query_ForEngineScaleContext` | ~3.08 ms | 44.4 KB |

### How to reproduce (.NET)

```bash
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j short -f *.QueryThroughputBench.* *.MemoryDiagnosisBench.* *.InsertThroughputBench.* *.VectorMarshallingBench.* *.FilterParsingBench.* *.EngineScaleReferenceBench.* --join
```

Named classes: `VectorMarshallingBench`, `QueryThroughputBench`, `InsertThroughputBench`, `MemoryDiagnosisBench`, `FilterParsingBench`, `EngineScaleReferenceBench` (+ `UpstreamEngineScaleBaseline` constants for Cohere 1M/10M). Legacy `ZVecPerformanceBenchmarks` (128-dim smoke) remains for quick local checks; it is not the primary baseline.

---

## Versioning

| What | Format | Example |
|------|--------|---------|
| **SDK version** | SemVer | `1.0.0-alpha.1` |
| **ZVec native pin** | Build metadata after `+` | `+zvec.0.5.1` |
| **.NET target** | TFM + `lib/` folder | `net8.0` (LTS) |
| **ABI floor** | `ZVecNativeAbi` | Minimum `0.5.1`, same major |

NuGet version example: `1.0.0-alpha.1+zvec.0.5.1` — SDK `1.0.0-alpha.1` wrapping ZVec C++ `0.5.1`.

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
│       ├── Abstractions/              # IZvecFactory, IZvecCollection (+ role interfaces)
│       ├── Concurrency/              # CollectionCallGate
│       ├── DependencyInjection/      # AddZVec, AddZVecCollection, ZVecOptions
│       ├── Builders/                 # SchemaBuilder
│       ├── Interop/                  # NativeMethods, SafeHandles, NativeLibraryResolver
│       ├── Internal/                 # Collection*Ops, native builders/unmarshallers
│       ├── Models/                   # ZVecDoc, ZVecStatus, enums
│       ├── IndexParams/              # All 8 index param types
│       ├── Query/                    # ZVecQuery, ZVecFtsQuery, ZVecReranker, FilterBuilder
│       ├── ZVecFactory.cs
│       ├── ZVecCollection.cs         # Thin façade over Collection*Ops
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
- Branching strategy (`feature/*` off `dev`)
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
