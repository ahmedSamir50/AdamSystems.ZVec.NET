# ZVec.NET.Samples.Console (.NET 10)

```bash
dotnet run --project samples/ZVec.NET.Samples.Console -- basics
dotnet run --project samples/ZVec.NET.Samples.Console -- ingest --fixtures
dotnet run --project samples/ZVec.NET.Samples.Console -- ask "What is ZVec.NET?"
dotnet run --project samples/ZVec.NET.Samples.Console -- search "mmap edge"
dotnet run --project samples/ZVec.NET.Samples.Console -- recommend "sci-fi mind-bending"
```

`basics` works without LM Studio (starts dataset download in the background). `ingest` / `search` / `recommend` await T1 ensure first (skip if already present).

RAG needs LM Studio with **both** models loaded concurrently:

- `text-embedding-google_embeddinggemma-300m-qat`
- `google/gemma-4-e2b`
