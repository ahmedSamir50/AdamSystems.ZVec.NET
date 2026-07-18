# ZVec.NET

[![NuGet](https://img.shields.io/nuget/v/ZVec.NET.svg)](https://www.nuget.org/packages/ZVec.NET/)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512bd4.svg)](https://dotnet.microsoft.com/)
[![CI](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/actions/workflows/build-managed.yml/badge.svg)](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/actions/workflows/build-managed.yml)

> **Beta** — `1.0.0-beta.2+zvec.0.5.1`. APIs may still evolve. PackageId **`ZVec.NET`** on nuget.org (tag `v1.0.0-beta.2`). Distinct from the unrelated NuGet package named [`Zvec`](https://www.nuget.org/packages/Zvec).

**Production .NET SDK for [Alibaba ZVec](https://github.com/alibaba/zvec)** — DI, typed ODM, async, SafeHandles, full indexes/FTS, and mobile RIDs. Not a thin P/Invoke wrapper.

**Host demos:** [samples/](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/samples) — ASP.NET Minimal API, **MAUI Blazor Hybrid** (offline/edge RAG), and Console. See [samples/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/README.md).

## Contents

- [Why ZVec.NET?](#why-zvecnet)
- [Requirements](#requirements)
- [Quick Start](#quick-start)
- [Host patterns](#host-patterns)
- [Architecture](#architecture)
- [API Overview](#api-overview)
- [Samples](#samples)
- [Performance](#performance)
- [Troubleshooting](#troubleshooting)
- [Versioning](#versioning)
- [Project structure](#project-structure)
- [Contributing](#contributing)
- [License](#license)
- [Links](#links)

---

## Why ZVec.NET?

| Feature | What it means for you |
|---------|----------------------|
| **DI-first for hosts** | `AddZVec()` / `AddZVecCollection<T>()` — ASP.NET Core, MAUI, Blazor Server out of the box |
| **Typed ODM** | Map POCOs with `ZVec.NET.Mapping` — schema `From<T>()`, expression filters, typed CRUD/DDL without magic field strings |
| **Sync + Async APIs** | Lowest-latency sync for batch jobs; async `ValueTask` for ASP.NET Core (cooperative cancel; no thread-pool offload today) |
| **Pin-based vector pipelines** | `ReadOnlyMemory<float>` on hot paths — no intermediate `float[]` copies on the query pin path |
| **Safe native lifecycle** | Collection handles owned by `SafeZvecHandle` (close-only); `Dispose` closes, `Destroy` deletes then closes; `Shutdown` disposes all tracked open collections before `zvec_shutdown` |
| **Cross-platform natives** | Single NuGet `ZVec.NET` with `runtimes/{rid}/native/` for **win-x64**, **linux-x64**, **osx-arm64**, **android-arm64/x64** in beta |
| **Full ZVec DB coverage** | HNSW, Flat, IVF, HNSW-RaBitQ, DiskANN, Vamana, Invert, FTS indexes; hybrid search; schema evolution; in-DB RRF/Weighted rerankers |
| **Idiomatic C#** | .NET naming guidelines, `ValueTask`, `CancellationToken`, fluent builders |

### Compared to NuGet package `Zvec`

Both wrap Alibaba ZVec. Prefer **`ZVec.NET`** for ASP.NET / MAUI / app code; the other package is a thinner sync P/Invoke surface.

| | `Zvec` ([TheBitBrine](https://www.nuget.org/packages/Zvec)) | `ZVec.NET` (this package) |
|---|---|---|
| Surface | Sync P/Invoke helpers | DI + typed ODM + sync/async |
| Vectors | `float[]` | `ReadOnlyMemory<float>` pin path |
| Indexes | HNSW / IVF / Flat / Invert | + RaBitQ, DiskANN, Vamana, FTS |
| Platforms | Desktop RIDs | + Android (iOS planned); net8 / net9 / net10 |
| Lifecycle | Dispose each document | SafeHandle collections; factory shutdown |

Install this SDK as **`ZVec.NET`** — not `Zvec`.

---

## Requirements

| Requirement | Detail |
|-------------|--------|
| **.NET** | TFMs `net8.0`, `net9.0`, `net10.0` (LTS floor: .NET 8) |
| **PackageId** | **`ZVec.NET`** (not [`Zvec`](https://www.nuget.org/packages/Zvec)) |
| **Native RID** | Matching `runtimes/{rid}/native/` binary in the package (see below) |
| **Samples** | [.NET 10 SDK](https://dotnet.microsoft.com/download) only; not shipped in the NuGet package |
| **Out of scope** | Blazor WebAssembly (no native RID) |

**Owner on nuget.org:** [AdamSystems](https://www.nuget.org/profiles/AdamSystems). **Source:** [ahmedSamir50/AdamSystems.ZVec.NET](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET).

### Native RIDs (NuGet `runtimes/`)

Managed TFMs are `net8.0` / `net9.0` / `net10.0` (samples need .NET 10). Natives ship under `runtimes/{rid}/native/`.

**Why some RIDs are missing:** not unfinished C# P/Invoke — **cross-compiling Alibaba zvec’s bundled C++ third parties** (mainly Apache Arrow and FastPFOR/SIMDe, plus host `protoc`, and on Apple Lz4/Arrow macabi). ZVec.NET applies [CI-only patches](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/build/ci/patches) (not pushed to alibaba/zvec). A RID ships when that build is **reliably green** and pack always includes it in the nupkg — no calendar date promised. Engineering detail: [build/ci/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/build/ci/README.md#rid-ship-gate).

#### Supported in `1.0.0-beta.2`

| RID | Native file | Status |
|-----|-------------|--------|
| `win-x64` | `zvec_c_api.dll` | Pack-required; desktop CI tested |
| `linux-x64` | `libzvec_c_api.so` | Pack-required; desktop CI tested |
| `osx-arm64` | `libzvec_c_api.dylib` | Pack-required; desktop CI tested |
| `android-arm64`, `android-x64` | `libzvec_c_api.so` | Intended ship RID (NDK CI); mobile jobs still soft-fail until hardened |

#### Not yet shipped — cause and unblock

| RID | Native file | Real reason | Unblock when |
|-----|-------------|-------------|--------------|
| `win-arm64` | `zvec_c_api.dll` | MSVC amd64→arm64 cross: FastPFOR needs SIMDe; Arrow PCG MSVC ARM64; host `protoc` (ARM64-built protoc cannot run on the x64 runner). Compile-only today (no Windows ARM64 run gate). | Optional CI job hard-green (no `continue-on-error`); pack always includes `runtimes/win-arm64`; release notes bump. Local patches bridge until upstream FastPFOR/Arrow accept fixes. |
| `linux-arm64` | `libzvec_c_api.so` | Cross from x86_64: Arrow EP enables SSE unless told aarch64/NEON; OpenSSL off for cross; host x86_64 `protoc` (aarch64 protoc won’t exec on the runner). | Same gate: optional→required + always in nupkg. |
| `osx-x64` | `libzvec_c_api.dylib` | Building `x86_64` on arm64 macOS runners: zvec auto-detects **host** arch for `-march` incorrectly (needs `CMAKE_OSX_ARCHITECTURES`). | Hard-green + pack include. |
| `ios-arm64`, `iossimulator-arm64`, `maccatalyst-arm64` | `libzvec_c_api.dylib` | Apple mobile/Catalyst CMake + third parties (iOS dual-STATIC `OUTPUT_NAME`, Lz4/Arrow macabi); host `protoc` (iOS-built protoc SIGKILL on Mac). Mobile workflow still `continue-on-error`. | Sustained green Apple-mobile CI + pack always ships those RIDs + MAUI device QA. |

#### Never supported / feature limits (not a RID packaging issue)

| Item | Why |
|------|-----|
| **Blazor WebAssembly** | No native `zvec_c_api` RID |
| **HNSW-RaBitQ on ARM** | Upstream ISA (x86_64 + AVX2 only); SDK throws `PlatformNotSupportedException` before native call — see [Index Types](#index-types) |
| **DiskANN on non-Linux** | Upstream Linux + **libaio** only; same SDK gate — see [Index Types](#index-types) |

Package size grows with each RID. There is **no** fixed 50 MB gate — see pack workflow / release notes for measured size.

---

## Quick Start

### Install

```bash
dotnet add package ZVec.NET --version 1.0.0-beta.2
```

Version scheme: `1.0.0-beta.2+zvec.0.5.1` (SDK SemVer + pinned native). TFMs are `lib/net8.0` … `lib/net10.0` — **not** encoded in the version string. Local tests Skip if the native for your RID is missing; Pack CI requires desktop natives.

### Two APIs

| API | When to use |
|-----|-------------|
| **Typed (recommended)** | `IZvecCollection<T>`, `ZVecCollectionSchemaBuilder.From<T>()`, `AddZVecCollection<T>`, expression filters (`p => p.Category == x`). Compile-time field safety via `ZVec.NET.Mapping`. |
| **Dynamic (escape hatch)** | `IZvecCollection`, `ZVecDoc`, string field names, `ZVecFilterBuilder.Where("…")`, `AddZVecCollection("key", …)`. Tooling, dynamic schemas, parity with Python/Node shapes. |

Typed is a thin façade over dynamic (`IZvecCollection<T>.Untyped`).

**DDL note:** native `add_column` / typed `EnsureSchema` only add **nullable numeric** columns. Put string/array fields in the create-time schema.

### Console / script (typed — shortest path)

```csharp
using ZVec.NET;
using ZVec.NET.Mapping;

using var factory = new ZVecFactory();
factory.Initialize(new ZVecOptions { LogLevel = ZVecLogLevel.Warn });

var path = "/tmp/products";
var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();
using var untyped = factory.CreateAndOpen(path, schema);
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

// Later / after restart: Open loads Schema from on-disk metadata (no schema argument).
using var reopened = factory.Open(path);
using IZvecCollection<Product> again = new ZVecCollection<Product>(reopened);
var doc = again.Fetch("p1"); // Title / Category present — not Id+Score only
_ = reopened.Schema;         // non-null; bound for unmarshalling
```

### Document model (`Product`)

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
```

`Product` is a plain document POCO — it does **not** implement `IZvecCollection`. Inject / hold `IZvecCollection<Product>`. Schema comes from `ZVecCollectionSchemaBuilder.From<Product>()` / `AddZVecCollection<Product>`.

| Member | Required? | Rule |
|--------|-----------|------|
| Identity | Yes (exactly one) | Convention: public `string Id` / `ID`, **or** `[ZVecId]` |
| Vector properties | **Yes** `[ZVecVector(dim, …)]` | Dimension / metric / index cannot be inferred from `ReadOnlyMemory<float>` alone |
| Scalar properties | Usually none | Mapped by property name + CLR type; optional `[ZVecField("storageName")]` / `Nullable` |
| Skip a property | `[ZVecIgnore]` | |
| Collection name | Optional `[ZVecCollection("name")]` | Defaults to the CLR type name |

### Typed filters

Expression filters on `IZvecCollection<T>` compile to native filter strings via `ZVecExpressionFilter` (same engine as `DeleteByFilter`). Property names use the mapped storage name (`[ZVecField]` overrides apply).

```csharp
products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Category == "demo");
products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Year > 2020);
products.Query(p => p.Embedding, vec, topK: 10,
    filter: p => p.Category == "ai" && p.Year >= 2020);
products.DeleteByFilter(p => p.Category == "expired");
```

| Supported | Ops / shapes |
|-----------|----------------|
| Compare | `==` `!=` `<` `<=` `>` `>=` (constant on either side) |
| Boolean | `&&` `\|\|` `!`, nested parentheses |
| Null | `== null` / `!= null` → `IsNull` / `IsNotNull` |
| Values | string, bool, int/long/float/double (and similar numerics) |

**Unsupported** (throws `ZVecException`): method calls (`StartsWith`, `Contains`, …), indexers, or anything that is not a comparison / boolean tree. Escape hatch:

```csharp
products.Untyped.Query(
    new ZVecQuery { FieldName = "Embedding", Vector = vec },
    topk: 10,
    filter: ZVecFilterBuilder.Create().Where("Category", ZVecCompareOp.Eq, "demo"));
```

---

## Host patterns

### ASP.NET Core / Blazor Server (typed)

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

// Create: true (default) = CreateAndOpen — first run only; see Create vs Open below
builder.Services.AddZVecCollection<Product>(options =>
{
    options.Path = "/data/products";
    options.EnableMmap = true;
    options.Create = true; // set false to Open an existing path
});

var app = builder.Build();
```

```csharp
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

### Configuration (`appsettings.json`)

`AddZVec(IConfiguration)` binds the **`ZVec`** section to `ZVecOptions`:

```json
{
  "ZVec": {
    "LogLevel": "Warn",
    "QueryThreads": -1,
    "MemoryLimitMb": 512,
    "MaxConcurrentNativeCalls": 0
  }
}
```

```csharp
builder.Services.AddZVec(builder.Configuration);
// or: builder.Services.AddZVec(builder.Configuration.GetSection("ZVec"));
```

### Create vs Open (restart-safe collections)

Upstream `CreateAndOpen` **throws if the path already exists** (same as Python/Node). There is no native `open_or_create`.

| API | Behavior |
|-----|----------|
| `factory.CreateAndOpen(path, schema)` | Create new collection; fails if path exists |
| `factory.Open(path)` | Open existing; loads schema from on-disk metadata |
| `AddZVecCollection<T>(… Create = true)` | DI → `CreateAndOpen` (default; **first run only**) |
| `AddZVecCollection<T>(… Create = false)` | DI → `Open` |

App-level open-or-create (used by samples):

```csharp
IZvecCollection<Product> OpenOrCreate(IZvecFactory factory, string path)
{
    var options = new ZVecCollectionOptions { EnableMmap = true };
    if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
        return new ZVecCollection<Product>(factory.Open(path, options));

    var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();
    return new ZVecCollection<Product>(factory.CreateAndOpen(path, schema, options));
}
```

See [samples `CollectionBootstrap.OpenOrCreate`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/ZVec.NET.Samples.Shared/CollectionBootstrap.cs).

### Keyed dynamic collection

```csharp
builder.Services.AddZVecCollection("products", options =>
{
    options.Path = "/data/products";
    options.Create = false; // open existing
});

// Inject: [FromKeyedServices("products")] IZvecCollection products
```

### Health checks

`ZVecHealthCheck` reports whether the registered `IZvecFactory` is initialized:

```csharp
using ZVec.NET.DependencyInjection;

builder.Services.AddHealthChecks()
    .AddCheck<ZVecHealthCheck>("zvec");
```

Requires a package that provides `AddHealthChecks()` (e.g. `Microsoft.Extensions.Diagnostics.HealthChecks`).

### MAUI / offline edge

Same DI surface as ASP.NET (`AddZVec` + `AddZVecCollection<T>`). For a full offline/edge RAG host (LM Studio + Gemma), see [samples/ZVec.NET.Samples.Maui](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/samples/ZVec.NET.Samples.Maui) and [samples/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/README.md) (prerequisites, models, datasets). Embeddings and chat are **sample host** concerns — not part of the NuGet DB SDK.

### Advanced / dynamic (`ZVecDoc`)

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
    filter: ZVecFilterBuilder.Create().Where("title", ZVecCompareOp.Eq, "Hello"),
    includeVector: false);
```

---

## Architecture

ZVec.NET is built for thread-safe use against the native engine:

1. **Instance-based factory:** `ZVecFactory` tracks its own state and open collection handles. A process-wide `lock` ensures exactly-once native library initialization (`zvec_initialize`).
2. **Collection lifecycle:** Open collections own a `SafeZvecHandle`. `Dispose` closes (data preserved); `Destroy` deletes on-disk data then closes. `Destroy` after `Dispose` throws `ObjectDisposedException`. `ZVecFactory.Shutdown` disposes every tracked open collection before native shutdown.
3. **Optional throttles:** `MaxConcurrentNativeCalls` / `MaxConcurrentReads` use `SemaphoreSlim` when > 0; `0` means unlimited. Native ZVec is already thread-safe.
4. **Zero-copy pipelines:** Hot paths pin `ReadOnlyMemory<float>` and pass pointers to native code.

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
          │  Builders / DTOs / DI        │
          ├──────────────────────────────┤
          │  Collection*Ops + CallGate   │
          │  SafeZvecHandle + P/Invoke   │
          └─────────────┬──────────────┘
                        │  Flat C ABI
          ┌─────────────▼──────────────┐
          │  zvec_c_api → ZVec C++ Core │
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

Prefer `includeVector: false` when you do not need result embeddings (lower latency and GC alloc). Default remains `true` for backward compatibility.

**Single vector**

```csharp
var hits = col.Query(
    new ZVecQuery { FieldName = "vec", Vector = myVec },
    topk: 10,
    includeVector: false);
```

**Full-text (FTS)**

```csharp
var hits = col.Query(
    new ZVecQuery
    {
        FieldName = "content",
        Fts = new ZVecFtsQuery { QueryString = "search terms" }
    },
    topk: 10,
    includeVector: false);
```

**Multi-vector + RRF rerank** (requires ≥ 2 sub-queries)

```csharp
var hits = col.Query(
    [
        new ZVecQuery { FieldName = "title_vec", Vector = titleVec },
        new ZVecQuery { FieldName = "body_vec", Vector = bodyVec }
    ],
    topk: 10,
    reranker: new ZVecRrfReranker { TopN = 10 },
    includeVector: false);
```

**Hybrid (dense + sparse) + filter + RRF**

```csharp
var denseQ = new ZVecQuery { FieldName = "vector1", Vector = dense };
var sparseQ = new ZVecQuery
{
    FieldName = "sparse1",
    SparseVector = new Dictionary<int, float> { [0] = 1.0f, [3] = 0.5f }
};
var filter = ZVecFilterBuilder.Create()
    .Where("category", ZVecCompareOp.Eq, "demo");
var hits = col.Query(
    [denseQ, sparseQ],
    topk: 5,
    reranker: new ZVecRrfReranker { TopN = 5 },
    filter: filter,
    includeVector: false);
```

**Dense + FTS + weighted rerank**

```csharp
var hits = col.Query(
    [
        new ZVecQuery { FieldName = "vec", Vector = dense },
        new ZVecQuery
        {
            FieldName = "content",
            Fts = new ZVecFtsQuery { QueryString = "zvec maui" }
        }
    ],
    topk: 10,
    reranker: new ZVecWeightedReranker
    {
        TopN = 10,
        Weights = new Dictionary<string, float>
        {
            ["vec"] = 0.7f,
            ["content"] = 0.3f
        }
    },
    includeVector: false);
```

**Filtered (typed)** — `products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Year > 2020)`

**Filtered (dynamic)** — `col.Query(query, topk: 10, filter: ZVecFilterBuilder.Create().Where("year", ZVecCompareOp.Gt, 2020))`

**Query-by-id** — set `ZVecQuery.DocumentId` to load that document’s embedding (extra managed `Fetch`), then search. Result docs already include IDs from native.

**Group-by** — `QueryGroupBy` / `QueryGroupByAsync` are **not supported** in this SDK release (`[Obsolete]`; throw `NotSupportedException`). Use filter + client-side grouping, or track upstream native group-by support.

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

Every mutating/querying operation exposes both sync and async variants. Async methods are **cancellation-aware wrappers** around synchronous native P/Invoke (they complete on the caller thread — not a thread-pool offload). When optional throttles are enabled (`MaxConcurrentNativeCalls` / `MaxConcurrentReads` > 0), async paths await the gate with `WaitAsync` (cancelable); after the gate is acquired, P/Invoke still runs synchronously on the continuation thread.

```csharp
var results = col.Query(query, topk: 10, includeVector: false);
var results = await col.QueryAsync(query, topk: 10, includeVector: false, cancellationToken: ct);
```

**ASP.NET Core guidance:** For heavy batch insert/optimize workloads, prefer the **sync** APIs on a dedicated worker (`BackgroundService`, channel consumer, or your own bounded queue) rather than unbounded `Task.Run` per request. Use `MaxConcurrentNativeCalls` only when you must **bound** how many threads may block in P/Invoke at once (`0` = unlimited). Do not wrap every SDK call in `Task.Run`.

---

## Samples

Host apps under [`samples/`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/samples) — **not** in the NuGet package; never gates packaging CI. All sample projects target **.NET 10**.

| Project | Role |
|---------|------|
| `ZVec.NET.Samples.Maui` | Flagship — Status + RAG + Search + Recommend (AppData + mmap) |
| `ZVec.NET.Samples.AspNet` | Minimal API parity (status, hints, models, seed, query) |
| `ZVec.NET.Samples.Console` | Interactive menu + CLI shortcuts |
| `ZVec.NET.Samples.Shared` | Shared helpers (not a package) |

```bash
dotnet build samples/ZVec.NET.Samples.slnx
dotnet run --project samples/ZVec.NET.Samples.Console
```

Prerequisites (LM Studio, natives, datasets): [samples/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/README.md).

---

## Performance

Workload for primary numbers: **768-dim Flat**, `win-x64`, native ZVec `v0.5.1-35-g1afdea8`, .NET 8.0.29, Intel Core i7-8850H, BenchmarkDotNet **medium** job (2026-07-17). Query/memory benches seed **10_000** docs; insert batch size **1000**. Primary latency / alloc benches use **`includeVector: false`**.

Official engine scale (VectorDBBench, Cohere 1M/10M): [zvec.org benchmarks](https://zvec.org/en/docs/db/benchmarks/) — not apples-to-apples with the 10k Flat binding suite.

### Binding suite: .NET vs Python vs Node.js (same machine, 10k Flat)

| Metric | .NET (ours) | Python | Node.js |
|--------|-------------|--------|---------|
| Warm query (128 docs, topk=10, no result vectors) | **0.512 ms** (median 0.390 ms) | 0.414 ms | 1.594 ms |
| Query (10k docs, topk=10, no result vectors) | **2.88 ms** | 4.33 ms | 4.103 ms |
| Batch insert (1000 docs) | **~16.8k docs/sec** | ~7.1k docs/sec | ~5.8k docs/sec |
| GC alloc / query (no result vectors) | **6.8 KB** | n/a | n/a |
| GC alloc / query (with topk vectors) | **40.5 KB** | n/a | n/a |

### Measured suite (primary, .NET BDN medium)

| Method | Mean | Allocated |
|--------|------|----------:|
| `Query_Sync` (10k, no vectors) | 2.88 ms | 6.8 KB |
| `Query_Sync_WithVectors` (10k) | 2.57 ms | 40.5 KB |
| `Query_WithFilter` (10k, no vectors) | 1.43 ms | 1.7 KB |
| `Query_WarmTinyCorpus` (128, no vectors) | 512 µs | 6.7 KB |
| `Insert_Batch` (1000 docs) | 59.4 ms | 446 KB |
| `Fetch_ScalarOnly` | 193 µs | 904 B |

**Typed ODM:** wall time ≈ dynamic `ZVecDoc` on insert/query (native dominates); expect **more managed allocations** per op (`TypedOdmOverheadBench`).

### How to reproduce

```bash
# Full suite (README numbers — medium job)
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *

# Subsets
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *QueryThroughputBench*
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *MemoryDiagnosisBench*
dotnet run -c Release --project testing/ZVec.NET.Benchmarks -- -j medium -f *InsertThroughputBench*
dotnet run -c Release --project testing/ZVec.NET.Benchmarks --filter *TypedOdmOverheadBench*
```

Job names are lowercase (`medium` / `short`). Classes: `QueryThroughputBench`, `MemoryDiagnosisBench`, `InsertThroughputBench`, `VectorMarshallingBench`, `FilterParsingBench`, `TypedOdmOverheadBench`, `EngineScaleReferenceBench`, plus legacy `ZVecPerformanceBenchmarks` (128-dim smoke — not the primary baseline).

---

## Troubleshooting

| Symptom | Likely cause / fix |
|---------|-------------------|
| `DllNotFoundException` / native load failure | Host RID not in the nupkg, or local `runtimes/{rid}/native/` is empty. Check [supported vs not-yet RIDs](#native-rids-nuget-runtimes). Use a shipped RID, or build/deploy natives (see [CONTRIBUTING.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/CONTRIBUTING.md)). |
| `ZVecAbiMismatchException` | Native ABI below floor or major mismatch. Use a package whose `+zvec.*` pin matches the shipped `zvec_c_api`. |
| Create fails: path already exists | Use `factory.Open` / `Create = false`, or the [open-or-create pattern](#create-vs-open-restart-safe-collections). |
| `PlatformNotSupportedException` (RaBitQ) | HNSW-RaBitQ needs x86_64 + AVX2; not available on Arm/Arm64 ([feature limits](#never-supported--feature-limits-not-a-rid-packaging-issue)). |
| `PlatformNotSupportedException` (DiskANN) | DiskANN is Linux + libaio only ([feature limits](#never-supported--feature-limits-not-a-rid-packaging-issue)). |

| Expression filter throws | Method calls / unsupported shapes — use `ZVecFilterBuilder` or `products.Untyped`. |
| Empty scalars after Open | Schema should load from on-disk metadata; if an old broken folder remains, delete the collection path once and recreate. |
| Samples won’t run | Need .NET 10 SDK + local native for your RID; see [samples/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/README.md). |

---

## Versioning

| What | Format | Example |
|------|--------|---------|
| **SDK version** | SemVer | `1.0.0-beta.2` |
| **ZVec native pin** | Build metadata after `+` | `+zvec.0.5.1` |
| **.NET target** | TFM + `lib/` folder | `net8.0` (LTS) |
| **ABI floor** | `ZVecNativeAbi` | Minimum `0.5.1`, same major |
| **Git tag** | `v` + SemVer (no `+`) | `v1.0.0-beta.2` |
| **Git branch (train)** | `release/1.0` | Long-lived 1.0.x line |

NuGet version example: `1.0.0-beta.2+zvec.0.5.1`. Do **not** put TFM or branch names into the version string. There is **no** branch named `release/1.0.0-beta.2+zvec.0.5.1`.

At startup the ABI gate requires:
1. `zvec_check_version(MinimumMajor, MinimumMinor, MinimumPatch)` (native ≥ minimum), **and**
2. `zvec_get_version_major() == MinimumMajor` (same major).

A mismatch throws `ZVecAbiMismatchException`.

Branching topology, hotfix flow, and tag-only nuget.org publish: [CONTRIBUTING.md — Branching & releases](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/CONTRIBUTING.md#branching--releases).

---

## Project structure

```
ZVec.NET/
├── src/Core/ZVec.NET/          # Published assembly (PackageId: ZVec.NET)
├── src/Native/ZVec.Native/     # CMake → upstream zvec_c_api (+ submodule)
├── testing/                    # xUnit tests + BenchmarkDotNet
├── samples/                    # Host demos (.NET 10; not in NuGet)
├── build/                      # .snk + CI scripts
├── ZVec.NET.slnx               # Core + tests + benchmarks
└── samples/ZVec.NET.Samples.slnx
```

---

## Contributing

We welcome contributions! Please read [CONTRIBUTING.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/CONTRIBUTING.md) for:

- Local development setup (C++ submodule init + CMake build)
- Branching (`development` → `main` → `release/1.0` → tag)
- API shape guidelines (DI-first, typed ODM preferred, full `type.h` enum coverage)
- Zero-allocation rules on hot paths
- Testing approach (real native library; tests Skip when the DLL is unavailable)

---

## License

[MIT](LICENSE) — same as [upstream ZVec](https://github.com/alibaba/zvec/blob/main/LICENSE).

---

## Links

- **ZVec (upstream):** [github.com/alibaba/zvec](https://github.com/alibaba/zvec)
- **ZVec docs:** [zvec.org](https://zvec.org)
- **Samples:** [samples/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/README.md)
- **Contributing:** [CONTRIBUTING.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/CONTRIBUTING.md)
- **Project Plan:** [ZVec.NET-Project-Plan.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/ZVec.NET-Project-Plan.md)
- **Implementation Epics:** [ZVec.NET-Implementation-Plan.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/ZVec.NET-Implementation-Plan.md)
- **NuGet:** [nuget.org/packages/ZVec.NET](https://www.nuget.org/packages/ZVec.NET/)
