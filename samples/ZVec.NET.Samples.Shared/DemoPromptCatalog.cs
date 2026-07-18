namespace ZVec.NET.Samples.Shared;

public sealed record DemoPrompt(string Id, string Label, string Query, string CorpusHint);

/// <summary>Suggested queries + corpus blurbs so users can try demos without knowing pack contents.</summary>
public static class DemoPromptCatalog
{
    public const string RagBlurb =
        "T0: EN+AR product fixtures + Egyptian CS FAQ (eg_faq_dataset.csv). T1 FiQA: finance Q&A. " +
        "Citations are score-desc (best first) and near-duplicates are dropped before chat.";

    public const string SearchBlurb =
        "T0: sample ZVec/.NET questions. T1 NFCorpus: nutrition/medical abstracts. T1 Quora: question pairs.";

    public const string RecommendBlurb =
        "T0: classic films. T1 MovieLens: movie titles/genres. T1 Amazon Beauty: product titles.";

    public static IReadOnlyList<DemoPrompt> Rag { get; } =
    [
        new("rag-zvec", "What is ZVec.NET?", "What is ZVec.NET?", "fixtures"),
        new("rag-offline", "Offline RAG", "How does offline RAG work on edge devices?", "fixtures"),
        new("rag-ar-zvec", "ما هو ZVec.NET؟", "ما هو ZVec.NET؟", "fixtures-ar"),
        new("rag-ar-rag", "RAG دون اتصال", "كيف يعمل RAG دون اتصال على الأجهزة الطرفية؟", "fixtures-ar"),
        new("rag-eg-order", "اوردر متأخر", "انا عملت اوردر من كام يوم ومش واصل، اعمل ايه؟", "eg-faq"),
        new("rag-eg-fiber", "نت فاصل", "النت الفايبر عندي فاصل، ممكن تساعدوني؟", "eg-faq"),
        new("rag-eg-warranty", "ضمان جهاز", "الثلاجة لسه في الضمان وبتعمل صوت غريب، أعمل إيه؟", "eg-faq"),
        new("rag-fiqa", "Investors / company", "How do investors evaluate a company?", "fiqa")
    ];

    public static IReadOnlyList<DemoPrompt> Search { get; } =
    [
        new("s-open", "Open collection", "How do I open a ZVec collection in .NET?", "fixtures"),
        new("s-dispose", "Dispose vs Destroy", "What is the difference between Dispose and Destroy?", "fixtures"),
        new("s-odm", "ODM attributes", "How do typed ODM attributes map to schema?", "fixtures"),
        new("s-nf", "Vitamin D", "vitamin D deficiency symptoms", "nfcorpus"),
        new("s-quora", "Learn programming", "how to learn programming", "quora")
    ];

    public static IReadOnlyList<DemoPrompt> Recommend { get; } =
    [
        new("r-scifi", "Sci-fi mind-bending", "sci-fi mind-bending", "fixtures"),
        new("r-anime", "Animated fantasy", "animated fantasy like Spirited Away", "fixtures"),
        new("r-crime", "Crime drama", "crime drama", "fixtures"),
        new("r-beauty", "Face cream", "moisturizing face cream", "amazon")
    ];

    public static object ToHintsResponse() => new
    {
        ragBlurb = RagBlurb,
        searchBlurb = SearchBlurb,
        recommendBlurb = RecommendBlurb,
        rag = Rag.Select(p => new { id = p.Id, label = p.Label, query = p.Query, corpus = p.CorpusHint }),
        search = Search.Select(p => new { id = p.Id, label = p.Label, query = p.Query, corpus = p.CorpusHint }),
        recommend = Recommend.Select(p => new { id = p.Id, label = p.Label, query = p.Query, corpus = p.CorpusHint })
    };
}
