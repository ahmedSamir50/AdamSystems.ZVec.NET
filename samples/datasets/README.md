# Sample datasets (MB only)

ZVec.NET samples use **MB-sized** corpora only. **No GB datasets** — not for demos and not later.

| Budget | Limit |
|--------|--------|
| Per downloaded pack | ≤ ~100 MB (`SampleDefaults.MaxPackBytes`) |
| In-repo fixtures (T0) | ≤ ~2 MB under `fixtures/` |

Forbidden: MS MARCO / TREC RAG, HotpotQA full, Amazon Books full, MovieLens 25M/32M.

## Layout

```text
datasets/
  fixtures/     # committed T0 (always works offline)
  cache/        # gitignored T1 downloads (created on sample startup)
```

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
| `fixtures/rag/*.md` | Offline RAG ingest |
| `fixtures/search/questions.json` | Semantic search |
| `fixtures/recommend/items.json` | Similar-item recommend |

## Attribution

| Folder | Attribution |
|--------|-------------|
| `cache/fiqa/` | [BeIR/fiqa](https://huggingface.co/datasets/BeIR/fiqa) / UKP BEIR |
| `cache/nfcorpus/` | [BeIR/nfcorpus](https://huggingface.co/datasets/BeIR/nfcorpus) / UKP BEIR |
| `cache/quora/` | Quora 2017 research terms |
| `cache/movielens-small/` | [GroupLens MovieLens](https://grouplens.org/datasets/movielens/) |
| `cache/amazon-beauty/` | [Amazon Reviews 2023](https://amazon-reviews-2023.github.io/) All_Beauty meta |

These files are **never** packed into the ZVec.NET NuGet (E21).
