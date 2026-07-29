# Docs snapshots

| File | Source | Purpose |
|------|--------|---------|
| [llms-full.txt](llms-full.txt) | https://zvec.org/llms-full.txt | Full upstream doc dump for DB coverage audits (see project plan §2.0) |
| [native-api-coverage.md](native-api-coverage.md) | Generated against `c_api.h` @ **zvec v0.6.0** and `NativeMethods.cs` | Binding coverage / missing / orphans |

Re-fetch product docs when re-auditing:

```powershell
Invoke-WebRequest -Uri "https://zvec.org/llms-full.txt" -OutFile "docs/llms-full.txt" -UseBasicParsing
```

AI Integration sections in that file are **out of scope** for ZVec.NET (embeddings, MCP, skills, model rerankers).

Coverage report pin: regenerate after each native submodule bump (current: **v0.6.0** / ZVec.NET `1.0.0-beta.4`).
