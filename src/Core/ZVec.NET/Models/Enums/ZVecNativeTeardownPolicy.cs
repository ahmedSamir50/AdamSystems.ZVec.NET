namespace ZVec.NET;

/// <summary>
/// Controls whether native <c>zvec_collection_close</c> / <c>zvec_shutdown</c> are invoked on dispose.
/// </summary>
/// <remarks>
/// Temporary workaround for upstream Linux SIGSEGV on teardown
/// (<see href="https://github.com/alibaba/zvec/issues/619">alibaba/zvec#619</see>).
/// When that issue is fixed and verified with <see cref="AlwaysCall"/> on linux-x64 (exit 0),
/// remove this policy and always call native close/shutdown.
/// </remarks>
public enum ZVecNativeTeardownPolicy
{
    /// <summary>
    /// Suppress native close/shutdown on Linux; call them on other OSes.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Always invoke native close/shutdown (use when debugging #619 or after upstream fix).
    /// </summary>
    AlwaysCall = 1,

    /// <summary>
    /// Never invoke native close/shutdown (abandon handles; OS reclaims on process exit).
    /// </summary>
    Suppress = 2
}
