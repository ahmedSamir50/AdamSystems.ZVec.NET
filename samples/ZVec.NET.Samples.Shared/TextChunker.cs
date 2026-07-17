namespace ZVec.NET.Samples.Shared;

/// <summary>Deterministic character-based chunker with overlap (no ML).</summary>
public sealed class TextChunker
{
    private readonly int _maxChars;
    private readonly int _overlap;

    public TextChunker(int maxChars = SampleDefaults.ChunkMaxChars, int overlap = SampleDefaults.ChunkOverlapChars)
    {
        if (maxChars <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChars));
        if (overlap < 0 || overlap >= maxChars)
            throw new ArgumentOutOfRangeException(nameof(overlap));

        _maxChars = maxChars;
        _overlap = overlap;
    }

    public IReadOnlyList<string> Chunk(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (normalized.Length <= _maxChars)
            return [normalized];

        var chunks = new List<string>();
        var step = _maxChars - _overlap;
        for (var i = 0; i < normalized.Length; i += step)
        {
            var len = Math.Min(_maxChars, normalized.Length - i);
            chunks.Add(normalized.Substring(i, len).Trim());
            if (i + len >= normalized.Length)
                break;
        }

        return chunks.Where(static c => c.Length > 0).ToArray();
    }
}
