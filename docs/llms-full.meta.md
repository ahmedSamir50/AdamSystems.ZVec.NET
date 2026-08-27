# llms-full.txt metadata

| Field | Value |
|-------|--------|
| **Source** | https://zvec.org/llms-full.txt |
| **Fetched (UTC)** | 2026-08-27 |
| **Intended native pin** | alibaba/zvec **v0.7.0** (ZVec.NET `1.0.0-beta.6`) |
| **Local path** | `docs/llms-full.txt` (gitignored — re-fetch locally) |
| **Site nav** | Not published (audit artifact only) |

## Refresh

```powershell
Invoke-WebRequest -Uri "https://zvec.org/llms-full.txt" -OutFile "docs/llms-full.txt" -UseBasicParsing
```

Update this file’s **Fetched** date whenever the dump is refreshed. Re-fetch on every native submodule bump together with `reference/native-api-coverage.md`.

AI Integration sections in the dump (embeddings, MCP, skills, model rerankers) are **out of scope** for ZVec.NET.
