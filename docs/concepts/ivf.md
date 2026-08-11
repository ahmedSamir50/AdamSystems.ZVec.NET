# IVF

Inverted File index — clusters vectors and searches a subset of lists (`nprobe`).

## When to use

- Larger datasets where HNSW memory is costly
- Tunable speed/recall via probe count

## ZVec.NET mapping

| Concern | Type / API |
|---------|------------|
| Build params | `ZVecIvfIndexParam` |
| Query params | `ZVecIvfQueryParams` (`nprobe`, scale factor, …) |

## Pitfalls

- Too-low `nprobe` hurts recall; too-high hurts latency
- Training/build assumptions follow upstream ZVec — see product docs

## Upstream

- [zvec.org](https://zvec.org)
- First-party math: [Theory: IVF](../theory/ivf.md)
