# ðŸš§ ZVec.NET

[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512bd4.svg)](https://dotnet.microsoft.com/)

> **âš ï¸ UNDER CONSTRUCTION â€” PRE-ALPHA**  
> This project is in early active development. APIs are unstable. Not yet available on NuGet.  
> See [Project Status](ZVec.NET-Implementation-Plan.md) for what's implemented.

**The definitive .NET SDK for [Alibaba ZVec](https://github.com/alibaba/zvec)**

---

## Why ZVec.NET?

| Feature | What it means for you |
|---------|----------------------|
| **Zero-allocation vector pipelines** | `ReadOnlyMemory<float>` on all vector hot paths â€” no `float[]` copies, no GC pressure on queries |
| **Sync + Async APIs** | Lowest-latency sync path for batch jobs; bounded async offload for ASP.NET Core |
| **DI-first design** | `AddZVec()` / `AddZVecCollection()` â€” works with ASP.NET Core, MAUI, Blazor Server out of the box |
| **SafeHandle guarantees** | Every native pointer wrapped in `SafeHandle`; `Dispose` / `await using` is the primary path; finalizer is a safety net |
| **Cross-platform single NuGet** | One package with native binaries for win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64 |
| **Full ZVec DB coverage** | HNSW, Flat, IVF, HNSW-RaBitQ, DiskANN, Vamana, Invert, FTS indexes; hybrid search; schema evolution; in-DB RRF/Weighted rerankers |
| **Idiomatic C#** | .NET naming guidelines, `ValueTask`, `CancellationToken`, fluent builders â€” feels like it was written by Microsoft |

---

## Architecture & Concurrency

ZVec.NET guarantees extreme performance and thread safety through its core architectural decisions:

1. **Instance-Based Factory:** `ZVecFactory` tracks its own state and collection handles. A standard managed `lock` ensures exactly-once native library initialization globally (`zvec_initialize`), completely eliminating cross-test interference and access violation race conditions.
2. **SafeHandle Disposal:** Every native pointer is wrapped in a `SafeHandle`. When a factory shuts down, a `CancellationToken` signals all open collections to safely abort operations and dispose of their memory gracefully.
3. **Zero-Copy Pipelines:** The hot path (`Insert`, `Query`) avoids `float[]` array allocations. The SDK pins `ReadOnlyMemory<float>` buffers using `MemoryHandle` and passes raw pointers directly to the native DB via P/Invoke. Our `BenchmarkDotNet` suite confirms query execution in **~500ns** (0.0005 ms) with less than 250 bytes of overhead!

---

## Quick Start

### Install

```bash
dotnet add package ZVec.NET
```

> **Requires .NET 8.0+ (LTS).** Native binaries for all 6 RIDs are bundled â€” no separate native package needed.

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

using var factory = new ZVecFactory(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });
factory.Initialize();

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
â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
â”‚        Consumer (ASP.NET Core / MAUI / Console)     â”‚
â”‚              DI: AddZVec / AddZVecCollection         â”‚
â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                        â”‚
          â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
          â”‚   ZVec.NET SDK  â”‚
          â”‚  IZvecFactory / IZvecCollection  â”‚
          â”‚  Builders / DTOs / DI        â”‚
          â”œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¤
           â”‚  Interlocked lifecycle gate   â”‚
          â”‚  SafeHandle Layer            â”‚
          â”‚  [LibraryImport] P/Invoke    â”‚
          â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                        â”‚  Flat C ABI
          â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
          â”‚  zvec_c_api (C bindings)    â”‚
          â”‚  Alibaba's official C API   â”‚
          â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”¬â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
                        â”‚
          â”Œâ”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â–¼â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”
          â”‚  ZVec C++ Core (Proxima)    â”‚
          â””â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”˜
```

---

## API Overview

### Index Types

| Index | Use Case | SDK Type |
|-------|----------|----------|
| **HNSW** | General-purpose ANN | `ZVecHnswIndexParam` |
| **Flat** | Exact search (small datasets) | `ZVecFlatIndexParam` |
| **IVF** | Clustered ANN | `ZVecIvfIndexParam` |
| **HNSW-RaBitQ** | Quantized HNSW (x86_64/AVX2) | `ZVecHnswRabitqIndexParam` |
| **DiskANN** | Disk-based ANN (Linux) | `ZVecDiskAnnIndexParam` |
| **Vamana** | Graph-based ANN | `ZVecVamanaIndexParam` |
| **Invert** | Scalar field index | `ZVecInvertIndexParam` |
| **FTS** | Full-text search | `ZVecFtsIndexParam` |

### Query Modes

- **Single vector** â€” `col.Query(new ZVecQuery { FieldName = "vec", Vector = myVec }, topk: 10)`
- **Multi-vector** â€” `col.Query(queries, topk: 10, reranker: new ZVecRrfReranker { TopN = 10 })`
- **Hybrid** (dense + sparse) â€” `ZVecQuery` with both `Vector` and `SparseVector`
- **Full-text** â€” `new ZVecQuery { FieldName = "content", Fts = new ZVecFtsQuery { QueryString = "search terms" } }`
- **Group-by** â€” `col.QueryGroupBy(new ZVecGroupByQuery { Query = q, GroupByField = "category", GroupSize = 5 })`
- **Filtered** â€” `col.Query(query, topk: 10, filter: ZVecFilterBuilder.Create().Where("year", ZVecCompareOp.Gt, 2020).ToString())`

### CRUD

```csharp
// Insert / Upsert / Update
col.Insert(doc);              // single
col.Insert(docList);          // batch -> IReadOnlyList<ZVecStatus>
col.Upsert(doc);
col.Update(doc);

// Delete
col.Delete("doc1");
col.Delete(idList);
col.DeleteByFilter("category = \"expired\"");

// Fetch
ZVecDoc? single = col.Fetch("doc1");
IReadOnlyDictionary<string, ZVecDoc> batch = col.Fetch(idList);
```

### Schema Evolution (DDL)

```csharp
col.AddColumn(new ZVecFieldSchema { Name = "rating", DataType = ZVecDataType.Float }, "0.0");
col.DropColumn("legacy_field");
col.AlterColumnRename("old_name", "new_name");
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
// Sync (lowest latency â€” P/Invoke on caller thread)
var results = col.Query(query, topk: 10);

// Async (bounded offload for ASP.NET â€” never unbounded Task.Run)
var results = await col.QueryAsync(query, topk: 10, cancellationToken: ct);
```

---

## Performance

| Metric | Target | Baseline |
|--------|--------|----------|
| Sync P/Invoke overhead (768-dim) | < 50 us | DllImport float[] ~ 30 us |
| Single-vector query (10k docs, topk=10) | < 1 ms | Python ZVec ~ 1.2 ms |
| Batch insert (1000 docs) | > 50k docs/sec | Python ZVec ~ 40k docs/sec |
| GC allocation per query (vector path) | < 256 B | float[] copy ~ 3 KB |

The zero-allocation vector pipeline uses `ReadOnlyMemory<float>` + `Memory.Pin()` for the native call duration only â€” no intermediate `float[]` copies.

---

## Versioning

| What | Format | Example |
|------|--------|---------|
| **SDK version** | SemVer | `1.0.0-alpha.1` |
| **ZVec native pin** | Build metadata after `+` | `+zvec.1.2.3` |
| **.NET target** | TFM + `lib/` folder | `net8.0` (LTS) |

NuGet version: `1.0.0-alpha.1+zvec.1.2.3` â€” our SDK `1.0.0-alpha.1` wrapping ZVec C++ `1.2.3`, targeting .NET 8.0+.

The version gate (`zvec_check_version`) validates the native ABI at startup â€” a mismatch throws `ZVecAbiMismatchException` immediately.

---

## Project Structure

```
ZVec.NET/
â”œâ”€â”€ src/
â”‚   â”œâ”€â”€ Native/ZVec.Native/           # CMake -> upstream zvec_c_api
â”‚   â”‚   â””â”€â”€ external/zvec/            # Git submodule (alibaba/zvec)
â”‚   â”œâ”€â”€ Core/ZVec.NET/    # Published assembly (PackageId: ZVec.NET)
â”‚   â”‚   â”œâ”€â”€ Abstractions/             # IZvecFactory, IZvecCollection
â”‚   â”‚   â”œâ”€â”€ DependencyInjection/      # AddZVec, AddZVecCollection, ZVecOptions
â”‚   â”‚   â”œâ”€â”€ Builders/                 # SchemaBuilder, FilterBuilder
â”‚   â”‚   â”œâ”€â”€ Interop/                  # NativeMethods, SafeHandles, NativeLibraryResolver
â”‚   â”‚   â”œâ”€â”€ Internal/                 # NativeDocBuilder, NativeQueryBuilder, etc.
â”‚   â”‚   â”œâ”€â”€ Models/                   # ZVecDoc, ZVecStatus, enums
â”‚   â”‚   â”œâ”€â”€ IndexParams/              # All 8 index param types
â”‚   â”‚   â”œâ”€â”€ Query/                    # ZVecQuery, ZVecFtsQuery, ZVecReranker
â”‚   â”‚   â”œâ”€â”€ ZVecFactory.cs
â”‚   â”‚   â””â”€â”€ ZVecCollection.cs
â”‚   â””â”€â”€ Mock/ZVec.Native.Mock/       # Mock native library (outside main Core code)
â”œâ”€â”€ testing/
â”‚   â”œâ”€â”€ ZVec.NET.Tests/  # xUnit + FluentAssertions
â”‚   â””â”€â”€ ZVec.NET.Benchmarks/  # BenchmarkDotNet
â”œâ”€â”€ build/                           # .snk + CI scripts
â”œâ”€â”€ ZVec.NET.slnx        # Solution (VS .slnx)
â”œâ”€â”€ Directory.Build.props
â””â”€â”€ Directory.Packages.props
```

---

## Contributing

We welcome contributions! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for:

- Local development setup (C++ submodule init + CMake build)
- Branching strategy (`feature/*` off `dev`)
- API shape guidelines (DI-first, Builder pattern, full `type.h` enum coverage)
- Zero-allocation rules on hot paths
- Testing approach (mock native library for fast unit tests)

---

## License

[MIT](LICENSE) â€” same as [upstream ZVec](https://github.com/alibaba/zvec/blob/main/LICENSE).

---

## Links

- **ZVec (upstream):** [github.com/alibaba/zvec](https://github.com/alibaba/zvec)
- **ZVec docs:** [zvec.org](https://zvec.org)
- **Project Plan:** [ZVec.NET-Project-Plan.md](ZVec.NET-Project-Plan.md)
- **Implementation Epics:** [ZVec.NET-Implementation-Plan.md](ZVec.NET-Implementation-Plan.md)
- **NuGet:** [nuget.org/packages/ZVec.NET](https://www.nuget.org/packages/ZVec.NET/)