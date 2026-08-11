# Contributing

Full contributor guide: [CONTRIBUTING.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/CONTRIBUTING.md) in the repository root.

## Docs workflow

```powershell
python -m pip install -r requirements-docs.txt
python -m mkdocs serve
```

Browse locally at `http://127.0.0.1:8000`. Public site (mike versions): [GitHub Pages](https://ahmedSamir50.github.io/AdamSystems.ZVec.NET/). See [docs/README.md](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/docs/README.md) for `gh-pages` / mike notes.

## After a native submodule bump

1. Re-fetch `docs/llms-full.txt` from https://zvec.org/llms-full.txt
2. Update [`llms-full.meta.md`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/blob/main/docs/llms-full.meta.md)
3. Regenerate [Native API coverage](reference/native-api-coverage.md) against `c_api.h` + `NativeMethods.cs`

AI Integration sections in the upstream dump remain **out of scope** for ZVec.NET.

## Engineering plans

Deep design / WBS live in repo markdown (`ZVec.NET-Project-Plan.md`) — not mirrored into this wiki.
