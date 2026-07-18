# ZVec.NET.Samples.Maui (.NET 10) — flagship edge demo

```bash
dotnet build samples/ZVec.NET.Samples.Maui -f net10.0-windows10.0.19041.0
dotnet build samples/ZVec.NET.Samples.Maui -t:Run -f net10.0-windows10.0.19041.0
```

## Pages

| Nav | Purpose |
|-----|---------|
| **Status** | LM Studio probe, model dropdowns (`/v1/models`), dataset flags, three collection counts |
| **RAG** | Paste / seed fixtures / seed FiQA · ask with suggested chips + citations |
| **Search** | Seed fixtures / NFCorpus / Quora · semantic query |
| **Recommend** | Seed T0 / MovieLens / Amazon · similar-items query |

Collections live under `FileSystem.AppDataDirectory` with mmap and **open-or-create** (restart-safe). Errors show in-page banners / `ErrorBoundary` (not a blank WebView).

RAG Ask uses **SSE streaming** from LM Studio (`chat/completions` with `stream: true`).

**Note:** Seeding MovieLens may log EmbeddingGemma `tokenizer.ggml.add_eos_token` / SEP warnings in LM Studio — harmless; seed still completes. Optional silence via LM Studio/GGUF settings, not ZVec.NET.

Requires .NET 10 MAUI workload and win-x64 `zvec_c_api`.
