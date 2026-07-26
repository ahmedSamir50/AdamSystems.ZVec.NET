namespace ZVec.NET.DependencyInjection;

/// <summary>
/// How <see cref="ZVecServiceCollectionExtensions.AddZVecCollection"/> opens a collection path.
/// </summary>
public enum ZVecCollectionOpenMode
{
    /// <summary>
    /// Create a new collection; fails if the path already exists (upstream <c>CreateAndOpen</c>).
    /// </summary>
    CreateOnly = 0,

    /// <summary>
    /// Open an existing collection path only.
    /// </summary>
    OpenOnly = 1,

    /// <summary>
    /// Open if the path has content; otherwise create. Restart-safe default for DI hosts.
    /// </summary>
    OpenOrCreate = 2
}
