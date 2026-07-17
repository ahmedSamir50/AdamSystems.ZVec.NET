# ZVec.NET.Samples.AspNet (.NET 10)

```bash
dotnet run --project samples/ZVec.NET.Samples.AspNet
```

Endpoints: `/health`, `POST /rag/ingest`, `/rag/query`, `/rag/ask`, `/search`, `/recommend`, `/recommend/seed-fixtures`.

On startup, `DatasetDownloadHostedService` downloads T1 packs into `samples/datasets/cache/` without blocking listen (skips packs already present).

Configure LM Studio under `LmStudio` in `appsettings.json` (EmbeddingGemma + `google/gemma-4-e2b`). Host lifetime shuts down the ZVec factory on stop.
