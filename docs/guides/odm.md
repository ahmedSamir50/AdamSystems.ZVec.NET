# Typed ODM

ZVec.NET maps POCOs through `ZVec.NET.Mapping` so schemas, filters, and CRUD stay compile-time safe.

## Schema from type

```csharp
var schema = ZVecCollectionSchemaBuilder.From<Product>().Build();
```

Or via DI: `AddZVecCollection<Product>(...)`.

## Attributes

| Attribute | Role |
|-----------|------|
| Convention `Id` / `ID` or `[ZVecId]` | Document identity (exactly one) |
| `[ZVecVector(dim, …)]` | Dense vector field + index params |
| `[ZVecField("storageName")]` | Rename / scalar options |
| `[ZVecIgnore]` | Skip property |
| `[ZVecCollection("name")]` | Collection name (default: CLR type name) |

## Expression filters

`IZvecCollection<T>` filters compile to native filter strings via `ZVecExpressionFilter` (same engine as `DeleteByFilter`).

Supported: comparisons, `&&` / `||` / `!`, null checks, numeric/string/bool constants.

Unsupported: method calls, indexers — use `ZVecFilterBuilder` on `Untyped`.

## CRUD (typed)

```csharp
products.Insert(product);
products.Upsert(product);
products.Update(product);
products.Delete("p1");
products.DeleteByFilter(p => p.Category == "expired");
Product? single = products.Fetch("p1");
```

## Schema evolution (DDL)

```csharp
// EnsureSchema adds missing scalar columns only (never drops).
// Native add_column supports nullable numeric types; string columns belong in create schema.
await products.EnsureSchemaAsync();
await products.DropColumnAsync(p => p.Year); // explicit destructive
await products.CreateIndexAsync(p => p.Year, new ZVecInvertIndexParam());
```

## Dynamic escape hatch

```csharp
using var col = factory.CreateAndOpen("/tmp/docs", new ZVecCollectionSchemaBuilder("docs")
    .AddField("title", ZVecDataType.String)
    .AddVector("vec", ZVecDataType.VectorFp32, 768, new ZVecHnswIndexParam())
    .Build());

col.Insert(ZVecDoc.Create("doc1",
    denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["vec"] = queryVec },
    fields: new Dictionary<string, object> { ["title"] = "Hello" }));
```

Typed wall time ≈ dynamic on insert/query (native dominates); expect more managed allocations per op.
