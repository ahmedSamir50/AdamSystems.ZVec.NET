using System.Runtime.InteropServices;

namespace ZVec.NET.Internal;

/// <summary>
/// Enforces upstream ZVec platform constraints for index types before native calls.
/// </summary>
internal static class ZVecPlatformRequirements
{
    /// <summary>
    /// Throws <see cref="PlatformNotSupportedException"/> when <paramref name="param"/>
    /// is not supported on the current OS/architecture.
    /// </summary>
    /// <remarks>
    /// HNSW-RaBitQ is supported only on x86_64 with AVX2 or higher (not ARM).
    /// DiskANN is supported on Linux only and requires libaio.
    /// </remarks>
    public static void ThrowIfUnsupported(ZVecIndexParam param)
    {
        ArgumentNullException.ThrowIfNull(param);

        switch (param)
        {
            case ZVecHnswRabitqIndexParam:
                if (RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64)
                {
                    throw new PlatformNotSupportedException(
                        ZVecDefaults.Errors.RabitqRequiresX64Avx2);
                }
                break;

            case ZVecDiskAnnIndexParam:
                if (!OperatingSystem.IsLinux())
                {
                    throw new PlatformNotSupportedException(
                        ZVecDefaults.Errors.DiskAnnRequiresLinuxLibaio);
                }
                break;
        }
    }
}
