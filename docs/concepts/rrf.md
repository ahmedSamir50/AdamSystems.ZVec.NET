# RRF and weighted rerank

Fuse multiple sub-query result lists inside the DB (dense, sparse, FTS).

## When to use

- Multi-field or hybrid retrieval (e.g. title vector + body vector, or dense + FTS)
- Prefer in-DB fusion over ad-hoc client-side merge when possible

## ZVec.NET mapping

| Reranker | Type | Notes |
|----------|------|--------|
| Reciprocal rank fusion | `ZVecRrfReranker` | Default RRF \(k\) is typically **60** upstream-style |
| Weighted | `ZVecWeightedReranker` | Per-field weights dictionary |

Requires **≥ 2** sub-queries on the multi-query path. See [Hybrid search and FTS](../guides/hybrid-fts.md).

## Pitfalls

- Single-query calls do not need a reranker
- Weight keys must match field names used in sub-queries

## Upstream / theory

- Product docs: [zvec.org](https://zvec.org)
- First-party math: [Theory: RRF and fusion](../theory/rrf.md)
