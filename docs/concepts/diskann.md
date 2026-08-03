# DiskANN

Disk-oriented ANN for large collections that do not fit comfortably in RAM.

## When to use

- Very large corpora on Linux hosts
- Out-of-core / disk-backed search workloads

## ZVec.NET mapping

| Concern | Type / API |
|---------|------------|
| Build params | `ZVecDiskAnnIndexParam` |
| Platform gate | **Linux only** — SDK throws `PlatformNotSupportedException` elsewhere |
| I/O | libaio optional via dlopen; otherwise synchronous pread |

## Pitfalls

- Not available on Windows/macOS/mobile RIDs
- Plan capacity and path layout from upstream guidance

## Upstream

- [zvec.org](https://zvec.org)
- First-party math: [Theory: DiskANN](../theory/diskann.md)
- [RIDs / feature limits](../guides/rids.md)
