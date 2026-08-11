# Flat

Brute-force (exact) vector search — no graph/cluster approximation.

## When to use

- Small corpora where exactness matters more than ANN speed
- Baselines and correctness checks against approximate indexes

## ZVec.NET mapping

| Concern | Type / API |
|---------|------------|
| Build params | `ZVecFlatIndexParam` |
| Query params | `ZVecFlatQueryParams` |

## Pitfalls

- Query cost grows linearly with corpus size
- Fine for demos and the binding benchmark suite (10k Flat); not a substitute for HNSW/IVF at million scale

## Upstream

- [zvec.org](https://zvec.org)
- [Theory: metrics](../theory/metrics.md)
