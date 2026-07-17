# ZVec.NET.Samples.Maui (.NET 10) — flagship edge RAG

```bash
dotnet build samples/ZVec.NET.Samples.Maui -f net10.0-windows10.0.19041.0
dotnet build samples/ZVec.NET.Samples.Maui -t:Run -f net10.0-windows10.0.19041.0
```

- Collection path: `FileSystem.AppDataDirectory` + mmap
- Ingest paste/text or built-in T0 fixtures
- Ask → retrieve + Gemma 4 E2B + citations (EmbeddingGemma + chat concurrent in LM Studio)
- Status bar probes both LM Studio models and dataset download progress
- Background ensure of T1 packs on startup (skip if already present)

Requires .NET 10 MAUI workload and win-x64 `zvec_c_api` available to the process (see samples README).
