namespace ZVec.NET;

/// <summary>
/// Minimum supported native ZVec ABI and major-compatibility rules for the version gate.
/// </summary>
/// <remarks>
/// At factory initialization the SDK requires
/// <c>zvec_check_version(MinimumMajor, MinimumMinor, MinimumPatch)</c> (native ≥ minimum)
/// and <c>zvec_get_version_major() == MinimumMajor</c> (same major).
/// </remarks>
public static class ZVecNativeAbi
{
    /// <summary>Minimum accepted native major version.</summary>
    public const int MinimumMajor = 0;

    /// <summary>Minimum accepted native minor version.</summary>
    public const int MinimumMinor = 5;

    /// <summary>Minimum accepted native patch version.</summary>
    public const int MinimumPatch = 1;

    /// <summary>Minimum accepted native version as <c>major.minor.patch</c>.</summary>
    public static string MinimumVersionString =>
        $"{MinimumMajor}.{MinimumMinor}.{MinimumPatch}";

    /// <summary>
    /// Returns <c>true</c> when the native library meets the minimum SemVer floor
    /// and shares the required major version.
    /// </summary>
    public static bool IsCompatible(bool meetsMinimumVersion, int foundMajor, int requiredMajor)
        => meetsMinimumVersion && foundMajor == requiredMajor;
}
