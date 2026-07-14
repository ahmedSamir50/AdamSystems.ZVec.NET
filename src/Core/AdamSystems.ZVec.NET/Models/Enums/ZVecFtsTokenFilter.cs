namespace AdamSystems.ZVec.NET;

/// <summary>
/// FTS token filter name. Maps to native string literals
/// (<c>"lowercase"</c>, <c>"ascii_folding"</c>, <c>"stemmer"</c>).
/// </summary>
public enum ZVecFtsTokenFilter
{
    Lowercase = 0,
    AsciiFolding = 1,
    Stemmer = 2
}
