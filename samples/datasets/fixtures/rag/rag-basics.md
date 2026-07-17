# Offline RAG with ZVec.NET

Retrieval-augmented generation (RAG) chunks documents, embeds each chunk with a local model, stores vectors in ZVec, then retrieves top-K chunks for a question.

These samples use LM Studio on localhost for embeddings and Gemma 4 for chat completions. No cloud API keys are required.

If the chat model is unavailable, the sample still returns citations from ZVec so you can verify retrieval independently.
