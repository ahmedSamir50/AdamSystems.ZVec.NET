namespace ZVec.NET;

/// <summary>
/// Controls whether native <c>zvec_collection_close</c> / <c>zvec_shutdown</c> are invoked on dispose.
/// </summary>
public enum ZVecNativeTeardownPolicy
{
    /// <summary>
    /// Call native close/shutdown on all platforms (default).
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Same as <see cref="Auto"/> — always invoke native close/shutdown.
    /// </summary>
    AlwaysCall = 1,

    /// <summary>
    /// Never invoke native close/shutdown (abandon handles; OS reclaims on process exit).
    /// </summary>
    Suppress = 2
}
