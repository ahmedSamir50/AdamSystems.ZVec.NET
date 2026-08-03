# RAG, Search, and Recommend

All three in-repo hosts share the same product scenarios. Embeddings and chat come from **LM Studio** (host concern); ZVec.NET stores and queries vectors.

## RAG

1. Seed / ingest documents (fixtures, EG FAQ CSV, optional FiQA)
2. Retrieve by dense vector (+ citations, score-descending, near-dup dedupe)
3. Ask with chat — Maui/AspNet prefer **SSE streaming** (`POST /rag/ask/stream`)

Suggested prompts: `DemoPromptCatalog` chips (EN + Arabic product + Egyptian CS FAQ). AspNet: `GET /hints`.

## Search

Semantic search over seeded corpora:

- Fixtures
- NFCorpus / Quora (download packs into gitignored `samples/datasets/cache/` on startup when used)

## Recommend

Similar-item style queries over:

- T0 fixtures
- MovieLens / Amazon Beauty (mass-embed; EmbeddingGemma EOS/SEP warnings in LM Studio are non-fatal)

## Datasets

See [samples/datasets/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/datasets/README.md). Seed via Maui buttons, Console commands, or AspNet POSTs (capped).

## Restart-safe collections

Samples use **`factory.OpenOrCreate`**. Paths live under AppData (Maui) or temp (Console/AspNet). If an old folder looks empty or broken after an upgrade, delete the `zvec-samples-*` folders once and re-seed.

## Smoke checklist

- [ ] Console → `status` → `models`
- [ ] `rag seed-fixtures` → `ask` (EN or Arabic chip)
- [ ] `rag seed-eg-faq` → `ask` (Egyptian CS chip)
- [ ] `search seed-fixtures` → `search`
- [ ] `recommend seed-fixtures` → `recommend`
- [ ] AspNet: `GET /status`, `GET /hints`, seed + query POSTs
- [ ] Maui: restart twice — no CreateAndOpen crash; Status shows three doc counts

Full notes: [samples/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/samples/README.md).
