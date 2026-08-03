# Hybrid search and FTS

Prefer `includeVector: false` when you do not need result embeddings (lower latency and GC alloc). Default remains `true` for backward compatibility.

## Single vector

```csharp
var hits = col.Query(
    new ZVecQuery { FieldName = "vec", Vector = myVec },
    topk: 10,
    includeVector: false);
```

## Full-text (FTS)

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

## Multi-vector + RRF (requires ≥ 2 sub-queries)

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

## Hybrid (dense + sparse) + filter + RRF

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

## Dense + FTS + weighted rerank

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

## Filter builder (dynamic)

```csharp
var filter = ZVecFilterBuilder.Create()
    .Where("publish_year", ZVecCompareOp.Gt, 2020)
    .And(f => f
        .Where("category", ZVecCompareOp.Eq, "fiction")
        .Or(g => g.ContainAny("tags", "AI", "ML")))
    .Build();
```

## Group-by

`QueryGroupBy` / `QueryGroupByAsync` remain **not executable** (`NotSupportedException`). Python uses pybind11 → C++; the official C API has no `zvec_collection_group_by_query`. Details: [Native API coverage](../reference/native-api-coverage.md).

## Theory / product docs

- Primers: [RRF](../concepts/rrf.md), [FTS](../concepts/fts.md)
- Upstream: [zvec.org](https://zvec.org)
