# Docs snapshots

| File | Source | Purpose |
|------|--------|---------|
| [llms-full.txt](llms-full.txt) | https://zvec.org/llms-full.txt | Full upstream doc dump for DB coverage audits (see project plan §2.0) |

Re-fetch when re-auditing:

```powershell
Invoke-WebRequest -Uri "https://zvec.org/llms-full.txt" -OutFile "docs/llms-full.txt" -UseBasicParsing
```

AI Integration sections in that file are **out of scope** for ZVec.NET (embeddings, MCP, skills, model rerankers).
