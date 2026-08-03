# API docs track

MkDocs hosts **conceptual** docs. Generated C# API reference is a second track.

## Current

- Public API uses `///` XML comments in `src/Core/ZVec.NET`
- `GenerateDocumentationFile` is enabled so builds emit `ZVec.NET.xml` (IntelliSense / future DocFX)

## Later (not blocking this site)

1. DocFX **or** XML→markdown export into `docs/reference/api/`
2. Wire generated pages into `mkdocs.yml` nav under Reference
3. Optionally version with `mike` alongside release tags

Until then, use this wiki + IDE IntelliSense + [Native API coverage](native-api-coverage.md) for binding reality.
