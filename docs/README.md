# ZVec.NET documentation

Human- and AI-friendly wiki built with **MkDocs Material** + **mike** versioning, published to GitHub Pages.

| | URL |
|---|-----|
| **Public site** | https://ahmedSamir50.github.io/AdamSystems.ZVec.NET/ |
| **Aliases** | `latest` (release tags), `dev` (tip of `main`) |
| **Local preview** | `python -m mkdocs serve` → http://127.0.0.1:8000 |
| **Agent map** | [llms.txt](llms.txt) (copied to each version’s site root on build) |
| **Logo** | [assets/zvec-net-logo.png](assets/zvec-net-logo.png) |

## Local development

On Windows, prefer `python -m …` (avoids stale `pip.exe` launchers):

```powershell
cd ..   # repo root
python -m pip install -r requirements-docs.txt
python -m mkdocs serve
# or: python -m mkdocs build --strict
```

Maintainer-only (versioned publish locally):

```powershell
python -m mike deploy --update-aliases 1.0.0-beta.4 latest
python -m mike set-default latest
python -m mike serve
```

Do **not** include `+zvec.*` in the mike version id. Tag `v1.0.0-beta.4` → version `1.0.0-beta.4`.

## GitHub Pages (mike / gh-pages)

Workflow [`.github/workflows/docs.yml`](../.github/workflows/docs.yml):

| Trigger | mike version | Aliases |
|---------|--------------|---------|
| Push to `main` | `dev` | — |
| Tag `v*` | tag without `v` | `latest` + set-default |
| `workflow_dispatch` | input or inferred | optional `latest` |

**One-time repo setting:** Settings → Pages → **Deploy from a branch** → branch **`gh-pages`** / folder `/ (root)`.  
(Switch away from “GitHub Actions” artifact deploy once mike is used — mike pushes HTML to `gh-pages`.)

This is a **project** Pages site (`/AdamSystems.ZVec.NET/`). It does not replace a user profile site at `https://ahmedSamir50.github.io/`.

### First publish after wiki lands

From CI (`workflow_dispatch` with version `1.0.0-beta.4` and `update_latest=true`) or locally with push access:

```powershell
python -m mike deploy --push --update-aliases 1.0.0-beta.4 latest
python -m mike set-default --push latest
```

## Audit artifacts (not in site nav)

| File | Purpose |
|------|---------|
| [llms-full.txt](llms-full.txt) | Upstream dump from zvec.org (gitignored; re-fetch locally) |
| [llms-full.meta.md](llms-full.meta.md) | Fetch date / pin |
| [reference/native-api-coverage.md](reference/native-api-coverage.md) | C API vs `NativeMethods.cs` (also in site Reference) |

```powershell
Invoke-WebRequest -Uri "https://zvec.org/llms-full.txt" -OutFile "llms-full.txt" -UseBasicParsing
```

AI Integration sections in that file are **out of scope** for ZVec.NET.
