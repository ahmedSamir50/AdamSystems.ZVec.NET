# ZVec.NET Samples (.NET 10 only)

User demos for offline/edge RAG, semantic search, and recommendations.

**Not part of the NuGet package.** Epic **E21** (packaging/CI) is deferred — these projects are `IsPackable=false`, live under `samples/`, and must never gate pack or default CI.

## Target framework

All samples target **.NET 10** only (`net10.0` / `net10.0-windows…` for MAUI).

## Solution entrypoint

```bash
dotnet build samples/ZVec.NET.Samples.slnx
```

Prefer this over the root solution when working on samples. Root `ZVec.NET.slnx` lists Shared/Console/AspNet optionally; MAUI stays in the samples solution so core CI does not need the MAUI workload.

## Apps

| Project | Role |
|---------|------|
| `ZVec.NET.Samples.Maui` | **Flagship** — Blazor Hybrid offline RAG (AppData + mmap) |
| `ZVec.NET.Samples.AspNet` | Minimal API + DI + health |
| `ZVec.NET.Samples.Console` | Typed + `ZVecDoc` vignette + CLI |
| `ZVec.NET.Samples.Shared` | Shared helpers (not a package) |

## Prerequisites

1. **.NET 10 SDK**
2. **Native library** for your RID (win-x64 today): copy `zvec_c_api.dll` into `src/Core/ZVec.NET/runtimes/win-x64/native/` (Appendix C in the Implementation Plan). Multi-RID bundling is E21.
3. **LM Studio** at `http://127.0.0.1:1234/v1` with **both** models loaded at once (no switching):
   - Embeddings: `text-embedding-google_embeddinggemma-300m-qat` (768-d EmbeddingGemma) → `POST /v1/embeddings`
   - Chat: `google/gemma-4-e2b` → `POST /v1/chat/completions`
4. **MAUI workload** (for the Maui project only): `dotnet workload install maui`

## Datasets (MB only, download on startup)

See [datasets/README.md](datasets/README.md).

- T0 fixtures are committed under `datasets/fixtures/`
- T1 packs download **async on sample startup** into gitignored `datasets/cache/`
- **Second startup skips** packs that are already ready (no re-download)
- Network failure leaves the T0 path working
- Hard cap ≈ 100 MB per pack — no GB corpora

## Quick smoke (win-x64, manual — no CI job)

- [ ] `dotnet run --project samples/ZVec.NET.Samples.Console -- basics`
- [ ] LM Studio up with **both** models → `ingest --fixtures` → `ask "What is ZVec.NET?"`
- [ ] First run downloads T1 into `datasets/cache/`; second run logs `skip …: already present`
- [ ] `dotnet run --project samples/ZVec.NET.Samples.AspNet` → `GET /health` healthy
- [ ] `POST /rag/ask` returns answer + hits
- [ ] MAUI Windows: ingest fixtures → ask → citations; restart app → doc count preserved

## E21 isolation

- No sample assets under NuGet `runtimes/`
- No GitHub Actions workflow for samples in E25
- After a package is published, samples may *document* `dotnet add package ZVec.NET` as an alternate to `ProjectReference`
- **No .NET Aspire** for LM Studio — use HTTP clients + health probes only
