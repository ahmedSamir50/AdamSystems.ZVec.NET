using ZVec.NET.Interop;
using FluentAssertions;

namespace ZVec.NET.Tests.Unit.Interop;

public class NativeMethodsTests
{
    [Fact]
    public void NativeMethods_LibraryImport_Compiles()
    {
        // If this compiles, the [LibraryImport] source-generator ran successfully
        // without any syntax or marshaling type mismatch errors.
        // Runtime P/Invoke coverage uses the real native library (Skip when unavailable).
        typeof(NativeMethods).Should().NotBeNull();
    }

    [Fact]
    public void NativeMethods_Constants_ArePinned()
    {
        ZVecNativeAbi.MinimumMajor.Should().Be(0);
        ZVecNativeAbi.MinimumMinor.Should().Be(5);
        ZVecNativeAbi.MinimumPatch.Should().Be(1);
        ZVecNativeAbi.MinimumVersionString.Should().Be("0.5.1");
        ZVecDefaults.Version.ExpectedMajor.Should().Be(ZVecNativeAbi.MinimumMajor);
        ZVecDefaults.Version.ExpectedMinor.Should().Be(ZVecNativeAbi.MinimumMinor);
        ZVecDefaults.Version.ExpectedPatch.Should().Be(ZVecNativeAbi.MinimumPatch);
    }
}
