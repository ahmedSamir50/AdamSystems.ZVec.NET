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
        // Full runtime P/Invoke testing will be performed in Epic E17 (Mock Library).
        typeof(NativeMethods).Should().NotBeNull();
    }

    [Fact]
    public void NativeMethods_Constants_ArePinned()
    {
        ZVecDefaults.Version.ExpectedMajor.Should().Be(0);
        ZVecDefaults.Version.ExpectedMinor.Should().Be(5);
        ZVecDefaults.Version.ExpectedPatch.Should().Be(1);
    }
}
