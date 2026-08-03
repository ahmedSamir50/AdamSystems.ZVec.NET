# AGENTS.md — navigating ZVec.NET

This repository is the **ZVec.NET** managed SDK (PackageId `ZVec.NET`) wrapping Alibaba ZVec’s official `zvec_c_api`.

## Read first

1. [docs/llms.txt](docs/llms.txt) — curated agent map (also published on GitHub Pages site root)
2. [docs/index.md](docs/index.md) — wiki home (branded)
3. [docs/examples/index.md](docs/examples/index.md) — in-repo samples + external demos
4. [CONTRIBUTING.md](CONTRIBUTING.md) — branch topology, native build, docs/mike
5. [docs/reference/native-api-coverage.md](docs/reference/native-api-coverage.md) — C API binding reality

## Documentation

| Surface | Role |
|---------|------|
| MkDocs wiki under `docs/` | Human + AI searchable guides / examples / theory |
| Public site | https://ahmedSamir50.github.io/AdamSystems.ZVec.NET/ (mike versions: `latest`, `dev`) |
| Branding | `docs/assets/zvec-net-logo.png` (source `assets/zvec-net-logo.png`) |
| `docs/llms-full.txt` | Upstream zvec.org dump (gitignored; see `docs/llms-full.meta.md`) |
| External demos | https://github.com/ahmedSamir50/ZVec.Net-DemosAndPOCs — link only, do not vendor |
| `ZVec.NET-Project-Plan.md` | Engineering design / WBS — not the user wiki |

Local preview: `python -m pip install -r requirements-docs.txt` then `python -m mkdocs serve`.

## Code map

| Path | Contents |
|------|----------|
| `src/Core/ZVec.NET/` | Published assembly — DI, Mapping, builders, P/Invoke |
| `src/Native/ZVec.Native/` | CMake wrapper + `external/zvec` submodule |
| `testing/` | xUnit + BenchmarkDotNet |
| `samples/` | Official ASP.NET / MAUI / Console hosts (.NET 10) |
| `build/ci/` | Pack/RID scripts and patches |

## Hard rules for agents

- Prefer typed ODM (`IZvecCollection<T>`, `ZVec.NET.Mapping`) in samples and docs.
- Do not invent C API symbols; check `c_api.h` and coverage report.
- AI Integration (embeddings, MCP, model rerankers) is **out of scope** for this package.
- After native submodule bumps: refresh `llms-full.txt` + regenerate native-api-coverage.
- Do not commit `docs/llms-full.txt` or MkDocs `site/` output.
- Do not submodule `ZVec.Net-DemosAndPOCs` into this repo; wiki links out.
