# Quick start

## Two APIs

| API | When to use |
|-----|-------------|
| **Typed (recommended)** | `IZvecCollection<T>`, `ZVecCollectionSchemaBuilder.From<T>()`, `AddZVecCollection<T>`, expression filters |
| **Dynamic (escape hatch)** | `IZvecCollection`, `ZVecDoc`, string field names, `ZVecFilterBuilder` |

Typed is a thin façade over dynamic (`IZvecCollection<T>.Untyped`).

!!! note "DDL"
    Native `add_column` / typed `EnsureSchema` only add **nullable numeric** columns. Put string/array fields in the create-time schema.

## Console / script (typed)

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
var doc = again.Fetch("p1");
_ = reopened.Schema;
```

## Document model (`Product`)

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

| Member | Required? | Rule |
|--------|-----------|------|
| Identity | Yes (exactly one) | Convention: public `string Id` / `ID`, **or** `[ZVecId]` |
| Vector properties | **Yes** `[ZVecVector(dim, …)]` | Dimension / metric / index cannot be inferred from `ReadOnlyMemory<float>` alone |
| Scalar properties | Usually none | Mapped by property name + CLR type |
| Skip a property | `[ZVecIgnore]` | |

## Typed filters

```csharp
products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Category == "demo");
products.Query(p => p.Embedding, vec, topK: 10, filter: p => p.Year > 2020);
products.DeleteByFilter(p => p.Category == "expired");
```

| Supported | Ops / shapes |
|-----------|----------------|
| Compare | `==` `!=` `<` `<=` `>` `>=` |
| Boolean | `&&` `\|\|` `!` |
| Null | `== null` / `!= null` |

**Unsupported** (throws `ZVecException`): method calls (`StartsWith`, `Contains`, …). Escape hatch: `products.Untyped.Query(...)` with `ZVecFilterBuilder`.

## Next

- [DI hosts](../guides/di.md)
- [Typed ODM](../guides/odm.md)
- [Hybrid search and FTS](../guides/hybrid-fts.md)
