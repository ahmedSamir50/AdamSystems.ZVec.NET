# Sample datasets (MB only)

ZVec.NET samples use **MB-sized** corpora only. **No GB datasets** — not for demos and not later.

| Budget | Limit |
|--------|--------|
| Per downloaded pack | ≤ ~100 MB (`SampleDefaults.MaxPackBytes`) |
| In-repo fixtures (T0) | ≤ ~3 MB under `fixtures/` (includes Egyptian FAQ CSV ~0.6 MB) |

Forbidden: MS MARCO / TREC RAG, HotpotQA full, Amazon Books full, MovieLens 25M/32M.

## Layout

```text
datasets/
  fixtures/     # committed T0 (always works offline)
  cache/        # gitignored T1 downloads (created on sample startup)
```

`samples/datasets/.gitignore` ignores downloaded `*.csv` / `*.jsonl` at the cache level; committed data must live under `fixtures/**`.

## Download on startup (not in git)

T1 packs are **not committed**. Console / AspNet / Maui call `SampleDatasetBootstrap` / `DatasetDownloader` on startup:

- **Async** — apps stay usable with T0 fixtures while packs arrive
- **Skip if already present** — if a pack’s ready files (and `.ready` marker) exist, logs `skip {pack}: already present` and **no HTTP GET**
- **MB hard cap** — reject packs over ~100 MB
- **Atomic** — download to `*.tmp`, then unzip/move, then write `.ready`; delete temp on failure

### URLs (encoded in `DatasetDownloadUrls`)

| Pack | URL | Approx size |
|------|-----|-------------|
| FiQA | `https://public.ukp.informatik.tu-darmstadt.de/thakur/BEIR/datasets/fiqa.zip` | ~17 MB |
| NFCorpus | `https://public.ukp.informatik.tu-darmstadt.de/thakur/BEIR/datasets/nfcorpus.zip` | ~2.4 MB |
| MovieLens small | `https://files.grouplens.org/datasets/movielens/ml-latest-small.zip` | ~1 MB |
| Quora QQP | Hugging Face `aisuko/quora_duplicate_questions` TSV | ~58 MB |
| Amazon Beauty | `https://mcauleylab.ucsd.edu/public_datasets/data/amazon_2023/raw/meta_categories/meta_All_Beauty.jsonl.gz` (stream → capped JSONL ≤25k / ≤100 MB) | capped |

Unpacked under `cache/fiqa/`, `cache/nfcorpus/`, `cache/movielens-small/`, `cache/quora/`, `cache/amazon-beauty/`.

### Offline / network failure

T0 fixtures under `fixtures/` still work. Status/logs explain download failures; loaders can re-try ensure on first use.

## T0 fixtures (committed)

| Path | Use |
|------|-----|
| `fixtures/rag/*.md` | Offline RAG ingest (EN + Arabic product docs) |
| `fixtures/rag/eg_faq_dataset.csv` | Egyptian Arabic customer-service FAQ (2000 Q&A rows) |
| `fixtures/search/questions.json` | Semantic search |
| `fixtures/recommend/items.json` | Similar-item recommend |

### Egyptian FAQ CSV (`eg_faq_dataset.csv`)

Synthetic Egyptian colloquial Q&A for sales, real estate, telecom/internet, and electronics warranty demos.

| Column | Ingest role |
|--------|-------------|
| `id` | Stable ZVec id `eg-cs-{id}` (re-seed upserts, does not clone) |
| `question_ar` | `RagDocument.Title` |
| `answer_ar` | `RagDocument.ChunkText` |
| `sector` / `category` | `Tags` only (not required for retrieval) |

Seed via Maui **Seed Arabic EG FAQ**, Console `rag seed-eg-faq`, or AspNet `POST /rag/seed-eg-faq`.

Fictional brands/policies — not official OEM or carrier data.

## RAG citations

- Ordered **most relevant first** (highest similarity score).
- Near-duplicate chunks are **deduped** after over-fetch so chat does not cite the same text as `[1]`, `[2]`, `[3]`.
- Fixture / FAQ seeds use **stable ids**. If an older collection was polluted with timestamp clones, delete the RAG folder once (`AppData/…/zvec-samples-rag` or `%TEMP%\ZVec.NET.Samples\zvec-samples-rag`) and seed again.

## Attribution

| Folder | Attribution |
|--------|-------------|
| `fixtures/rag/eg_faq_dataset.csv` | Synthetic sample corpus (Egyptian CS FAQ) |
| `cache/fiqa/` | [BeIR/fiqa](https://huggingface.co/datasets/BeIR/fiqa) / UKP BEIR |
| `cache/nfcorpus/` | [BeIR/nfcorpus](https://huggingface.co/datasets/BeIR/nfcorpus) / UKP BEIR |
| `cache/quora/` | Quora 2017 research terms |
| `cache/movielens-small/` | [GroupLens MovieLens](https://grouplens.org/datasets/movielens/) |
| `cache/amazon-beauty/` | [Amazon Reviews 2023](https://amazon-reviews-2023.github.io/) All_Beauty meta |

These files are **never** packed into the ZVec.NET NuGet (E21).
