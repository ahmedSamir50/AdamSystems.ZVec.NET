# DI hosts

## ASP.NET Core / Blazor Server (typed)

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

// OpenMode default = OpenOrCreate (restart-safe).
builder.Services.AddZVecCollection<Product>(options =>
{
    options.Path = "/data/products";
    options.EnableMmap = true;
});

var app = builder.Build();
```

```csharp
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

## Configuration (`appsettings.json`)

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
```

## Create vs Open (restart-safe)

Upstream `CreateAndOpen` **throws if the path already exists**. There is no native `open_or_create`. ZVec.NET adds managed `OpenOrCreate` and defaults DI to it.

| API | Behavior |
|-----|----------|
| `factory.CreateAndOpen(path, schema)` | Create new; fails if path exists |
| `factory.Open(path)` | Open existing; loads schema from disk |
| `factory.OpenOrCreate(path, schema)` | Open if path has content; otherwise create |
| `AddZVecCollection<T>(… OpenMode = OpenOrCreate)` | DI default — **restart-safe** |
| `CreateOnly` / `OpenOnly` | Map to `CreateAndOpen` / `Open` |

## Keyed dynamic collection

```csharp
builder.Services.AddZVecCollection("products", options =>
{
    options.Path = "/data/products";
    options.OpenMode = ZVecCollectionOpenMode.OpenOnly;
});
```

## Native lifecycle

Treat `IZvecFactory` as a **process singleton**: one `Initialize` per process; `Shutdown` once at host stop. DI host shutdown handles this automatically.

Use `NativeTeardownPolicy.Suppress` only when abandoning native handles at process exit (rare).

## Health checks

```csharp
builder.Services.AddHealthChecks()
    .AddCheck<ZVecHealthCheck>("zvec");
```

Requires a package that provides `AddHealthChecks()` (e.g. `Microsoft.Extensions.Diagnostics.HealthChecks`).

## MAUI / offline edge

Same DI surface (`AddZVec` + `AddZVecCollection<T>`). Full offline/edge RAG host: [samples/ZVec.NET.Samples.Maui](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/samples/ZVec.NET.Samples.Maui). Embeddings and chat are **sample host** concerns — not part of the NuGet DB SDK.
