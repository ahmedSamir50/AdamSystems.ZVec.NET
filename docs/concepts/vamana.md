# Vamana

Graph-based ANN index (often used with mmap / out-of-core layouts in ZVec).

## When to use

- Graph ANN alternative to classic HNSW for certain scale/memory profiles
- Workloads that benefit from upstream Vamana/mmap options

## ZVec.NET mapping

| Concern | Type / API |
|---------|------------|
| Build params | `ZVecVamanaIndexParam` |
| Platform | All supported RIDs (subject to upstream limits) |

## Pitfalls

- Parameter names and defaults follow native ZVec — prefer product docs for tuning curves
- Validate recall on your embedding distribution before production cutover

## Upstream

- [zvec.org](https://zvec.org)
- First-party math: [Theory: Vamana](../theory/vamana.md)
