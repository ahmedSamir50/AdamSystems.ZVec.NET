using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Helpers;

/// <summary>
/// Shared probe for tests that need a real RID binary but do not use <see cref="Integration.ZVecRealNativeFixture"/>.
/// </summary>
internal static class RealNativeAvailability
{
    /// <summary>
    /// Skips the current test when <c>zvec_c_api</c> cannot be loaded (no runtimes/{rid}/native).
    /// </summary>
    public static void SkipIfUnavailable()
    {
        try
        {
            NativeLibraryResolver.UseRealLibrary();
            NativeLibraryResolver.EnsureLoaded();
            var version = NativeMethods.GetVersionString();
            if (string.IsNullOrEmpty(version))
                Assert.Skip("Real ZVec native library is not available. Skipping test.");
        }
        catch (DllNotFoundException)
        {
            Assert.Skip("Real ZVec native library is not available. Skipping test.");
        }
        catch (EntryPointNotFoundException)
        {
            Assert.Skip("Real ZVec native library is not available. Skipping test.");
        }
    }
}
