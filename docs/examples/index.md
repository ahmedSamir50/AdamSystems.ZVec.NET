# Examples overview

ZVec.NET demos come in **two layers**. Neither is inside the NuGet package — they show how to **host** the SDK (embeddings/chat stay in the host).

| Layer | Where | Role |
|-------|--------|------|
| **Official host demos** | [`samples/`](https://github.com/ahmedSamir50/AdamSystems.ZVec.NET/tree/main/samples) in this repo | MAUI / ASP.NET / Console — RAG, Search, Recommend |
| **Demos & POCs** | [ZVec.Net-DemosAndPOCs](https://github.com/ahmedSamir50/ZVec.Net-DemosAndPOCs) | Advanced / alternate apps (Jira RAG, CLIP gallery, MovieLens MAUI) |
| **Inline snippets** | This wiki’s guides | Short copy-paste C# for API learning |

## Scope

| In the SDK (NuGet) | In samples / demos |
|--------------------|--------------------|
| Collections, indexes, query, DI, ODM | LM Studio / ONNX / MiniLM embeddings |
| SafeHandles, RIDs, filters, rerankers | Chat, Aspire, Docker, UI frameworks |

## In this section

1. [Host apps (in-repo)](hosts.md) — Maui, AspNet, Console
2. [RAG / Search / Recommend](scenarios.md) — shared scenarios + smoke checklist
3. [Demos and POCs (external)](demos-and-pocs.md) — separate repository
4. [Inline snippets](snippets.md) — guide → code map

## Prerequisites (in-repo samples)

- **.NET 10 SDK**
- Native `zvec_c_api` for your RID under `src/Core/ZVec.NET/runtimes/{rid}/native/`
- **LM Studio** at `http://127.0.0.1:1234/v1` (embed + chat models) for RAG/ask flows
- MAUI workload for the Maui host

```bash
dotnet build samples/ZVec.NET.Samples.slnx
```
