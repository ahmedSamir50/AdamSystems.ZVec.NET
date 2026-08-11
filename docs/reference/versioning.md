# Versioning

| What | Format | Example |
|------|--------|---------|
| **SDK version** | SemVer | `1.0.0-beta.5` |
| **ZVec native pin** | Build metadata after `+` | `+zvec.0.6.0` |
| **.NET target** | TFM + `lib/` folder | `net8.0` (LTS) |
| **ABI floor** | `ZVecNativeAbi` | Minimum `0.6.0`, same major |
| **Git tag** | `v` + SemVer (no `+`) | `v1.0.0-beta.5` |
| **Git branch (train)** | `release/1.0` | Long-lived 1.0.x line |

NuGet version example: `1.0.0-beta.5+zvec.0.6.0`. Do **not** put TFM or branch names into the version string.

## ABI gate

At startup:

1. `zvec_check_version(MinimumMajor, MinimumMinor, MinimumPatch)` (native ≥ minimum), **and**
2. `zvec_get_version_major() == MinimumMajor` (same major).

A mismatch throws `ZVecAbiMismatchException`.

## Changelog

- [CHANGELOG.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/CHANGELOG.md)
- Branching / tags: [Contributing](../contributing.md)
