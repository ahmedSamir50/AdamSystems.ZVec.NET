namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Normalized text row produced by MB-capped dataset loaders.</summary>
public sealed record TextSource(string Id, string Title, string Body, string Source, string Tags = "");
