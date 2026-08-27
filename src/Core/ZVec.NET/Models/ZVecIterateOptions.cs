namespace ZVec.NET;

/// <summary>
/// Options for full-collection document iteration via <see cref="IZvecCollectionQueries.Iterate"/>.
/// </summary>
/// <remarks>
/// The iterator takes a snapshot at creation time; writes and deletes after that are not visible.
/// Close the iterator before releasing the last collection handle. Schema DDL and optimize conflict
/// with open iterators on writable collections.
/// </remarks>
public sealed class ZVecIterateOptions
{
    /// <summary>
    /// Scalar fields to return. When null, all fields are returned (native default).
    /// An empty list returns only primary key / system columns.
    /// </summary>
    public IReadOnlyList<string>? OutputFields { get; init; }

    /// <summary>Whether to include vector fields in returned documents. Default is true.</summary>
    public bool IncludeVector { get; init; } = true;
}
