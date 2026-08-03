# HNSW

Hierarchical Navigable Small World graphs are the default general-purpose ANN index.

## When to use

- Medium-to-large collections where approximate nearest neighbor is acceptable
- Low-latency online query with tunable recall via `ef_search`

## ZVec.NET mapping

| Concern | Type / API |
|---------|------------|
| Build params | `ZVecHnswIndexParam` (`M`, `EfConstruction`, quantization, `EnableRotate`, …) |
| Query params | `ZVecHnswQueryParams` (`EfSearch`, radius, linear/refiner options) |
| Typed attribute | `[ZVecVector(dim, M = …, EfConstruction = …)]` |

## Pitfalls

- Higher `M` / `EfConstruction` improves recall and increases memory/build time
- Prefer `includeVector: false` when result embeddings are unused

## Upstream

- [zvec.org](https://zvec.org) — index configuration and tuning
- First-party math: [Theory: HNSW](../theory/hnsw.md)
