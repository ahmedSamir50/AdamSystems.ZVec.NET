# Index concepts

Short **when to use** primers plus pointers to [zvec.org](https://zvec.org). First-party math, citations, and SDK defaults live under [Theory](../theory/index.md).

## Index types in ZVec.NET

| Index | Use case | SDK type | Platform notes |
|-------|----------|----------|----------------|
| **HNSW** | General-purpose ANN | `ZVecHnswIndexParam` | All supported RIDs |
| **Flat** | Exact search (small datasets) | `ZVecFlatIndexParam` | All supported RIDs |
| **IVF** | Clustered ANN | `ZVecIvfIndexParam` | All supported RIDs |
| **HNSW-RaBitQ** | Quantized HNSW | `ZVecHnswRabitqIndexParam` | x86_64 + AVX2 only; C API create still blocked — see [coverage](../reference/native-api-coverage.md) |
| **DiskANN** | Disk-based ANN | `ZVecDiskAnnIndexParam` | **Linux only** |
| **Vamana** | Graph-based ANN | `ZVecVamanaIndexParam` | All supported RIDs |
| **Invert** | Scalar field index | `ZVecInvertIndexParam` | All supported RIDs |
| **FTS** | Full-text search | `ZVecFtsIndexParam` | All supported RIDs |

## Primers

- [HNSW](hnsw.md)
- [Flat](flat.md)
- [IVF](ivf.md)
- [DiskANN](diskann.md)
- [Vamana](vamana.md)
- [FTS](fts.md)
- [RRF and weighted rerank](rrf.md)

## Upstream

- Product docs: [zvec.org](https://zvec.org)
- Benchmarks: [zvec.org/en/docs/db/benchmarks](https://zvec.org/en/docs/db/benchmarks/)
