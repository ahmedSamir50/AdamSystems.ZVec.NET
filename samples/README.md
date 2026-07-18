# ZVec.NET Samples (.NET 10 only)

User demos for offline/edge RAG, semantic search, and recommendations.

**Not part of the NuGet package.** Epic **E21** (packaging/CI) is deferred — these projects are `IsPackable=false`, live under `samples/`, and must never gate pack or default CI.

## Target framework

All samples target **.NET 10** only (`net10.0` / `net10.0-windows…` for MAUI).

## Solution entrypoint

```bash
dotnet build samples/ZVec.NET.Samples.slnx
```

## Apps

| Project | Role |
|---------|------|
| `ZVec.NET.Samples.Maui` | **Flagship** — Status + RAG + Search + Recommend (AppData + mmap) |
| `ZVec.NET.Samples.AspNet` | Minimal API parity (status, hints, models, seed, query) |
| `ZVec.NET.Samples.Console` | Interactive menu (no args) + CLI shortcuts |
| `ZVec.NET.Samples.Shared` | Shared helpers (not a package) |

## Prerequisites

1. **.NET 10 SDK**
2. **Native library** for your RID (win-x64): `zvec_c_api` under `src/Core/ZVec.NET/runtimes/win-x64/native/`
3. **LM Studio** at `http://127.0.0.1:1234/v1` with both models loaded (concurrent):
   - Default embed: `text-embedding-google_embeddinggemma-300m-qat` (768-d)
   - Default chat: `google/gemma-4-e2b`
   - Change selection via Maui Status, Console `models`, or `GET/PUT /models`
4. **MAUI workload** (Maui only): `dotnet workload install maui`

## Collections (restart-safe)

Samples use **app-level open-or-create** (upstream `CreateAndOpen` throws if the path exists — same as Python/Node). Second launch opens existing collections under AppData / temp.

## Datasets

See [datasets/README.md](datasets/README.md). T1 packs download on startup into gitignored `cache/` (skip if present). Seed into ZVec via Maui buttons / Console / AspNet seed endpoints (capped).

## Suggested queries

`DemoPromptCatalog` provides chips / numbered hints so you can try demos without knowing corpus contents. AspNet: `GET /hints`.

## Quick smoke

- [ ] `dotnet run --project samples/ZVec.NET.Samples.Console` → interactive menu → `status` → `models`
- [ ] `rag seed-fixtures` → `ask` (pick a suggested #)
- [ ] `search seed-fixtures` → `search` query
- [ ] `recommend seed-fixtures` → `recommend` query
- [ ] AspNet: `GET /status`, `GET /hints`, `GET /models`, seed + query POSTs
- [ ] Maui: restart twice — no CreateAndOpen crash; Status shows three doc counts

## E21 isolation

- No sample assets under NuGet `runtimes/`
- No Aspire for LM Studio
- No GitHub Actions workflow for samples in E25
