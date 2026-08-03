# Troubleshooting

| Symptom | Likely cause / fix |
|---------|-------------------|
| `DllNotFoundException` / native load failure | Host RID not in the nupkg, or local `runtimes/{rid}/native/` is empty. See [RIDs](rids.md). Build/deploy natives per [CONTRIBUTING.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/CONTRIBUTING.md). |
| `ZVecAbiMismatchException` | Native ABI below floor or major mismatch. Use a package whose `+zvec.*` pin matches the shipped `zvec_c_api`. |
| Create fails: path already exists | Use `factory.Open` / `OpenMode = OpenOnly`, or `factory.OpenOrCreate` / default DI `OpenOrCreate`. |
| Linux process exit 139 on stop | Fixed in **`1.0.0-beta.3.2`** (log-config ownership). Upgrade to ≥beta.4; ensure factory `Shutdown` / DI host stop. |
| `PlatformNotSupportedException` (RaBitQ) | Needs x86_64 + AVX2; not on Arm/Arm64. |
| `PlatformNotSupportedException` (DiskANN) | Linux-only. |
| Expression filter throws | Method calls / unsupported shapes — use `ZVecFilterBuilder` or `products.Untyped`. |
| Empty scalars after Open | Schema should load from on-disk metadata; if an old broken folder remains, delete the collection path once and recreate. |
| Samples won’t run | Need .NET 10 SDK + local native for your RID; see [samples/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/README.md). |
| `NotSupportedException` (group-by / RaBitQ create) | C API gaps — [Native API coverage](../reference/native-api-coverage.md). |
