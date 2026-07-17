# ZVec.NET overview

ZVec.NET is a .NET SDK for Alibaba ZVec, an embedded vector database often described as the SQLite of vector DBs.

It supports HNSW and other ANN indexes, typed ODM with ZVec.NET.Mapping attributes, and DI registration via AddZVec and AddZVecCollection.

Collections store dense embeddings as ReadOnlyMemory float vectors. Dispose closes a collection; Destroy deletes on-disk data.

Edge and offline apps can keep data under AppData with memory-mapped I/O enabled for local RAG and semantic search.
