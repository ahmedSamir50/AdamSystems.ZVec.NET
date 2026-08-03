# Demos and POCs (external)

Advanced and alternate demos live in a **separate repository** — not inside `AdamSystems.ZVec.NET` and not in the NuGet package.

**Repo:** [ahmedSamir50/ZVec.Net-DemosAndPOCs](https://github.com/ahmedSamir50/ZVec.Net-DemosAndPOCs)

## Requirements

- **ZVec.NET 1.0.0-beta.4** (`+zvec.0.6.0`) from NuGet
- Collections use SDK **`OpenOrCreate`** / DI `OpenMode = OpenOrCreate` (restart-safe)
- Dense-vector **FP32 HNSW** focus in those demos; group-by remains blocked in the .NET C API; INT8/INT4 `EnableRotate` exists in the SDK but is unused there

If an on-disk collection fails to open after a native bump, use each demo’s **Reset → Ingest** path.

## Projects

| Path | What it shows |
|------|----------------|
| [`Advanced/`](https://github.com/ahmedSamir50/ZVec.Net-DemosAndPOCs/tree/main/Advanced) | **PDDM** — Projects Docs Deep Mind (Jira RAG navigator with Aspire + Docker) |
| [`examples/01-clip-onnx`](https://github.com/ahmedSamir50/ZVec.Net-DemosAndPOCs/tree/main/examples/01-clip-onnx) | **CLIP ONNX gallery** — Flickr8k vision embeddings in ZVec; text or image query |
| [`examples/02-movie-recs`](https://github.com/ahmedSamir50/ZVec.Net-DemosAndPOCs/tree/main/examples/02-movie-recs) | **MovieLens recs** — MAUI Blazor Hybrid + MudBlazor; MiniLM + ZVec on Windows/Android |

Talk track / session deck in that repo: `docs/ZVec.NET_Team_Session.html`.

## vs in-repo `samples/`

| | This SDK repo `samples/` | ZVec.Net-DemosAndPOCs |
|--|--------------------------|------------------------|
| Purpose | Official host parity (Maui / AspNet / Console) | Extra POCs, vision, Aspire/Jira, talk demos |
| Shipped with SDK source | Yes | No (link only) |
| Maintained for | SDK consumers learning hosts | Broader demos / sessions |

Clone the demos repo separately; do not expect it as a submodule of `AdamSystems.ZVec.NET`.
