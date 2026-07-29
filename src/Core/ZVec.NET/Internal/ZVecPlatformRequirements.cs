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
    /// HNSW-RaBitQ: reject ARM here. On x64 this method allows the type through so
    /// <see cref="NativeIndexParamBuilder"/> can throw the C API gap (no AVX2 CPUID probe).
    /// DiskANN is supported on Linux only (libaio is optional at runtime via dlopen).
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
                        ZVecDefaults.Errors.DiskAnnRequiresLinux);
                }
                break;
        }
    }
}
