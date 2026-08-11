# Host apps (in-repo)

Official demos under [`samples/`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/samples). Target **.NET 10** only. Not packable; must not gate NuGet CI.

| Project | Role |
|---------|------|
| `ZVec.NET.Samples.Maui` | **Flagship** — Status + RAG + Search + Recommend (AppData + mmap) |
| `ZVec.NET.Samples.AspNet` | Minimal API parity (status, hints, models, seed, query, SSE) |
| `ZVec.NET.Samples.Console` | Interactive menu + CLI shortcuts |
| `ZVec.NET.Samples.Shared` | Shared helpers (not a demo by itself) |

Collections use SDK **`OpenOrCreate`** (restart-safe). Schema loads from on-disk metadata on reopen.

## Maui (flagship)

```bash
dotnet build samples/ZVec.NET.Samples.Maui -f net10.0-windows10.0.19041.0
dotnet build samples/ZVec.NET.Samples.Maui -t:Run -f net10.0-windows10.0.19041.0
```

| Nav | Purpose |
|-----|---------|
| **Status** | LM Studio probe, model dropdowns, dataset flags, three collection counts |
| **RAG** | Seed fixtures / FiQA · ask with chips + citations (SSE stream) |
| **Search** | Seed fixtures / NFCorpus / Quora · semantic query |
| **Recommend** | Seed T0 / MovieLens / Amazon · similar-items query |

Requires MAUI workload + win-x64 (or matching) `zvec_c_api`. Details: [samples/ZVec.NET.Samples.Maui/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/ZVec.NET.Samples.Maui/README.md).

## AspNet

```bash
dotnet run --project samples/ZVec.NET.Samples.AspNet
```

`GET /` lists endpoints. Highlights: `/status`, `/hints`, `/models`, `/rag/*` (including `/rag/ask/stream` SSE), `/search/*`, `/recommend/*`.

Details: [samples/ZVec.NET.Samples.AspNet/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/ZVec.NET.Samples.AspNet/README.md).

## Console

```bash
dotnet run --project samples/ZVec.NET.Samples.Console
# or: … -- status | rag seed-fixtures | ask | …
```

Commands: `status`, `models`, `basics`, `rag …`, `search …`, `recommend …`, `ask`, `help`, `quit`.

Details: [samples/ZVec.NET.Samples.Console/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/ZVec.NET.Samples.Console/README.md).

## See also

- [Scenarios](scenarios.md)
- [External demos & POCs](demos-and-pocs.md)
