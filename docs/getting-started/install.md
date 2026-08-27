# Install

```bash
dotnet add package ZVec.NET --version 1.0.0-beta.6
```

## Requirements

| Requirement | Detail |
|-------------|--------|
| **.NET** | TFMs `net8.0`, `net9.0`, `net10.0` (LTS floor: .NET 8) |
| **PackageId** | **`ZVec.NET`** (not [`Zvec`](https://www.nuget.org/packages/Zvec)) |
| **Native RID** | Matching `runtimes/{rid}/native/` binary in the package |
| **Samples** | [.NET 10 SDK](https://dotnet.microsoft.com/download) only; not shipped in the NuGet package |
| **Out of scope** | Blazor WebAssembly (no native RID) |

Version scheme: `1.0.0-beta.6+zvec.0.7.0` (SDK SemVer + pinned native). TFMs live under `lib/` — **not** in the version string.

See [Native RIDs](../guides/rids.md) for the pack-required matrix and feature limits (RaBitQ, DiskANN).

## Next

- [Quick start](quick-start.md)
- [Package identity](../reference/package-identity.md)
