# FTS

Full-text search over string fields (tokenizers such as Jieba where configured upstream).

## When to use

- Keyword / lexical retrieval alongside dense vectors
- Hybrid RAG patterns (dense + FTS fusion)

## ZVec.NET mapping

| Concern | Type / API |
|---------|------------|
| Index params | `ZVecFtsIndexParam` |
| Query | `ZVecFtsQuery` on `ZVecQuery.Fts` |
| Fusion | Combine with vector sub-queries + [RRF / weighted](rrf.md) |

## Pitfalls

- Embeddings and chat models are **host/sample** concerns — not part of the NuGet DB SDK
- Tokenizer/language behavior is defined by upstream ZVec

## Upstream

- [zvec.org](https://zvec.org)
- [.NET hybrid guide](../guides/hybrid-fts.md)
