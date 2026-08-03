# Package identity

| | `ZVec.NET` (this SDK) | `Zvec` (other NuGet) |
|---|----------------------|----------------------|
| Owner | AdamSystems | TheBitBrine |
| Surface | DI + typed ODM + sync/async | Thinner sync P/Invoke helpers |
| Vectors | `ReadOnlyMemory<float>` pin path | `float[]` |
| Indexes | HNSW / IVF / Flat / Invert + RaBitQ, DiskANN, Vamana, FTS | HNSW / IVF / Flat / Invert |
| Platforms | Desktop + Android + iOS HARD RIDs; Catalyst soft | Desktop + Android + iOS |

Install:

```bash
dotnet add package ZVec.NET
```

NuGet: [nuget.org/packages/ZVec.NET](https://www.nuget.org/packages/ZVec.NET/)
