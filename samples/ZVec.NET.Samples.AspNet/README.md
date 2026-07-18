# ZVec.NET.Samples.AspNet (.NET 10)

```bash
dotnet run --project samples/ZVec.NET.Samples.AspNet
```

`GET /` lists endpoints. Highlights:

| Method | Path |
|--------|------|
| GET | `/health`, `/status`, `/hints`, `/models` |
| PUT | `/models` |
| POST | `/rag/ingest`, `/rag/seed-fixtures`, `/rag/seed-fiqa`, `/rag/query`, `/rag/ask` |
| POST | `/search/seed-fixtures`, `/search/seed-nfcorpus`, `/search/seed-quora`, `/search/query` |
| POST | `/recommend/seed-fixtures`, `/recommend/seed-movielens`, `/recommend/seed-amazon`, `/recommend/query` |

Collections use sample open-or-create (safe on restart). Background T1 download on startup. Configure LM Studio under `LmStudio` in `appsettings.json` (defaults can be overridden at runtime via `PUT /models`).
