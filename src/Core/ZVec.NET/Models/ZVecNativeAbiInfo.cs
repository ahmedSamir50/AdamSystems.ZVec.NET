namespace ZVec.NET;

/// <summary>
/// Describes the SDK's required native ABI and the loaded native library version, when available.
/// </summary>
public sealed record ZVecNativeAbiInfo(
    string RequiredMinimumVersion,
    int RequiredMajor,
    string? FoundVersion,
    int? FoundMajor,
    bool IsCompatible);
