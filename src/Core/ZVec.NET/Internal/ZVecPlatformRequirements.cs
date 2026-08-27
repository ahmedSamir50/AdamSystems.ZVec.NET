using System.Runtime.InteropServices;

namespace ZVec.NET.Internal;

/// <summary>
/// Enforces upstream ZVec platform constraints for index types before native calls.
/// </summary>
internal static class ZVecPlatformRequirements
{
    private static bool IsDiskAnnSupportedPlatform()
    {
        if (OperatingSystem.IsLinux())
            return true;

        if (OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            return true;

        return false;
    }

    /// <summary>
    /// Throws <see cref="PlatformNotSupportedException"/> when <paramref name="param"/>
    /// is not supported on the current OS/architecture.
    /// </summary>
    public static void ThrowIfUnsupported(ZVecIndexParam param)
    {
        ArgumentNullException.ThrowIfNull(param);

        switch (param)
        {
            case ZVecHnswRabitqIndexParam:
            case ZVecIvfRabitqIndexParam:
                if (RuntimeInformation.ProcessArchitecture is Architecture.Arm or Architecture.Arm64)
                {
                    throw new PlatformNotSupportedException(
                        ZVecDefaults.Errors.RabitqRequiresX64Avx2);
                }
                break;

            case ZVecDiskAnnIndexParam:
                if (!IsDiskAnnSupportedPlatform())
                {
                    throw new PlatformNotSupportedException(
                        ZVecDefaults.Errors.DiskAnnRequiresSupportedPlatform);
                }
                break;
        }
    }
}
