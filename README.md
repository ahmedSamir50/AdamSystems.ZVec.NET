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
| **Zero-allocation vector pipelines** | `ReadOnlyMemory<float>` on all vector hot paths — no `float[]` copies, no GC pressure on queries |
| **Sync + Async APIs** | Lowest-latency sync path for batch jobs; bounded async offload for ASP.NET Core |
| **DI-first design** | `AddZVec()` / `AddZVecCollection()` — works with ASP.NET Core, MAUI, Blazor Server out of the box |
| **Safe native lifecycle** | Collection handles use `nint` + `Interlocked` dispose/destroy; query/doc helpers use `SafeHandle` where ownership is short-lived |
| **Cross-platform NuGet (planned)** | Target RIDs: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64 — full multi-RID packaging is Epic E21 |
| **Full ZVec DB coverage** | HNSW, Flat, IVF, HNSW-RaBitQ, DiskANN, Vamana, Invert, FTS indexes; hybrid search; schema evolution; in-DB RRF/Weighted rerankers |
| **Idiomatic C#** | .NET naming guidelines, `ValueTask`, `CancellationToken`, fluent builders |

---

## Architecture & Concurrency

ZVec.NET is built for thread-safe use against the native engine:

1. **Instance-based factory:** `ZVecFactory` tracks its own state and open collection handles. A process-wide `lock` ensures exactly-once native library initialization (`zvec_initialize`).
2. **Collection lifecycle:** Open collections hold a raw `nint` handle. `Dispose` / `Destroy` use `Interlocked` flags (idempotent). Short-lived query/doc native objects use `SafeHandle` helpers.
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
            ? ZVecFilterBuilder.Create().Where("category", ZVecCompareOp.Eq, category).ToString()
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
          │  IZvecFactory / IZvecCollection  │
          │  Builders / DTOs / DI        │
          ├──────────────────────────────┤
          │  Interlocked lifecycle gate   │
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
- **Filtered** — `col.Query(query, topk: 10, filter: ZVecFilterBuilder.Create().Where("year", ZVecCompareOp.Gt, 2020).ToString())`

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

```csharp
var filter = ZVecFilterBuilder.Create()
    .Where("publish_year", ZVecCompareOp.Gt, 2020)
    .And(ZVecFilterBuilder.Create()
        .Where("category", ZVecCompareOp.Eq, "fiction")
        .Or(ZVecFilterBuilder.Create().ContainAny("tags", "AI", "ML")));

// -> "publish_year > 2020 AND (category = \"fiction\" OR tags CONTAIN_ANY [\"AI\", \"ML\"])"
```

### Sync + Async

Every mutating/querying operation exposes both sync and async variants:

```csharp
// Sync (lowest latency — P/Invoke on caller thread)
var results = col.Query(query, topk: 10);

// Async (bounded offload for ASP.NET — never unbounded Task.Run)
var results = await col.QueryAsync(query, topk: 10, cancellationToken: ct);
```

---

## Performance

| Metric | Target | Notes |
|--------|--------|-------|
| Sync P/Invoke overhead (768-dim) | &lt; 50 µs | Target; measure with BenchmarkDotNet |
| Single-vector query (10k docs, topk=10) | &lt; 1 ms | Target vs Python ZVec baseline |
| Batch insert (1000 docs) | &gt; 50k docs/sec | Target |
| GC allocation per query (vector path) | &lt; 256 B | Intermediate + result objects only |

The zero-allocation vector pipeline uses `ReadOnlyMemory<float>` + `Memory.Pin()` for the native call duration only — no intermediate `float[]` copies.

**Measured** (2026-07-17, `win-x64`, .NET 8.0.29, Intel Core i7-8850H, Release BenchmarkDotNet, **128-dim Flat**, tiny/empty corpus — not the 768-dim / 10k-doc targets above):

| Method | Mean | Allocated |
|--------|------|----------:|
| `InsertDocument` | 3.31 µs | 496 B |
| `QueryVector` (topk=10) | 516 ns | 208 B |

Query allocation is under the 256 B target on this path. Latency and insert allocation are **not** comparable to the aspirational rows (different dimension, corpus size, and workload). Re-run `dotnet run -c Release --project testing/ZVec.NET.Benchmarks` after meaningful corpus/index changes.

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
│       ├── Abstractions/              # IZvecFactory, IZvecCollection
│       ├── DependencyInjection/      # AddZVec, AddZVecCollection, ZVecOptions
│       ├── Builders/                 # SchemaBuilder
│       ├── Interop/                  # NativeMethods, SafeHandles, NativeLibraryResolver
│       ├── Internal/                 # NativeDocBuilder, NativeQueryBuilder, etc.
│       ├── Models/                   # ZVecDoc, ZVecStatus, enums
│       ├── IndexParams/              # All 8 index param types
│       ├── Query/                    # ZVecQuery, ZVecFtsQuery, ZVecReranker, FilterBuilder
│       ├── ZVecFactory.cs
│       ├── ZVecCollection.cs
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
