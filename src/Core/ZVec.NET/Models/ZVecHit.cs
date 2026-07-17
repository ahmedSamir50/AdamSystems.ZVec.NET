namespace ZVec.NET;

/// <summary>
/// A typed query hit: the mapped record plus the native relevance score.
/// </summary>
/// <typeparam name="T">Mapped document type.</typeparam>
public sealed class ZVecHit<T> where T : class
{
    /// <summary>Mapped document.</summary>
    public required T Record { get; init; }

    /// <summary>Score from the vector / hybrid search.</summary>
    public float Score { get; init; }
}
